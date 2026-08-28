param([string]$BaseUrl = "http://localhost:5296")

$ErrorActionPreference = "Stop"
$script:Passed = 0
$script:Failed = 0
$script:AdminToken = $null
$script:CleanupTargets = [System.Collections.Generic.List[object]]::new()

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

function Invoke-Api([string]$Method, [string]$Path, [string]$Token, [object]$Body) {
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
    return [pscustomobject]@{
        Token = [string]$response.Data.accessToken
        BranchId = [string]$response.Data.user.branchId
        Role = [string]$response.Data.user.role
    }
}

function Require-Environment {
    $names = @(
        "MBI_ADMIN_EMAIL", "MBI_ADMIN_PASSWORD",
        "MBI_INVENTORY_EMAIL", "MBI_INVENTORY_PASSWORD"
    )
    $missing = @($names | Where-Object {
        [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($_))
    })
    if ($missing.Count -gt 0) {
        throw "Missing required environment variable(s): $($missing -join ', ')"
    }
}

function Register-Cleanup([string]$Path, [string]$Id) {
    if ($Id) {
        $script:CleanupTargets.Add([pscustomobject]@{ Path = $Path; Id = $Id })
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
$inventoryEmail = [Environment]::GetEnvironmentVariable("MBI_INVENTORY_EMAIL")
$inventoryPassword = [Environment]::GetEnvironmentVariable("MBI_INVENTORY_PASSWORD")
$suffix = [Guid]::NewGuid().ToString("N").Substring(0, 10)
$ownInventoryId = $null
$otherInventoryId = $null
$otherTransactionId = $null

try {
    $response = Invoke-Api GET "/api/inventory" $null $null
    [void](Assert-Status "GET inventory without JWT" 401 $response.StatusCode)

    $admin = Login "SuperAdmin" $adminEmail $adminPassword
    $officer = Login "InventoryOfficer" $inventoryEmail $inventoryPassword
    if (-not $admin -or -not $officer) { Finish }
    $script:AdminToken = $admin.Token

    if ([string]::IsNullOrWhiteSpace($officer.BranchId)) {
        Write-Result $false "InventoryOfficer login -> branchId claim is missing"
        Finish
    }

    $response = Invoke-Api GET "/api/inventory" $officer.Token $null
    [void](Assert-Status "GET inventory as InventoryOfficer" 200 $response.StatusCode)

    $response = Invoke-Api POST "/api/inventory" $officer.Token @{
        branchId = $officer.BranchId
        productId = [Guid]::NewGuid()
        minimumStockLevel = 1
        reorderLevel = 2
    }
    [void](Assert-Status "POST inventory as InventoryOfficer" 403 $response.StatusCode)

    $category = Invoke-Api POST "/api/categories" $admin.Token @{
        name = "Inventory Test Category $suffix"
    }
    $categoryId = [string]$category.Data.id
    [void](Assert-Status "Setup category" 201 $category.StatusCode (-not [string]::IsNullOrWhiteSpace($categoryId)))
    Register-Cleanup "/api/categories" $categoryId

    $product = Invoke-Api POST "/api/products" $admin.Token @{
        sku = "INV-$suffix"
        name = "Inventory Test Product $suffix"
        categoryId = $categoryId
        unitPrice = 10
    }
    $productId = [string]$product.Data.id
    [void](Assert-Status "Setup active product" 201 $product.StatusCode (-not [string]::IsNullOrWhiteSpace($productId)))
    Register-Cleanup "/api/products" $productId

    $otherBranch = Invoke-Api POST "/api/branches" $admin.Token @{
        code = "OT-$suffix"
        name = "Other Inventory Branch $suffix"
    }
    $otherBranchId = [string]$otherBranch.Data.id
    [void](Assert-Status "Setup other branch" 201 $otherBranch.StatusCode (-not [string]::IsNullOrWhiteSpace($otherBranchId)))
    Register-Cleanup "/api/branches" $otherBranchId

    $response = Invoke-Api POST "/api/inventory" $admin.Token @{
        branchId = $officer.BranchId
        productId = $productId
        minimumStockLevel = 2
        reorderLevel = 8
    }
    $validInventory = $response.Data.quantityOnHand -eq 0 -and
        [string]$response.Data.branchId -eq $officer.BranchId
    if (Assert-Status "SuperAdmin creates inventory" 201 $response.StatusCode $validInventory) {
        $ownInventoryId = [string]$response.Data.id
    }

    $response = Invoke-Api POST "/api/inventory" $admin.Token @{
        branchId = $officer.BranchId
        productId = $productId
        minimumStockLevel = 2
        reorderLevel = 8
    }
    [void](Assert-Status "Duplicate branch and product inventory" 409 $response.StatusCode)

    $response = Invoke-Api POST "/api/inventory" $admin.Token @{
        branchId = [Guid]::NewGuid()
        productId = $productId
        minimumStockLevel = 1
        reorderLevel = 2
    }
    [void](Assert-Status "Inventory with unknown branch" 404 $response.StatusCode)

    $inactiveBranch = Invoke-Api POST "/api/branches" $admin.Token @{
        code = "IB-$suffix"
        name = "Inactive Inventory Branch $suffix"
    }
    $inactiveBranchId = [string]$inactiveBranch.Data.id
    Register-Cleanup "/api/branches" $inactiveBranchId
    [void](Invoke-Api PATCH "/api/branches/$inactiveBranchId/status" $admin.Token @{ isActive = $false })
    $response = Invoke-Api POST "/api/inventory" $admin.Token @{
        branchId = $inactiveBranchId
        productId = $productId
        minimumStockLevel = 1
        reorderLevel = 2
    }
    [void](Assert-Status "Inventory with inactive branch" 400 $response.StatusCode)

    $response = Invoke-Api POST "/api/inventory" $admin.Token @{
        branchId = $otherBranchId
        productId = [Guid]::NewGuid()
        minimumStockLevel = 1
        reorderLevel = 2
    }
    [void](Assert-Status "Inventory with unknown product" 404 $response.StatusCode)

    $inactiveProduct = Invoke-Api POST "/api/products" $admin.Token @{
        sku = "INACTIVE-$suffix"
        name = "Inactive Inventory Product $suffix"
        categoryId = $categoryId
        unitPrice = 1
    }
    $inactiveProductId = [string]$inactiveProduct.Data.id
    Register-Cleanup "/api/products" $inactiveProductId
    [void](Invoke-Api PATCH "/api/products/$inactiveProductId/status" $admin.Token @{ isActive = $false })
    $response = Invoke-Api POST "/api/inventory" $admin.Token @{
        branchId = $otherBranchId
        productId = $inactiveProductId
        minimumStockLevel = 1
        reorderLevel = 2
    }
    [void](Assert-Status "Inventory with inactive product" 400 $response.StatusCode)

    $response = Invoke-Api POST "/api/inventory" $admin.Token @{
        branchId = $otherBranchId
        productId = $productId
        minimumStockLevel = 20
        reorderLevel = 10
    }
    [void](Assert-Status "Inventory with invalid levels" 400 $response.StatusCode)

    if ($ownInventoryId) {
        $response = Invoke-Api GET "/api/inventory/$ownInventoryId" $officer.Token $null
        [void](Assert-Status "GET own inventory by ID" 200 $response.StatusCode ([string]$response.Data.id -eq $ownInventoryId))
    }

    $response = Invoke-Api GET "/api/inventory/11111111-1111-1111-1111-111111111111" $admin.Token $null
    [void](Assert-Status "GET unknown inventory" 404 $response.StatusCode)

    $other = Invoke-Api POST "/api/inventory" $admin.Token @{
        branchId = $otherBranchId
        productId = $productId
        minimumStockLevel = 1
        reorderLevel = 2
    }
    $otherInventoryId = [string]$other.Data.id
    [void](Assert-Status "Setup other-branch inventory" 201 $other.StatusCode (-not [string]::IsNullOrWhiteSpace($otherInventoryId)))

    if ($ownInventoryId) {
        $response = Invoke-Api PUT "/api/inventory/$ownInventoryId/levels" $officer.Token @{
            minimumStockLevel = 3
            reorderLevel = 8
        }
        [void](Assert-Status "InventoryOfficer updates own levels" 200 $response.StatusCode ($response.Data.reorderLevel -eq 8))
    }

    $response = Invoke-Api PUT "/api/inventory/$otherInventoryId/levels" $officer.Token @{
        minimumStockLevel = 1
        reorderLevel = 3
    }
    [void](Assert-Status "InventoryOfficer updates other branch levels" 403 $response.StatusCode)

    if ($ownInventoryId) {
        $response = Invoke-Api POST "/api/inventory/$ownInventoryId/adjustments" $officer.Token @{
            type = 4
            quantity = 10
            referenceNumber = "OPEN-$suffix"
            notes = "Opening smoke-test adjustment"
        }
        [void](Assert-Status "AdjustmentIncrease 10" 200 $response.StatusCode ($response.Data.quantityOnHand -eq 10))

        $ledger = Invoke-Api GET "/api/inventory-transactions?inventoryId=$ownInventoryId" $officer.Token $null
        $increase = @($ledger.Data) | Where-Object {
            $_.type -eq 4 -and $_.quantity -eq 10 -and
            $_.quantityBefore -eq 0 -and $_.quantityAfter -eq 10
        } | Select-Object -First 1
        [void](Assert-Status "Ledger contains AdjustmentIncrease" 200 $ledger.StatusCode ($null -ne $increase))

        $response = Invoke-Api POST "/api/inventory/$ownInventoryId/adjustments" $officer.Token @{
            type = 5
            quantity = 4
            referenceNumber = "DEC-$suffix"
        }
        [void](Assert-Status "AdjustmentDecrease 4" 200 $response.StatusCode ($response.Data.quantityOnHand -eq 6))

        $ledger = Invoke-Api GET "/api/inventory-transactions?inventoryId=$ownInventoryId" $officer.Token $null
        $decrease = @($ledger.Data) | Where-Object {
            $_.type -eq 5 -and $_.quantity -eq 4 -and
            $_.quantityBefore -eq 10 -and $_.quantityAfter -eq 6
        } | Select-Object -First 1
        [void](Assert-Status "Ledger contains AdjustmentDecrease" 200 $ledger.StatusCode ($null -ne $decrease))

        $response = Invoke-Api POST "/api/inventory/$ownInventoryId/adjustments" $officer.Token @{
            type = 5
            quantity = 100
        }
        $unchanged = Invoke-Api GET "/api/inventory/$ownInventoryId" $officer.Token $null
        [void](Assert-Status "Excessive decrease rejected and stock unchanged" 400 $response.StatusCode ($unchanged.Data.quantityOnHand -eq 6))

        $response = Invoke-Api POST "/api/inventory/$ownInventoryId/adjustments" $officer.Token @{
            type = 1
            quantity = 1
        }
        [void](Assert-Status "PurchaseReceipt manual adjustment rejected" 400 $response.StatusCode)

        $lowStock = Invoke-Api GET "/api/inventory?lowStockOnly=true" $officer.Token $null
        $included = $null -ne (@($lowStock.Data) | Where-Object { [string]$_.id -eq $ownInventoryId })
        [void](Assert-Status "lowStockOnly uses quantity <= reorder level" 200 $lowStock.StatusCode $included)
    }

    $response = Invoke-Api POST "/api/inventory/$otherInventoryId/adjustments" $officer.Token @{
        type = 4
        quantity = 1
    }
    [void](Assert-Status "InventoryOfficer adjusts other branch inventory" 403 $response.StatusCode)

    $response = Invoke-Api GET "/api/inventory/$otherInventoryId" $officer.Token $null
    [void](Assert-Status "InventoryOfficer reads other branch inventory" 403 $response.StatusCode)

    $otherAdjustment = Invoke-Api POST "/api/inventory/$otherInventoryId/adjustments" $admin.Token @{
        type = 4
        quantity = 1
        referenceNumber = "OTHER-$suffix"
    }
    [void](Assert-Status "Setup other-branch transaction" 200 $otherAdjustment.StatusCode)
    $otherLedger = Invoke-Api GET "/api/inventory-transactions?inventoryId=$otherInventoryId" $admin.Token $null
    $otherTransaction = @($otherLedger.Data) | Select-Object -First 1
    $otherTransactionId = [string]$otherTransaction.id

    $response = Invoke-Api GET "/api/inventory-transactions/$otherTransactionId" $officer.Token $null
    [void](Assert-Status "InventoryOfficer reads other branch transaction" 403 $response.StatusCode)

    $response = Invoke-Api GET "/api/inventory-transactions" $admin.Token $null
    [void](Assert-Status "SuperAdmin reads all inventory transactions" 200 $response.StatusCode)

    $response = Invoke-Api POST "/api/inventory-transactions" $admin.Token @{ quantity = 1 }
    $noWriteEndpoint = $response.StatusCode -in @(404, 405)
    Write-Result $noWriteEndpoint "No direct inventory transaction write endpoint -> $($response.StatusCode)"
}
finally {
    if ($script:AdminToken) {
        foreach ($target in $script:CleanupTargets) {
            $response = Invoke-Api PATCH "$($target.Path)/$($target.Id)/status" $script:AdminToken @{ isActive = $false }
            [void](Assert-Status "Cleanup deactivate $($target.Path)/$($target.Id)" 200 $response.StatusCode ($response.Data.isActive -eq $false))
        }
    }
}

Finish
