namespace MultiBranchInventory.Domain.Enums;

public enum InventoryTransactionType
{
    PurchaseReceipt = 1,
    TransferIn = 2,
    TransferOut = 3,
    AdjustmentIncrease = 4,
    AdjustmentDecrease = 5
}