namespace Ready4Balfolk.Domain.Services.Editor;

public sealed record EditorActionResult(bool Success, string? ErrorMessage = null)
{
    public static EditorActionResult Ok() => new(true);
    public static EditorActionResult Error(string message) => new(false, message);
}
