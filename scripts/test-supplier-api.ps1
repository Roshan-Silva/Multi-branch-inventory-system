param([string]$BaseUrl = "http://localhost:5296")

$ErrorActionPreference = "Stop"
$script:Passed = 0
$script:Failed = 0
$script:AdminToken = $null
$script:SupplierIds = [System.Collections.Generic.List[string]]::new()

function Write-Result([bool]$Success, [string]$Message) {
    if ($Success) {
        $script:Passed++
        Write-Host "[PASS] $Message" -ForegroundColor Green
    }
    else {
        $script:Failed++
        Write-Host "[FAIL] $Message" -ForegroundColor Red
    }
}

function Assert-Status(
    [string]$Name,
    [int]$Expected,
    [int]$Actual,
    [bool]$Condition = $true,
    [string]$Failure = "response verification failed") {
    if ($Actual -ne $Expected) {
        Write-Result $false "$Name -> expected $Expected, got $Actual"
        return $false
    }
    if (-not $Condition) {
        Write-Result $false "$Name -> $Failure"
        return $false
    }
    Write-Result $true "$Name -> $Actual"
    return $true
}

function Invoke-Api(
    [string]$Method,
    [string]$Path,
    [string]$Token,
    [object]$Body) {
    $parameters = @{
        Uri = "$($BaseUrl.TrimEnd('/'))$Path"
        Method = $Method
        UseBasicParsing = $true
    }
    if ($Token) { $parameters.Headers = @{ Authorization = "Bearer $Token" } }
    if ($null -ne $Body) {
        $parameters.ContentType = "application/json"
        $parameters.Body = $Body | ConvertTo-Json -Depth 10 -Compress
    }

    try {
        $response = Invoke-WebRequest @parameters
        $data = if ($response.Content) { $response.Content | ConvertFrom-Json } else { $null }
        return [pscustomobject]@{ StatusCode = [int]$response.StatusCode; Data = $data }
    }
    catch {
        $status = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }
        return [pscustomobject]@{ StatusCode = $status; Data = $null }
    }
}

function Login([string]$Label, [string]$Email, [string]$Password) {
    $response = Invoke-Api POST "/api/auth/login" $null @{
        email = $Email
        password = $Password
    }
    if ($response.StatusCode -ne 200 -or
        [string]::IsNullOrWhiteSpace([string]$response.Data.accessToken)) {
        Write-Result $false "Login as $Label -> expected 200 with access token, got $($response.StatusCode)"
        return $null
    }
    return $response.Data.accessToken
}

function Require-Environment {
    $names = @(
        "MBI_ADMIN_EMAIL", "MBI_ADMIN_PASSWORD",
        "MBI_EMPLOYEE_EMAIL", "MBI_EMPLOYEE_PASSWORD"
    )
    $missing = @($names | Where-Object {
        [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($_))
    })
    if ($missing.Count -gt 0) {
        throw "Missing required environment variable(s): $($missing -join ', ')"
    }
}

function Finish {
    Write-Host ""
    Write-Host "Passed: $script:Passed"
    Write-Host "Failed: $script:Failed"
    if ($script:Failed -gt 0) { exit 1 }
    exit 0
}

Require-Environment
$adminEmail = [Environment]::GetEnvironmentVariable("MBI_ADMIN_EMAIL")
$adminPassword = [Environment]::GetEnvironmentVariable("MBI_ADMIN_PASSWORD")
$employeeEmail = [Environment]::GetEnvironmentVariable("MBI_EMPLOYEE_EMAIL")
$employeePassword = [Environment]::GetEnvironmentVariable("MBI_EMPLOYEE_PASSWORD")
$suffix = [Guid]::NewGuid().ToString("N").Substring(0, 10)
$code = "SUP-$suffix".ToUpperInvariant()
$updatedCode = "SUP-UP-$suffix".ToUpperInvariant()
$supplierId = $null

