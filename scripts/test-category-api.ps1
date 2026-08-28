param(
    [string]$BaseUrl = "http://localhost:5296"
)

$ErrorActionPreference = "Stop"
$script:Passed = 0
$script:Failed = 0
$script:AdminToken = $null
$script:CreatedCategoryIds = [System.Collections.Generic.List[string]]::new()

function Write-TestResult {
    param(
        [bool]$Passed,
        [string]$Message
    )

    if ($Passed) {
        $script:Passed++
        Write-Host "[PASS] $Message" -ForegroundColor Green
    }
    else {
        $script:Failed++
        Write-Host "[FAIL] $Message" -ForegroundColor Red
    }
}

function Assert-Status {
    param(
        [string]$TestName,
        [int]$Expected,
        [int]$Actual,
        [bool]$Condition = $true,
        [string]$ConditionFailure = "response verification failed"
    )

    if ($Actual -ne $Expected) {
        Write-TestResult $false "$TestName -> expected $Expected, got $Actual"
        return $false
    }

    if (-not $Condition) {
        Write-TestResult $false "$TestName -> $ConditionFailure"
        return $false
    }

    Write-TestResult $true "$TestName -> $Actual"
    return $true
}

function Invoke-ApiRequest {
    param(
        [ValidateSet("GET", "POST", "PUT", "PATCH")]
        [string]$Method,
        [string]$Path,
        [string]$Token,
        [object]$Body
    )

    $parameters = @{
        Uri = "$($BaseUrl.TrimEnd('/'))$Path"
        Method = $Method
        UseBasicParsing = $true
    }

    if ($Token) {
        $parameters.Headers = @{ Authorization = "Bearer $Token" }
    }

    if ($null -ne $Body) {
        $parameters.ContentType = "application/json"
        $parameters.Body = $Body | ConvertTo-Json -Depth 10 -Compress
    }

    try {
        $response = Invoke-WebRequest @parameters
        $data = if ([string]::IsNullOrWhiteSpace($response.Content)) {
            $null
        }
        else {
            $response.Content | ConvertFrom-Json
        }

        return [pscustomobject]@{
            StatusCode = [int]$response.StatusCode
            Data = $data
        }
    }
    catch {
        $statusCode = 0
        $responseBody = $null

        if ($null -ne $_.Exception.Response) {
            $statusCode = [int]$_.Exception.Response.StatusCode
        }

        if (-not [string]::IsNullOrWhiteSpace($_.ErrorDetails.Message)) {
            $rawErrorBody = $_.ErrorDetails.Message

            try {
                $responseBody = $rawErrorBody | ConvertFrom-Json
            }
            catch {
                $responseBody = $rawErrorBody
            }
        }

        return [pscustomobject]@{
            StatusCode = $statusCode
            Data = $responseBody
        }
    }
}

