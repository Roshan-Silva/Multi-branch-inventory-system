param([string]$BaseUrl = "http://localhost:5296")

$ErrorActionPreference = "Stop"
$script:Passed = 0
$script:Failed = 0
$script:Cleanup = [System.Collections.Generic.List[object]]::new()

function Write-Result([bool]$Success, [string]$Message) {
    if ($Success) { $script:Passed++; Write-Host "[PASS] $Message" -ForegroundColor Green }
    else { $script:Failed++; Write-Host "[FAIL] $Message" -ForegroundColor Red }
}

function Assert-Status([string]$Name, [int]$Expected, [object]$Response, [bool]$Condition = $true) {
    if ($Response.StatusCode -ne $Expected) {
        Write-Result $false "$Name -> expected $Expected, got $($Response.StatusCode)"; return $false
    }
    Write-Result $Condition "$Name -> $Expected"; return $Condition
}

function Invoke-Api([string]$Method, [string]$Path, [string]$Token, [object]$Body) {
    $parameters = @{ Uri = "$($BaseUrl.TrimEnd('/'))$Path"; Method = $Method; UseBasicParsing = $true }
    if ($Token) { $parameters.Headers = @{ Authorization = "Bearer $Token" } }
    if ($null -ne $Body) {
        $parameters.ContentType = "application/json"
        $parameters.Body = $Body | ConvertTo-Json -Depth 12 -Compress
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

function Login([string]$Name, [string]$Email, [string]$Password) {
    $response = Invoke-Api POST "/api/auth/login" $null @{ email = $Email; password = $Password }
    if ($response.StatusCode -ne 200 -or -not $response.Data.accessToken) {
        Write-Result $false "Login as $Name"; return $null
    }
    return [pscustomobject]@{
        Token = [string]$response.Data.accessToken
        BranchId = [string]$response.Data.user.branchId
    }
}

function Add-Cleanup([string]$Path, [string]$Id) {
    if ($Id) { $script:Cleanup.Add([pscustomobject]@{ Path = $Path; Id = $Id }) }
}

function New-ConfirmedPo([string]$BranchId, [object[]]$Products, [string]$SupplierId) {
    $prItems = @($Products | ForEach-Object {
        @{ productId = $_.ProductId; requestedQuantity = $_.Quantity; notes = "GRN smoke" }
    })
    $pr = Invoke-Api POST "/api/purchase-requests" $admin.Token @{
        branchId = $BranchId; reason = "GRN smoke $suffix"; items = $prItems
    }
    if ($pr.StatusCode -ne 201) { throw "Could not create setup PR ($($pr.StatusCode))." }
    [void](Invoke-Api POST "/api/purchase-requests/$($pr.Data.id)/submit" $admin.Token $null)
    [void](Invoke-Api POST "/api/purchase-requests/$($pr.Data.id)/approve" $admin.Token $null)
    $poItems = @()
    for ($index = 0; $index -lt $Products.Count; $index++) {
        $poItems += @{
            purchaseRequestItemId = @($pr.Data.items)[$index].id
            orderedQuantity = $Products[$index].Quantity
            unitPrice = 10
        }
    }
    $po = Invoke-Api POST "/api/purchase-orders" $procurement.Token @{
        purchaseRequestId = $pr.Data.id; supplierId = $SupplierId; items = $poItems
    }
    if ($po.StatusCode -ne 201) { throw "Could not create setup PO ($($po.StatusCode))." }
    [void](Invoke-Api POST "/api/purchase-orders/$($po.Data.id)/submit" $procurement.Token $null)
    [void](Invoke-Api POST "/api/purchase-orders/$($po.Data.id)/approve" $admin.Token $null)
    [void](Invoke-Api POST "/api/purchase-orders/$($po.Data.id)/send" $procurement.Token $null)
    $confirmed = Invoke-Api POST "/api/purchase-orders/$($po.Data.id)/confirm" $procurement.Token $null
    if ($confirmed.StatusCode -ne 200) { throw "Could not confirm setup PO ($($confirmed.StatusCode))." }
    return $confirmed.Data
}

function New-GrnBody([string]$PoId, [object[]]$Items, [string]$Reference = "DN-SMOKE") {
    return @{ purchaseOrderId = $PoId; deliveryReference = $Reference; notes = "GRN smoke"; items = $Items }
}

function Stock([string]$InventoryId) {
    return [int](Invoke-Api GET "/api/inventory/$InventoryId" $admin.Token $null).Data.quantityOnHand
}

function Require-Environment {
    $names = @("MBI_ADMIN_EMAIL", "MBI_ADMIN_PASSWORD", "MBI_INVENTORY_EMAIL", "MBI_INVENTORY_PASSWORD",
        "MBI_MANAGER_EMAIL", "MBI_MANAGER_PASSWORD", "MBI_PROCUREMENT_EMAIL", "MBI_PROCUREMENT_PASSWORD")
    $missing = @($names | Where-Object { [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($_)) })
    if ($missing.Count) { throw "Missing required environment variable(s): $($missing -join ', ')" }
}

function Finish {
    Write-Host ""; Write-Host "Passed: $script:Passed"; Write-Host "Failed: $script:Failed"
    if ($script:Failed) { exit 1 }
}

Require-Environment
$admin = Login "SuperAdmin" $env:MBI_ADMIN_EMAIL $env:MBI_ADMIN_PASSWORD
$officer = Login "InventoryOfficer" $env:MBI_INVENTORY_EMAIL $env:MBI_INVENTORY_PASSWORD
$manager = Login "BranchManager" $env:MBI_MANAGER_EMAIL $env:MBI_MANAGER_PASSWORD
$procurement = Login "ProcurementOfficer" $env:MBI_PROCUREMENT_EMAIL $env:MBI_PROCUREMENT_PASSWORD
if (-not $admin -or -not $officer -or -not $manager -or -not $procurement) { Finish }

$suffix = [Guid]::NewGuid().ToString("N").Substring(0, 10)
try {
    if (-not $officer.BranchId -or $manager.BranchId -ne $officer.BranchId) {
        throw "InventoryOfficer and BranchManager must share a non-empty branchId."
    }

    $response = Invoke-Api GET "/api/goods-received-notes" $null $null
    [void](Assert-Status "1. GET GRNs without JWT" 401 $response)

    $category = Invoke-Api POST "/api/categories" $admin.Token @{ name = "GRN Category $suffix" }
    Add-Cleanup "/api/categories" ([string]$category.Data.id)
    $supplier = Invoke-Api POST "/api/suppliers" $admin.Token @{ code = "GS-$suffix"; name = "GRN Supplier $suffix" }
    Add-Cleanup "/api/suppliers" ([string]$supplier.Data.id)
    $product = Invoke-Api POST "/api/products" $admin.Token @{
        sku = "GRN-$suffix"; name = "GRN Product $suffix"; categoryId = $category.Data.id; unitPrice = 10
    }
    Add-Cleanup "/api/products" ([string]$product.Data.id)
    $inventory = Invoke-Api POST "/api/inventory" $admin.Token @{
        branchId = $officer.BranchId; productId = $product.Data.id; minimumStockLevel = 0; reorderLevel = 0
    }
    $baseline = Stock $inventory.Data.id
    $po = New-ConfirmedPo $officer.BranchId @([pscustomobject]@{ ProductId = $product.Data.id; Quantity = 10 }) $supplier.Data.id
    $poItemId = [string]@($po.items)[0].id

    $draft = Invoke-Api POST "/api/goods-received-notes" $officer.Token (New-GrnBody $po.id @(
        @{ purchaseOrderItemId = $poItemId; receivedQuantity = 6; notes = "first shipment" }))
    [void](Assert-Status "2. Officer creates own-branch Draft GRN" 201 $draft ($draft.Data.status -eq 1))
    $draftId = [string]$draft.Data.id
    $grnNumber = [string]$draft.Data.grnNumber
    [void](Assert-Status "3. Draft does not change stock" 200 (Invoke-Api GET "/api/inventory/$($inventory.Data.id)" $admin.Token $null) ((Stock $inventory.Data.id) -eq $baseline))
    $ledger = Invoke-Api GET "/api/inventory-transactions?inventoryId=$($inventory.Data.id)" $admin.Token $null
    $draftLedger = @($ledger.Data | Where-Object { $_.referenceNumber -eq $grnNumber })
    [void](Assert-Status "4. Draft creates no PurchaseReceipt ledger entry" 200 $ledger ($draftLedger.Count -eq 0))

    $zero = Invoke-Api POST "/api/goods-received-notes" $officer.Token (New-GrnBody $po.id @(
        @{ purchaseOrderItemId = $poItemId; receivedQuantity = 0 }))
    [void](Assert-Status "5. Zero quantity rejected" 400 $zero)
    $unknown = Invoke-Api POST "/api/goods-received-notes" $officer.Token (New-GrnBody ([Guid]::NewGuid()) @(
        @{ purchaseOrderItemId = $poItemId; receivedQuantity = 1 }))
    [void](Assert-Status "6. Unknown PO rejected" 404 $unknown)

    $invalidPoPr = Invoke-Api POST "/api/purchase-requests" $admin.Token @{
        branchId = $officer.BranchId; reason = "invalid state"; items = @(@{ productId = $product.Data.id; requestedQuantity = 1 })
    }
    [void](Invoke-Api POST "/api/purchase-requests/$($invalidPoPr.Data.id)/submit" $admin.Token $null)
    [void](Invoke-Api POST "/api/purchase-requests/$($invalidPoPr.Data.id)/approve" $admin.Token $null)
    $invalidPo = Invoke-Api POST "/api/purchase-orders" $procurement.Token @{
        purchaseRequestId = $invalidPoPr.Data.id; supplierId = $supplier.Data.id
        items = @(@{ purchaseRequestItemId = @($invalidPoPr.Data.items)[0].id; orderedQuantity = 1; unitPrice = 1 })
    }
    $invalidState = Invoke-Api POST "/api/goods-received-notes" $officer.Token (New-GrnBody $invalidPo.Data.id @(
        @{ purchaseOrderItemId = @($invalidPo.Data.items)[0].id; receivedQuantity = 1 }))
    [void](Assert-Status "7. Draft PO is not receivable" 409 $invalidState)
    $wrongItem = Invoke-Api POST "/api/goods-received-notes" $officer.Token (New-GrnBody $po.id @(
        @{ purchaseOrderItemId = @($invalidPo.Data.items)[0].id; receivedQuantity = 1 }))
    [void](Assert-Status "8. Item from another PO rejected" 400 $wrongItem)
    $duplicate = Invoke-Api POST "/api/goods-received-notes" $officer.Token (New-GrnBody $po.id @(
        @{ purchaseOrderItemId = $poItemId; receivedQuantity = 1 },
        @{ purchaseOrderItemId = $poItemId; receivedQuantity = 1 }))
    [void](Assert-Status "9. Duplicate PO item rejected" 400 $duplicate)

    $otherBranch = Invoke-Api POST "/api/branches" $admin.Token @{ code = "GB-$suffix"; name = "GRN Other Branch $suffix" }
    Add-Cleanup "/api/branches" ([string]$otherBranch.Data.id)
    $otherPo = New-ConfirmedPo $otherBranch.Data.id @([pscustomobject]@{ ProductId = $product.Data.id; Quantity = 2 }) $supplier.Data.id
    $crossCreate = Invoke-Api POST "/api/goods-received-notes" $officer.Token (New-GrnBody $otherPo.id @(
        @{ purchaseOrderItemId = @($otherPo.items)[0].id; receivedQuantity = 1 }))
    [void](Assert-Status "10. Officer cannot create for another branch" 403 $crossCreate)

    $confirmed = Invoke-Api POST "/api/goods-received-notes/$draftId/confirm" $officer.Token $null
    [void](Assert-Status "11. Confirm first partial GRN" 200 $confirmed ($confirmed.Data.status -eq 2))
    [void](Assert-Status "12. Inventory increases exactly by 6" 200 (Invoke-Api GET "/api/inventory/$($inventory.Data.id)" $admin.Token $null) ((Stock $inventory.Data.id) -eq $baseline + 6))
    $ledger = Invoke-Api GET "/api/inventory-transactions?inventoryId=$($inventory.Data.id)" $admin.Token $null
    $entry = @($ledger.Data | Where-Object { $_.referenceNumber -eq $grnNumber -and $_.type -eq 1 })[0]
    [void](Assert-Status "13. PurchaseReceipt ledger entry is exact" 200 $ledger ($entry.quantity -eq 6 -and $entry.quantityBefore -eq $baseline -and $entry.quantityAfter -eq $baseline + 6))
    [void](Assert-Status "14. GRN becomes Confirmed" 200 $confirmed ($confirmed.Data.status -eq 2))
    [void](Assert-Status "15. ConfirmedBy is populated" 200 $confirmed (-not [string]::IsNullOrWhiteSpace([string]$confirmed.Data.confirmedByUserId)))
    [void](Assert-Status "16. ConfirmedAt is populated" 200 $confirmed ($null -ne $confirmed.Data.confirmedAt))
    $poAfterFirst = Invoke-Api GET "/api/purchase-orders/$($po.id)" $admin.Token $null
    [void](Assert-Status "17. PO becomes PartiallyReceived" 200 $poAfterFirst ($poAfterFirst.Data.status -eq 6))
    [void](Assert-Status "18. Reconfirm is rejected" 409 (Invoke-Api POST "/api/goods-received-notes/$draftId/confirm" $officer.Token $null))

    $second = Invoke-Api POST "/api/goods-received-notes" $officer.Token (New-GrnBody $po.id @(
        @{ purchaseOrderItemId = $poItemId; receivedQuantity = 4 }) "DN-SECOND")
    [void](Assert-Status "19. Create remaining quantity GRN" 201 $second)
    $secondConfirmed = Invoke-Api POST "/api/goods-received-notes/$($second.Data.id)/confirm" $officer.Token $null
    [void](Assert-Status "20. Confirm remaining quantity GRN" 200 $secondConfirmed)
    [void](Assert-Status "21. Inventory increases exactly by 4" 200 (Invoke-Api GET "/api/inventory/$($inventory.Data.id)" $admin.Token $null) ((Stock $inventory.Data.id) -eq $baseline + 10))
    $ledger = Invoke-Api GET "/api/inventory-transactions?inventoryId=$($inventory.Data.id)" $admin.Token $null
    $secondEntry = @($ledger.Data | Where-Object { $_.referenceNumber -eq $second.Data.grnNumber })[0]
    [void](Assert-Status "22. Second ledger entry is exact" 200 $ledger ($secondEntry.quantity -eq 4 -and $secondEntry.quantityBefore -eq $baseline + 6 -and $secondEntry.quantityAfter -eq $baseline + 10))
    $completedPo = Invoke-Api GET "/api/purchase-orders/$($po.id)" $admin.Token $null
    [void](Assert-Status "23. PO becomes Completed" 200 $completedPo ($completedPo.Data.status -eq 7))
    $afterCompleted = Invoke-Api POST "/api/goods-received-notes" $officer.Token (New-GrnBody $po.id @(
        @{ purchaseOrderItemId = $poItemId; receivedQuantity = 1 }))
    [void](Assert-Status "24. Completed PO rejects another GRN" 409 $afterCompleted)

    $overPo = New-ConfirmedPo $officer.BranchId @([pscustomobject]@{ ProductId = $product.Data.id; Quantity = 3 }) $supplier.Data.id
    $over = Invoke-Api POST "/api/goods-received-notes" $officer.Token (New-GrnBody $overPo.id @(
        @{ purchaseOrderItemId = @($overPo.items)[0].id; receivedQuantity = 4 }))
    $stockBeforeOver = Stock $inventory.Data.id
    [void](Assert-Status "25. Over-receiving rejected" 409 $over)
    [void](Assert-Status "26. Failed over-receive leaves stock unchanged" 200 (Invoke-Api GET "/api/inventory/$($inventory.Data.id)" $admin.Token $null) ((Stock $inventory.Data.id) -eq $stockBeforeOver))

    $cancelDraft = Invoke-Api POST "/api/goods-received-notes" $officer.Token (New-GrnBody $overPo.id @(
        @{ purchaseOrderItemId = @($overPo.items)[0].id; receivedQuantity = 1 }))
    $cancelled = Invoke-Api POST "/api/goods-received-notes/$($cancelDraft.Data.id)/cancel" $officer.Token $null
    [void](Assert-Status "27. Draft GRN can be cancelled" 200 $cancelled ($cancelled.Data.status -eq 3))
    [void](Assert-Status "28. Cancellation leaves stock unchanged" 200 (Invoke-Api GET "/api/inventory/$($inventory.Data.id)" $admin.Token $null) ((Stock $inventory.Data.id) -eq $stockBeforeOver))
    [void](Assert-Status "29. Confirmed GRN cannot be cancelled" 409 (Invoke-Api POST "/api/goods-received-notes/$draftId/cancel" $officer.Token $null))
    [void](Assert-Status "30. Manager reads own-branch GRN" 200 (Invoke-Api GET "/api/goods-received-notes/$draftId" $manager.Token $null))

    $otherDraft = Invoke-Api POST "/api/goods-received-notes" $admin.Token (New-GrnBody $otherPo.id @(
        @{ purchaseOrderItemId = @($otherPo.items)[0].id; receivedQuantity = 1 }))
    [void](Assert-Status "31. Manager cannot read other-branch GRN" 403 (Invoke-Api GET "/api/goods-received-notes/$($otherDraft.Data.id)" $manager.Token $null))
    [void](Assert-Status "32. Procurement can read GRNs" 200 (Invoke-Api GET "/api/goods-received-notes/$draftId" $procurement.Token $null))
    [void](Assert-Status "33. Procurement cannot confirm GRN" 403 (Invoke-Api POST "/api/goods-received-notes/$($otherDraft.Data.id)/confirm" $procurement.Token $null))
    [void](Assert-Status "34. Officer cannot confirm other-branch GRN" 403 (Invoke-Api POST "/api/goods-received-notes/$($otherDraft.Data.id)/confirm" $officer.Token $null))
    [void](Assert-Status "35. Missing Inventory blocks confirmation" 409 (Invoke-Api POST "/api/goods-received-notes/$($otherDraft.Data.id)/confirm" $admin.Token $null))

    $product2 = Invoke-Api POST "/api/products" $admin.Token @{
        sku = "GR2-$suffix"; name = "GRN Product Two $suffix"; categoryId = $category.Data.id; unitPrice = 10
    }
    Add-Cleanup "/api/products" ([string]$product2.Data.id)
    $multiPo = New-ConfirmedPo $officer.BranchId @(
        [pscustomobject]@{ ProductId = $product.Data.id; Quantity = 2 },
        [pscustomobject]@{ ProductId = $product2.Data.id; Quantity = 2 }) $supplier.Data.id
    $multiDraft = Invoke-Api POST "/api/goods-received-notes" $officer.Token (New-GrnBody $multiPo.id @(
        @{ purchaseOrderItemId = @($multiPo.items)[0].id; receivedQuantity = 1 },
        @{ purchaseOrderItemId = @($multiPo.items)[1].id; receivedQuantity = 1 }))
    $beforeAtomic = Stock $inventory.Data.id
    $atomicFail = Invoke-Api POST "/api/goods-received-notes/$($multiDraft.Data.id)/confirm" $officer.Token $null
    [void](Assert-Status "36. Multi-item missing inventory is atomic" 409 $atomicFail ((Stock $inventory.Data.id) -eq $beforeAtomic))

    $inventory2 = Invoke-Api POST "/api/inventory" $admin.Token @{
        branchId = $officer.BranchId; productId = $product2.Data.id; minimumStockLevel = 0; reorderLevel = 0
    }
    $multiConfirmed = Invoke-Api POST "/api/goods-received-notes/$($multiDraft.Data.id)/confirm" $officer.Token $null
    $ledgerA = Invoke-Api GET "/api/inventory-transactions?inventoryId=$($inventory.Data.id)" $admin.Token $null
    $ledgerB = Invoke-Api GET "/api/inventory-transactions?inventoryId=$($inventory2.Data.id)" $admin.Token $null
    $multiCount = @($ledgerA.Data | Where-Object referenceNumber -eq $multiDraft.Data.grnNumber).Count +
        @($ledgerB.Data | Where-Object referenceNumber -eq $multiDraft.Data.grnNumber).Count
    [void](Assert-Status "37. Multi-item confirmation creates two ledger entries" 200 $multiConfirmed ($multiCount -eq 2))
    $procurementCheck = Invoke-Api GET "/api/purchase-orders/$($multiPo.id)" $procurement.Token $null
    [void](Assert-Status "38. Existing procurement read workflow still functions" 200 $procurementCheck)
}
finally {
    for ($index = $script:Cleanup.Count - 1; $index -ge 0; $index--) {
        $target = $script:Cleanup[$index]
        [void](Invoke-Api PATCH "$($target.Path)/$($target.Id)/status" $admin.Token @{ isActive = $false })
    }
}

Finish
