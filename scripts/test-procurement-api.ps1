param([string]$BaseUrl = "http://localhost:5296")

$ErrorActionPreference = "Stop"
$script:Passed = 0
$script:Failed = 0
$script:AdminToken = $null
$script:Cleanup = [System.Collections.Generic.List[object]]::new()

function Write-Result([bool]$Success, [string]$Message) {
    if ($Success) { $script:Passed++; Write-Host "[PASS] $Message" -ForegroundColor Green }
    else { $script:Failed++; Write-Host "[FAIL] $Message" -ForegroundColor Red }
}

function Assert-Status([string]$Name, [int]$Expected, [int]$Actual,
    [bool]$Condition = $true, [string]$Failure = "response verification failed") {
    if ($Actual -ne $Expected) { Write-Result $false "$Name -> expected $Expected, got $Actual"; return $false }
    if (-not $Condition) { Write-Result $false "$Name -> $Failure"; return $false }
    Write-Result $true "$Name -> $Actual"; return $true
}

function Invoke-Api([string]$Method, [string]$Path, [string]$Token, [object]$Body) {
    $p = @{ Uri = "$($BaseUrl.TrimEnd('/'))$Path"; Method = $Method; UseBasicParsing = $true }
    if ($Token) { $p.Headers = @{ Authorization = "Bearer $Token" } }
    if ($null -ne $Body) { $p.ContentType = "application/json"; $p.Body = $Body | ConvertTo-Json -Depth 12 -Compress }
    try {
        $r = Invoke-WebRequest @p
        $data = if ($r.Content) { $r.Content | ConvertFrom-Json } else { $null }
        return [pscustomobject]@{ StatusCode = [int]$r.StatusCode; Data = $data }
    }
    catch {
        $status = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }
        return [pscustomobject]@{ StatusCode = $status; Data = $null }
    }
}

function Login([string]$Label, [string]$Email, [string]$Password) {
    $r = Invoke-Api POST "/api/auth/login" $null @{ email = $Email; password = $Password }
    if ($r.StatusCode -ne 200 -or [string]::IsNullOrWhiteSpace([string]$r.Data.accessToken)) {
        Write-Result $false "Login as $Label -> expected 200 with access token, got $($r.StatusCode)"; return $null
    }
    return [pscustomobject]@{
        Token = [string]$r.Data.accessToken
        BranchId = [string]$r.Data.user.branchId
        UserId = [string]$r.Data.user.id
    }
}

function Require-Environment {
    $names = @(
        "MBI_ADMIN_EMAIL", "MBI_ADMIN_PASSWORD", "MBI_MANAGER_EMAIL", "MBI_MANAGER_PASSWORD",
        "MBI_INVENTORY_EMAIL", "MBI_INVENTORY_PASSWORD", "MBI_PROCUREMENT_EMAIL", "MBI_PROCUREMENT_PASSWORD"
    )
    $missing = @($names | Where-Object { [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($_)) })
    if ($missing.Count -gt 0) { throw "Missing required environment variable(s): $($missing -join ', ')" }
}

function Add-Cleanup([string]$Path, [string]$Id) {
    if ($Id) { $script:Cleanup.Add([pscustomobject]@{ Path = $Path; Id = $Id }) }
}

function New-PrBody([string]$ProductId, [int]$Quantity, [string]$Reason) {
    return @{ reason = $Reason; items = @(@{ productId = $ProductId; requestedQuantity = $Quantity; notes = "Smoke test" }) }
}

function Finish {
    Write-Host ""; Write-Host "Passed: $script:Passed"; Write-Host "Failed: $script:Failed"
    if ($script:Failed -gt 0) { exit 1 }; exit 0
}

