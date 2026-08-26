namespace MultiBranchInventory.Domain.Enums;

public enum PurchaseOrderStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    SentToSupplier = 4,
    Confirmed = 5,
    PartiallyReceived = 6,
    Completed = 7,
    Cancelled = 8
}