try {
    $response = Invoke-Api GET "/api/suppliers" $null $null
    [void](Assert-Status "GET suppliers without JWT" 401 $response.StatusCode)

    $employeeToken = Login "employee" $employeeEmail $employeePassword
    $script:AdminToken = Login "SuperAdmin" $adminEmail $adminPassword
    if (-not $employeeToken -or -not $script:AdminToken) { Finish }

    $response = Invoke-Api GET "/api/suppliers" $employeeToken $null
    [void](Assert-Status "GET suppliers with employee" 200 $response.StatusCode)

    $response = Invoke-Api POST "/api/suppliers" $employeeToken @{
        code = "AUTH-$suffix"
        name = "Authorization Supplier"
    }
    [void](Assert-Status "POST supplier with employee" 403 $response.StatusCode)

    $response = Invoke-Api POST "/api/suppliers" $script:AdminToken @{
        code = $code
        name = "Smoke Test Supplier $suffix"
        contactPerson = "Test Contact"
        email = "supplier-$suffix@example.com"
        phoneNumber = "+1 555 0100"
        address = "Test Address"
    }
    $validCreate = $response.Data.code -eq $code -and $response.Data.isActive -eq $true
    if (Assert-Status "POST supplier with SuperAdmin" 201 $response.StatusCode $validCreate) {
        $supplierId = [string]$response.Data.id
        $script:SupplierIds.Add($supplierId)
    }

    $response = Invoke-Api POST "/api/suppliers" $script:AdminToken @{
        code = $code
        name = "Exact Duplicate"
    }
    [void](Assert-Status "POST duplicate supplier code" 409 $response.StatusCode)

    $response = Invoke-Api POST "/api/suppliers" $script:AdminToken @{
        code = $code.ToLowerInvariant()
        name = "Case Duplicate"
    }
    [void](Assert-Status "POST case-insensitive duplicate supplier code" 409 $response.StatusCode)

    $response = Invoke-Api GET "/api/suppliers" $employeeToken $null
    $listed = $supplierId -and
        $null -ne (@($response.Data) | Where-Object { [string]$_.id -eq $supplierId })
    [void](Assert-Status "GET suppliers contains created supplier" 200 $response.StatusCode $listed)

    if ($supplierId) {
        $response = Invoke-Api GET "/api/suppliers/$supplierId" $employeeToken $null
        [void](Assert-Status "GET supplier by ID" 200 $response.StatusCode ([string]$response.Data.id -eq $supplierId))
    }

    $response = Invoke-Api GET "/api/suppliers/11111111-1111-1111-1111-111111111111" $employeeToken $null
    [void](Assert-Status "GET unknown supplier" 404 $response.StatusCode)

    if ($supplierId) {
        $response = Invoke-Api PUT "/api/suppliers/$supplierId" $script:AdminToken @{
            code = $updatedCode
            name = "Updated Smoke Test Supplier"
            contactPerson = "Updated Contact"
            email = "updated-$suffix@example.com"
            phoneNumber = "+1 555 0200"
            address = "Updated Address"
        }
        [void](Assert-Status "PUT supplier" 200 $response.StatusCode ($response.Data.code -eq $updatedCode))

        $response = Invoke-Api PATCH "/api/suppliers/$supplierId/status" $script:AdminToken @{ isActive = $false }
        [void](Assert-Status "PATCH supplier inactive" 200 $response.StatusCode ($response.Data.isActive -eq $false))

        $response = Invoke-Api GET "/api/suppliers" $employeeToken $null
        $hidden = $null -eq (@($response.Data) | Where-Object { [string]$_.id -eq $supplierId })
        [void](Assert-Status "Normal GET hides inactive supplier" 200 $response.StatusCode $hidden)

        $response = Invoke-Api GET "/api/suppliers?includeInactive=true" $employeeToken $null
        $included = $null -ne (@($response.Data) | Where-Object { [string]$_.id -eq $supplierId })
        [void](Assert-Status "includeInactive shows supplier" 200 $response.StatusCode $included)
    }
}
finally {
    if ($script:AdminToken) {
        foreach ($id in $script:SupplierIds) {
            $response = Invoke-Api PATCH "/api/suppliers/$id/status" $script:AdminToken @{ isActive = $false }
            [void](Assert-Status "Cleanup deactivate supplier $id" 200 $response.StatusCode ($response.Data.isActive -eq $false))
        }
    }
}

Finish