function Login {
    param(
        [string]$Label,
        [string]$Email,
        [string]$Password
    )

    $response = Invoke-ApiRequest `
        -Method POST `
        -Path "/api/auth/login" `
        -Body @{ email = $Email; password = $Password }

    $hasToken = $response.StatusCode -eq 200 -and
        $null -ne $response.Data -and
        -not [string]::IsNullOrWhiteSpace($response.Data.accessToken)

    if (-not $hasToken) {
        Write-TestResult $false "Login as $Label -> expected 200 with access token, got $($response.StatusCode)"
        return $null
    }

    return $response.Data.accessToken
}

function Test-RequiredEnvironmentVariables {
    $requiredVariables = @(
        "MBI_ADMIN_EMAIL",
        "MBI_ADMIN_PASSWORD",
        "MBI_EMPLOYEE_EMAIL",
        "MBI_EMPLOYEE_PASSWORD"
    )

    $missingVariables = @(
        $requiredVariables | Where-Object {
            [string]::IsNullOrWhiteSpace(
                [Environment]::GetEnvironmentVariable($_))
        }
    )

    if ($missingVariables.Count -gt 0) {
        throw "Missing required environment variable(s): $($missingVariables -join ', '). Set them before running this script."
    }
}

function Complete-TestRun {
    Write-Host ""
    Write-Host "Passed: $script:Passed"
    Write-Host "Failed: $script:Failed"

    if ($script:Failed -gt 0) {
        exit 1
    }

    exit 0
}

Test-RequiredEnvironmentVariables

$adminEmail = [Environment]::GetEnvironmentVariable("MBI_ADMIN_EMAIL")
$adminPassword = [Environment]::GetEnvironmentVariable("MBI_ADMIN_PASSWORD")
$employeeEmail = [Environment]::GetEnvironmentVariable("MBI_EMPLOYEE_EMAIL")
$employeePassword = [Environment]::GetEnvironmentVariable("MBI_EMPLOYEE_PASSWORD")

$suffix = [Guid]::NewGuid().ToString("N").Substring(0, 10)
$firstName = "Codex Test Electronics $suffix"
$secondName = "Codex Test Supplies $suffix"
$updatedName = "Codex Test Equipment $suffix"
$firstCategoryId = $null
$secondCategoryId = $null

try {
    $response = Invoke-ApiRequest -Method GET -Path "/api/categories"
    [void](Assert-Status "GET categories without JWT" 401 $response.StatusCode)

    $employeeToken = Login "employee" $employeeEmail $employeePassword
    if (-not $employeeToken) {
        Complete-TestRun
    }

    $response = Invoke-ApiRequest `
        -Method GET `
        -Path "/api/categories" `
        -Token $employeeToken
    [void](Assert-Status "GET categories with employee" 200 $response.StatusCode)

    $response = Invoke-ApiRequest `
        -Method POST `
        -Path "/api/categories" `
        -Token $employeeToken `
        -Body @{ name = "Codex Authorization Test $suffix"; description = "Authorization test" }
    [void](Assert-Status "POST category with employee" 403 $response.StatusCode)

    $script:AdminToken = Login "SuperAdmin" $adminEmail $adminPassword
    if (-not $script:AdminToken) {
        Complete-TestRun
    }

    $response = Invoke-ApiRequest `
        -Method POST `
        -Path "/api/categories" `
        -Token $script:AdminToken `
        -Body @{ name = $firstName; description = "Electronic devices and accessories" }

    $propertyNames = @($response.Data.PSObject.Properties.Name)
    $hasSensitiveProperty = @("password", "passwordHash", "accessToken") |
        Where-Object { $propertyNames -contains $_ }
    $createValid = $null -ne $response.Data -and
        -not [string]::IsNullOrWhiteSpace([string]$response.Data.id) -and
        $response.Data.name -eq $firstName -and
        $response.Data.isActive -eq $true -and
        @($hasSensitiveProperty).Count -eq 0

    if (Assert-Status "POST unique category" 201 $response.StatusCode $createValid "response fields were invalid or sensitive") {
        $firstCategoryId = [string]$response.Data.id
        $script:CreatedCategoryIds.Add($firstCategoryId)
    }

    $response = Invoke-ApiRequest `
        -Method POST `
        -Path "/api/categories" `
        -Token $script:AdminToken `
        -Body @{ name = $firstName; description = "Exact duplicate" }
    [void](Assert-Status "POST exact duplicate category" 409 $response.StatusCode)

    $response = Invoke-ApiRequest `
        -Method POST `
        -Path "/api/categories" `
        -Token $script:AdminToken `
        -Body @{ name = $firstName.ToUpperInvariant(); description = "Case duplicate" }
    [void](Assert-Status "POST case-insensitive duplicate category" 409 $response.StatusCode)

    $response = Invoke-ApiRequest `
        -Method POST `
        -Path "/api/categories" `
        -Token $script:AdminToken `
        -Body @{ description = "Missing name" }
    [void](Assert-Status "POST category without name" 400 $response.StatusCode)

    $response = Invoke-ApiRequest `
        -Method GET `
        -Path "/api/categories" `
        -Token $script:AdminToken
    $firstExists = $firstCategoryId -and
        $null -ne (@($response.Data) | Where-Object { [string]$_.id -eq $firstCategoryId })
    [void](Assert-Status "GET categories contains created category" 200 $response.StatusCode $firstExists "created category was absent")

    if ($firstCategoryId) {
        $response = Invoke-ApiRequest `
            -Method GET `
            -Path "/api/categories/$firstCategoryId" `
            -Token $script:AdminToken
        $idMatches = [string]$response.Data.id -eq $firstCategoryId
        [void](Assert-Status "GET created category by ID" 200 $response.StatusCode $idMatches "returned ID did not match")
    }
    else {
        Write-TestResult $false "GET created category by ID -> creation did not return an ID"
    }

    $response = Invoke-ApiRequest `
        -Method GET `
        -Path "/api/categories/11111111-1111-1111-1111-111111111111" `
        -Token $script:AdminToken
    [void](Assert-Status "GET unknown category" 404 $response.StatusCode)

    $response = Invoke-ApiRequest `
        -Method POST `
        -Path "/api/categories" `
        -Token $script:AdminToken `
        -Body @{ name = $secondName; description = "Duplicate update target" }
    $secondValid = $response.StatusCode -eq 201 -and
        -not [string]::IsNullOrWhiteSpace([string]$response.Data.id)
    if (Assert-Status "POST second unique category" 201 $response.StatusCode $secondValid "response did not contain an ID") {
        $secondCategoryId = [string]$response.Data.id
        $script:CreatedCategoryIds.Add($secondCategoryId)
    }

    if ($firstCategoryId) {
        $response = Invoke-ApiRequest `
            -Method PUT `
            -Path "/api/categories/$firstCategoryId" `
            -Token $script:AdminToken `
            -Body @{ name = $updatedName; description = "Updated category description" }
        $updateValid = $response.Data.name -eq $updatedName -and
            $response.Data.description -eq "Updated category description" -and
            $null -ne $response.Data.updatedAt
        [void](Assert-Status "PUT category" 200 $response.StatusCode $updateValid "updated fields were not returned")

        $response = Invoke-ApiRequest `
            -Method PUT `
            -Path "/api/categories/$firstCategoryId" `
            -Token $script:AdminToken `
            -Body @{ name = $secondName; description = "Duplicate update" }
        [void](Assert-Status "PUT category with duplicate name" 409 $response.StatusCode)

        $response = Invoke-ApiRequest `
            -Method PUT `
            -Path "/api/categories/$firstCategoryId" `
            -Token $employeeToken `
            -Body @{ name = $updatedName; description = "Employee update attempt" }
        [void](Assert-Status "PUT category with employee" 403 $response.StatusCode)

        $response = Invoke-ApiRequest `
            -Method PATCH `
            -Path "/api/categories/$firstCategoryId/status" `
            -Token $script:AdminToken `
            -Body @{ isActive = $false }
        [void](Assert-Status "PATCH category inactive" 200 $response.StatusCode ($response.Data.isActive -eq $false) "isActive was not false")

        $response = Invoke-ApiRequest `
            -Method GET `
            -Path "/api/categories" `
            -Token $script:AdminToken
        $inactiveHidden = $null -eq (@($response.Data) | Where-Object { [string]$_.id -eq $firstCategoryId })
        [void](Assert-Status "GET categories hides inactive category" 200 $response.StatusCode $inactiveHidden "inactive category was returned")

        $response = Invoke-ApiRequest `
            -Method GET `
            -Path "/api/categories?includeInactive=true" `
            -Token $script:AdminToken
        $inactiveIncluded = $null -ne (@($response.Data) | Where-Object { [string]$_.id -eq $firstCategoryId })
        [void](Assert-Status "GET categories includes inactive category" 200 $response.StatusCode $inactiveIncluded "inactive category was absent")

        $response = Invoke-ApiRequest `
            -Method PATCH `
            -Path "/api/categories/$firstCategoryId/status" `
            -Token $script:AdminToken `
            -Body @{ isActive = $true }
        [void](Assert-Status "PATCH category active" 200 $response.StatusCode ($response.Data.isActive -eq $true) "isActive was not true")

        $response = Invoke-ApiRequest `
            -Method PATCH `
            -Path "/api/categories/$firstCategoryId/status" `
            -Token $employeeToken `
            -Body @{ isActive = $false }
        [void](Assert-Status "PATCH category with employee" 403 $response.StatusCode)

        $response = Invoke-ApiRequest `
            -Method GET `
            -Path "/api/categories" `
            -Token $script:AdminToken
        $reactivatedExists = $null -ne (@($response.Data) | Where-Object { [string]$_.id -eq $firstCategoryId })
        [void](Assert-Status "Final GET contains reactivated category" 200 $response.StatusCode $reactivatedExists "reactivated category was absent")
    }
}
finally {
    if ($script:AdminToken) {
        foreach ($categoryId in $script:CreatedCategoryIds) {
            $response = Invoke-ApiRequest `
                -Method PATCH `
                -Path "/api/categories/$categoryId/status" `
                -Token $script:AdminToken `
                -Body @{ isActive = $false }

            $cleanupValid = $response.StatusCode -eq 200 -and
                $response.Data.isActive -eq $false
            [void](Assert-Status "Cleanup deactivate category $categoryId" 200 $response.StatusCode $cleanupValid "category was not deactivated")
        }
    }
}

Complete-TestRun
