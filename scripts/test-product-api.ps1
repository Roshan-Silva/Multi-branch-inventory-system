param([string]$BaseUrl = "http://localhost:5296")

$ErrorActionPreference = "Stop"
$script:Passed = 0
$script:Failed = 0
$script:AdminToken = $null
$script:ProductIds = [System.Collections.Generic.List[string]]::new()
$script:CategoryIds = [System.Collections.Generic.List[string]]::new()

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

    if ($Token) {
        $parameters.Headers = @{ Authorization = "Bearer $Token" }
    }

    if ($null -ne $Body) {
        $parameters.ContentType = "application/json"
        $parameters.Body = $Body | ConvertTo-Json -Depth 10 -Compress
    }

    try {
        $response = Invoke-WebRequest @parameters
        $data = if ($response.Content) {
            $response.Content | ConvertFrom-Json
        }
        else { $null }

        return [pscustomobject]@{
            StatusCode = [int]$response.StatusCode
            Data = $data
        }
    }
    catch {
        $status = if ($_.Exception.Response) {
            [int]$_.Exception.Response.StatusCode
        }
        else { 0 }

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
$sku = "TEST-$suffix".ToUpperInvariant()
$updatedSku = "UPDATED-$suffix".ToUpperInvariant()
$productId = $null
$activeCategoryId = $null

try {
    $response = Invoke-Api GET "/api/products" $null $null
    [void](Assert-Status "GET products without JWT" 401 $response.StatusCode)

    $employeeToken = Login "employee" $employeeEmail $employeePassword
    $script:AdminToken = Login "SuperAdmin" $adminEmail $adminPassword
    if (-not $employeeToken -or -not $script:AdminToken) { Finish }

    $response = Invoke-Api GET "/api/products" $employeeToken $null
    [void](Assert-Status "GET products with employee" 200 $response.StatusCode)

    $response = Invoke-Api POST "/api/products" $employeeToken @{
        sku = "AUTH-$suffix"
        name = "Authorization Test"
        categoryId = [Guid]::NewGuid()
        unitPrice = 1
    }
    [void](Assert-Status "POST product with employee" 403 $response.StatusCode)

    $category = Invoke-Api POST "/api/categories" $script:AdminToken @{
        name = "Product Test Category $suffix"
        description = "Temporary smoke-test category"
    }
    if (Assert-Status "Create active test category" 201 $category.StatusCode (-not [string]::IsNullOrWhiteSpace([string]$category.Data.id))) {
        $activeCategoryId = [string]$category.Data.id
        $script:CategoryIds.Add($activeCategoryId)
    }

    $response = Invoke-Api POST "/api/products" $script:AdminToken @{
        sku = $sku
        name = "Smoke Test Product $suffix"
        description = "Product smoke test"
        categoryId = $activeCategoryId
        unitPrice = 125.50
    }
    $validCreate = $response.Data.sku -eq $sku -and
        $response.Data.categoryId -eq $activeCategoryId -and
        $response.Data.isActive -eq $true
    if (Assert-Status "POST product with SuperAdmin" 201 $response.StatusCode $validCreate) {
        $productId = [string]$response.Data.id
        $script:ProductIds.Add($productId)
    }

    $response = Invoke-Api POST "/api/products" $script:AdminToken @{
        sku = $sku
        name = "Exact Duplicate"
        categoryId = $activeCategoryId
        unitPrice = 1
    }
    [void](Assert-Status "POST duplicate SKU" 409 $response.StatusCode)

    $response = Invoke-Api POST "/api/products" $script:AdminToken @{
        sku = $sku.ToLowerInvariant()
        name = "Case Duplicate"
        categoryId = $activeCategoryId
        unitPrice = 1
    }
    [void](Assert-Status "POST case-insensitive duplicate SKU" 409 $response.StatusCode)

    $response = Invoke-Api POST "/api/products" $script:AdminToken @{
        sku = "UNKNOWN-$suffix"
        name = "Unknown Category"
        categoryId = [Guid]::NewGuid()
        unitPrice = 1
    }
    [void](Assert-Status "POST product with unknown category" 404 $response.StatusCode)

    $inactiveCategory = Invoke-Api POST "/api/categories" $script:AdminToken @{
        name = "Inactive Product Category $suffix"
    }
    $inactiveCategoryId = [string]$inactiveCategory.Data.id
    if ($inactiveCategory.StatusCode -eq 201) {
        $script:CategoryIds.Add($inactiveCategoryId)
        [void](Invoke-Api PATCH "/api/categories/$inactiveCategoryId/status" $script:AdminToken @{ isActive = $false })
    }
    $response = Invoke-Api POST "/api/products" $script:AdminToken @{
        sku = "INACTIVE-$suffix"
        name = "Inactive Category Product"
        categoryId = $inactiveCategoryId
        unitPrice = 1
    }
    [void](Assert-Status "POST product with inactive category" 400 $response.StatusCode)

    $response = Invoke-Api POST "/api/products" $script:AdminToken @{
        sku = "NEGATIVE-$suffix"
        name = "Negative Price Product"
        categoryId = $activeCategoryId
        unitPrice = -1
    }
    [void](Assert-Status "POST product with negative price" 400 $response.StatusCode)

    if ($productId) {
        $response = Invoke-Api GET "/api/products/$productId" $employeeToken $null
        [void](Assert-Status "GET product by ID" 200 $response.StatusCode ([string]$response.Data.id -eq $productId))
    }

    $response = Invoke-Api GET "/api/products/11111111-1111-1111-1111-111111111111" $employeeToken $null
    [void](Assert-Status "GET unknown product" 404 $response.StatusCode)

    if ($productId) {
        $response = Invoke-Api PUT "/api/products/$productId" $script:AdminToken @{
            sku = $updatedSku
            name = "Updated Smoke Test Product"
            description = "Updated"
            categoryId = $activeCategoryId
            unitPrice = 250
        }
        [void](Assert-Status "PUT product" 200 $response.StatusCode ($response.Data.sku -eq $updatedSku))

        $response = Invoke-Api PATCH "/api/products/$productId/status" $script:AdminToken @{ isActive = $false }
        [void](Assert-Status "PATCH product inactive" 200 $response.StatusCode ($response.Data.isActive -eq $false))

        $response = Invoke-Api GET "/api/products" $employeeToken $null
        $hidden = $null -eq (@($response.Data) | Where-Object { [string]$_.id -eq $productId })
        [void](Assert-Status "Normal GET hides inactive product" 200 $response.StatusCode $hidden)

        $response = Invoke-Api GET "/api/products?includeInactive=true" $employeeToken $null
        $included = $null -ne (@($response.Data) | Where-Object { [string]$_.id -eq $productId })
        [void](Assert-Status "includeInactive shows product" 200 $response.StatusCode $included)
    }
}
finally {
    if ($script:AdminToken) {
        foreach ($id in $script:ProductIds) {
            $response = Invoke-Api PATCH "/api/products/$id/status" $script:AdminToken @{ isActive = $false }
            [void](Assert-Status "Cleanup deactivate product $id" 200 $response.StatusCode ($response.Data.isActive -eq $false))
        }
        foreach ($id in $script:CategoryIds) {
            $response = Invoke-Api PATCH "/api/categories/$id/status" $script:AdminToken @{ isActive = $false }
            [void](Assert-Status "Cleanup deactivate category $id" 200 $response.StatusCode ($response.Data.isActive -eq $false))
        }
    }
}

Finish
