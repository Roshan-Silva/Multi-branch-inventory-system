namespace MultiBranchInventory.Application.GoodsReceivedNotes.DTOs;

public class GoodsReceivedNoteOperationResult
{
    public bool IsSuccess { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public GoodsReceivedNoteResponse? GoodsReceivedNote { get; private set; }
    public static GoodsReceivedNoteOperationResult Success(GoodsReceivedNoteResponse note) =>
        new() { IsSuccess = true, GoodsReceivedNote = note };
    public static GoodsReceivedNoteOperationResult Failure(string code, string message) =>
        new() { ErrorCode = code, ErrorMessage = message };
}
