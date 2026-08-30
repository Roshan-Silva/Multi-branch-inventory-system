namespace MultiBranchInventory.Application.GoodsReceivedNotes.DTOs;

public class GoodsReceivedNoteQueryResult
{
    public bool IsSuccess { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public IReadOnlyList<GoodsReceivedNoteResponse> GoodsReceivedNotes { get; private set; }
        = Array.Empty<GoodsReceivedNoteResponse>();
    public static GoodsReceivedNoteQueryResult Success(IReadOnlyList<GoodsReceivedNoteResponse> notes) =>
        new() { IsSuccess = true, GoodsReceivedNotes = notes };
    public static GoodsReceivedNoteQueryResult Failure(string code, string message) =>
        new() { ErrorCode = code, ErrorMessage = message };
}