Require-Environment
$admin = Login "SuperAdmin" $env:MBI_ADMIN_EMAIL $env:MBI_ADMIN_PASSWORD
$manager = Login "BranchManager" $env:MBI_MANAGER_EMAIL $env:MBI_MANAGER_PASSWORD
$officer = Login "InventoryOfficer" $env:MBI_INVENTORY_EMAIL $env:MBI_INVENTORY_PASSWORD
$procurement = Login "ProcurementOfficer" $env:MBI_PROCUREMENT_EMAIL $env:MBI_PROCUREMENT_PASSWORD
if (-not $admin -or -not $manager -or -not $officer -or -not $procurement) { Finish }
$script:AdminToken = $admin.Token
$suffix = [Guid]::NewGuid().ToString("N").Substring(0, 10)

try {
    if ([string]::IsNullOrWhiteSpace($officer.BranchId) -or $manager.BranchId -ne $officer.BranchId) {
        Write-Result $false "Setup -> InventoryOfficer and BranchManager must have the same non-empty branchId"; Finish
    }

    $r = Invoke-Api GET "/api/purchase-requests" $null $null
    [void](Assert-Status "GET purchase requests without JWT" 401 $r.StatusCode)

    $category = Invoke-Api POST "/api/categories" $admin.Token @{ name = "Procurement Test Category $suffix" }
    $categoryId = [string]$category.Data.id; Add-Cleanup "/api/categories" $categoryId
    $product = Invoke-Api POST "/api/products" $admin.Token @{
        sku = "PROC-$suffix"; name = "Procurement Test Product $suffix"; categoryId = $categoryId; unitPrice = 100
    }
    $productId = [string]$product.Data.id; Add-Cleanup "/api/products" $productId
    $supplier = Invoke-Api POST "/api/suppliers" $admin.Token @{ code = "PS-$suffix"; name = "Procurement Test Supplier $suffix" }
    $supplierId = [string]$supplier.Data.id; Add-Cleanup "/api/suppliers" $supplierId
    [void](Assert-Status "Setup catalog and supplier" 201 $supplier.StatusCode ($product.StatusCode -eq 201 -and $category.StatusCode -eq 201))

    $inventory = Invoke-Api POST "/api/inventory" $admin.Token @{
        branchId = $officer.BranchId; productId = $productId; minimumStockLevel = 0; reorderLevel = 0
    }
    $inventoryId = [string]$inventory.Data.id
    [void](Assert-Status "Setup inventory baseline" 201 $inventory.StatusCode ($inventory.Data.quantityOnHand -eq 0))

    $r = Invoke-Api POST "/api/purchase-requests" $officer.Token (New-PrBody $productId 10 "Low stock restocking")
    $prId = [string]$r.Data.id; $prItemId = [string](@($r.Data.items)[0].id)
    $validPr = $r.Data.status -eq 1 -and [string]$r.Data.branchId -eq $officer.BranchId
    [void](Assert-Status "InventoryOfficer creates Draft own-branch PR" 201 $r.StatusCode $validPr)

    $r = Invoke-Api POST "/api/purchase-requests" $officer.Token @{
        reason = "Duplicates"; items = @(
            @{ productId = $productId; requestedQuantity = 1 },
            @{ productId = $productId; requestedQuantity = 2 })
    }
    [void](Assert-Status "PR duplicate product item" 400 $r.StatusCode)
    $r = Invoke-Api POST "/api/purchase-requests" $officer.Token (New-PrBody $productId 0 "Invalid quantity")
    [void](Assert-Status "PR quantity zero" 400 $r.StatusCode)
    $r = Invoke-Api POST "/api/purchase-requests" $officer.Token (New-PrBody ([Guid]::NewGuid()) 1 "Unknown product")
    [void](Assert-Status "PR unknown product" 404 $r.StatusCode)

    $inactiveProduct = Invoke-Api POST "/api/products" $admin.Token @{
        sku = "PI-$suffix"; name = "Inactive Procurement Product $suffix"; categoryId = $categoryId; unitPrice = 1
    }
    $inactiveProductId = [string]$inactiveProduct.Data.id; Add-Cleanup "/api/products" $inactiveProductId
    [void](Invoke-Api PATCH "/api/products/$inactiveProductId/status" $admin.Token @{ isActive = $false })
    $r = Invoke-Api POST "/api/purchase-requests" $officer.Token (New-PrBody $inactiveProductId 1 "Inactive product")
    [void](Assert-Status "PR inactive product" 400 $r.StatusCode)

    $r = Invoke-Api PUT "/api/purchase-requests/$prId" $officer.Token (New-PrBody $productId 10 "Updated reason")
    $updatedPrItemId = [string](@($r.Data.items)[0].id)
    $updateValid = $r.Data.reason -eq "Updated reason" -and
        -not [string]::IsNullOrWhiteSpace($updatedPrItemId)
    if (Assert-Status "InventoryOfficer edits own Draft PR" 200 $r.StatusCode $updateValid) {
        $prItemId = $updatedPrItemId
    }
    $r = Invoke-Api POST "/api/purchase-requests/$prId/submit" $officer.Token $null
    [void](Assert-Status "Submit Draft PR" 200 $r.StatusCode ($r.Data.status -eq 2))
    $r = Invoke-Api PUT "/api/purchase-requests/$prId" $officer.Token (New-PrBody $productId 10 "Too late")
    [void](Assert-Status "Edit submitted PR rejected" 409 $r.StatusCode)
    $r = Invoke-Api POST "/api/purchase-requests/$prId/approve" $officer.Token $null
    [void](Assert-Status "InventoryOfficer cannot approve PR" 403 $r.StatusCode)
    $r = Invoke-Api POST "/api/purchase-requests/$prId/approve" $manager.Token $null
    $reviewed = $r.Data.status -eq 3 -and $null -ne $r.Data.reviewedByUserId -and $null -ne $r.Data.reviewedAt
    [void](Assert-Status "Own-branch BranchManager approves PR" 200 $r.StatusCode $reviewed)
    $r = Invoke-Api POST "/api/purchase-requests/$prId/submit" $admin.Token $null
    [void](Assert-Status "Approved to Submitted transition rejected" 409 $r.StatusCode)

    $rejectPr = Invoke-Api POST "/api/purchase-requests" $officer.Token (New-PrBody $productId 2 "Reject workflow")
    $rejectPrId = [string]$rejectPr.Data.id
    [void](Invoke-Api POST "/api/purchase-requests/$rejectPrId/submit" $officer.Token $null)
    $r = Invoke-Api POST "/api/purchase-requests/$rejectPrId/reject" $manager.Token @{ reason = "Budget unavailable" }
    [void](Assert-Status "BranchManager rejects submitted PR" 200 $r.StatusCode ($r.Data.status -eq 4 -and $r.Data.rejectionReason -eq "Budget unavailable"))

    $otherBranch = Invoke-Api POST "/api/branches" $admin.Token @{ code = "PB-$suffix"; name = "Procurement Other Branch $suffix" }
    $otherBranchId = [string]$otherBranch.Data.id; Add-Cleanup "/api/branches" $otherBranchId
    $crossPr = Invoke-Api POST "/api/purchase-requests" $admin.Token @{
        branchId = $otherBranchId; reason = "Cross branch"; items = @(@{ productId = $productId; requestedQuantity = 1 })
    }
    $crossPrId = [string]$crossPr.Data.id
    [void](Invoke-Api POST "/api/purchase-requests/$crossPrId/submit" $admin.Token $null)
    $r = Invoke-Api POST "/api/purchase-requests/$crossPrId/approve" $manager.Token $null
    [void](Assert-Status "Cross-branch BranchManager cannot approve" 403 $r.StatusCode)
    [void](Invoke-Api POST "/api/purchase-requests/$crossPrId/approve" $admin.Token $null)

    $r = Invoke-Api GET "/api/purchase-orders" $null $null
    [void](Assert-Status "GET purchase orders without JWT" 401 $r.StatusCode)
    $r = Invoke-Api POST "/api/purchase-orders" $officer.Token @{
        purchaseRequestId = $prId; supplierId = $supplierId
        items = @(@{ purchaseRequestItemId = $prItemId; orderedQuantity = 1; unitPrice = 100 })
    }
    [void](Assert-Status "InventoryOfficer cannot create PO" 403 $r.StatusCode)

    $inactiveSupplier = Invoke-Api POST "/api/suppliers" $admin.Token @{ code = "IS-$suffix"; name = "Inactive Supplier $suffix" }
    $inactiveSupplierId = [string]$inactiveSupplier.Data.id; Add-Cleanup "/api/suppliers" $inactiveSupplierId
    [void](Invoke-Api PATCH "/api/suppliers/$inactiveSupplierId/status" $admin.Token @{ isActive = $false })
    $basePoItem = @{ purchaseRequestItemId = $prItemId; orderedQuantity = 1; unitPrice = 100 }
    $r = Invoke-Api POST "/api/purchase-orders" $procurement.Token @{
        purchaseRequestId = $prId; supplierId = $inactiveSupplierId; items = @($basePoItem)
    }
    [void](Assert-Status "PO inactive supplier" 400 $r.StatusCode)
    $r = Invoke-Api POST "/api/purchase-orders" $procurement.Token @{
        purchaseRequestId = $prId; supplierId = [Guid]::NewGuid(); items = @($basePoItem)
    }
    [void](Assert-Status "PO unknown supplier" 404 $r.StatusCode)
    $otherItemId = [string](@($crossPr.Data.items)[0].id)
    $r = Invoke-Api POST "/api/purchase-orders" $procurement.Token @{
        purchaseRequestId = $prId; supplierId = $supplierId
        items = @(@{ purchaseRequestItemId = $otherItemId; orderedQuantity = 1; unitPrice = 1 })
    }
    [void](Assert-Status "PO item from another PR" 400 $r.StatusCode)
    $r = Invoke-Api POST "/api/purchase-orders" $procurement.Token @{
        purchaseRequestId = $prId; supplierId = $supplierId
        items = @(@{ purchaseRequestItemId = $prItemId; orderedQuantity = 0; unitPrice = 1 })
    }
    [void](Assert-Status "PO ordered quantity zero" 400 $r.StatusCode)
    $r = Invoke-Api POST "/api/purchase-orders" $procurement.Token @{
        purchaseRequestId = $prId; supplierId = $supplierId
        items = @(@{ purchaseRequestItemId = $prItemId; orderedQuantity = 1; unitPrice = -1 })
    }
    [void](Assert-Status "PO negative unit price" 400 $r.StatusCode)

    $po1 = Invoke-Api POST "/api/purchase-orders" $procurement.Token @{
        purchaseRequestId = $prId; supplierId = $supplierId; notes = "Partial allocation"
        items = @(@{ purchaseRequestItemId = $prItemId; orderedQuantity = 6; unitPrice = 100 })
    }
    $po1Id = [string]$po1.Data.id
    $prAfterPartial = Invoke-Api GET "/api/purchase-requests/$prId" $procurement.Token $null
    [void](Assert-Status "Partial allocation keeps PR Approved" 201 $po1.StatusCode ($prAfterPartial.Data.status -eq 3))
    if ([string]::IsNullOrWhiteSpace($po1Id)) {
        Write-Result $false "Dependent PO workflow blocked because partial PO creation returned no ID"
        Finish
    }

    $po2 = Invoke-Api POST "/api/purchase-orders" $procurement.Token @{
        purchaseRequestId = $prId; supplierId = $supplierId
        items = @(@{ purchaseRequestItemId = $prItemId; orderedQuantity = 4; unitPrice = 110 })
    }
    $po2Id = [string]$po2.Data.id
    $prConverted = Invoke-Api GET "/api/purchase-requests/$prId" $procurement.Token $null
    [void](Assert-Status "Remaining allocation converts PR" 201 $po2.StatusCode ($prConverted.Data.status -eq 6))
    if ([string]::IsNullOrWhiteSpace($po2Id)) {
        Write-Result $false "Dependent allocation workflow blocked because second PO creation returned no ID"
        Finish
    }

    $r = Invoke-Api POST "/api/purchase-orders" $procurement.Token @{
        purchaseRequestId = $prId; supplierId = $supplierId
        items = @(@{ purchaseRequestItemId = $prItemId; orderedQuantity = 1; unitPrice = 1 })
    }
    [void](Assert-Status "Over-allocation rejected" 409 $r.StatusCode)

    $r = Invoke-Api POST "/api/purchase-orders/$po1Id/submit" $procurement.Token $null
    [void](Assert-Status "PO Draft to Submitted" 200 $r.StatusCode ($r.Data.status -eq 2))
    $r = Invoke-Api POST "/api/purchase-orders/$po1Id/approve" $procurement.Token $null
    [void](Assert-Status "ProcurementOfficer cannot approve PO" 403 $r.StatusCode)
    $r = Invoke-Api POST "/api/purchase-orders/$po1Id/approve" $admin.Token $null
    [void](Assert-Status "SuperAdmin approves PO" 200 $r.StatusCode ($r.Data.status -eq 3))
    $r = Invoke-Api POST "/api/purchase-orders/$po1Id/send" $procurement.Token $null
    [void](Assert-Status "Approved PO sent to supplier" 200 $r.StatusCode ($r.Data.status -eq 4))
    $stockBefore = (Invoke-Api GET "/api/inventory/$inventoryId" $admin.Token $null).Data.quantityOnHand
    $r = Invoke-Api POST "/api/purchase-orders/$po1Id/confirm" $procurement.Token $null
    $stockAfter = (Invoke-Api GET "/api/inventory/$inventoryId" $admin.Token $null).Data.quantityOnHand
    [void](Assert-Status "Supplier confirms PO without stock change" 200 $r.StatusCode ($r.Data.status -eq 5 -and $stockAfter -eq $stockBefore))
    $r = Invoke-Api GET "/api/purchase-orders/$po1Id" $officer.Token $null
    [void](Assert-Status "InventoryOfficer reads own-branch PO" 200 $r.StatusCode)

    $crossPo = Invoke-Api POST "/api/purchase-orders" $procurement.Token @{
        purchaseRequestId = $crossPrId; supplierId = $supplierId
        items = @(@{ purchaseRequestItemId = $otherItemId; orderedQuantity = 1; unitPrice = 1 })
    }
    $crossPoId = [string]$crossPo.Data.id
    if ([string]::IsNullOrWhiteSpace($crossPoId)) {
        Write-Result $false "Cross-branch PO read test blocked because setup PO creation failed"
    }
    else {
        $r = Invoke-Api GET "/api/purchase-orders/$crossPoId" $officer.Token $null
        [void](Assert-Status "InventoryOfficer cannot read other-branch PO" 403 $r.StatusCode)
    }

    $r = Invoke-Api POST "/api/purchase-orders/$po2Id/cancel" $procurement.Token $null
    $prReleased = Invoke-Api GET "/api/purchase-requests/$prId" $procurement.Token $null
    [void](Assert-Status "Cancelling allocated PO releases quantity and reopens PR" 200 $r.StatusCode ($r.Data.status -eq 8 -and $prReleased.Data.status -eq 3))
    $r = Invoke-Api POST "/api/purchase-orders/$po1Id/confirm" $procurement.Token $null
    [void](Assert-Status "Invalid PO transition rejected" 409 $r.StatusCode)
}
finally {
    if ($script:AdminToken) {
        foreach ($target in $script:Cleanup) {
            $r = Invoke-Api PATCH "$($target.Path)/$($target.Id)/status" $script:AdminToken @{ isActive = $false }
            [void](Assert-Status "Cleanup deactivate $($target.Path)/$($target.Id)" 200 $r.StatusCode ($r.Data.isActive -eq $false))
        }
    }
}

Finish
