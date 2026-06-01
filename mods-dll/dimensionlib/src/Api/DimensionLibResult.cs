namespace DimensionLib.Api;

/// <summary>
/// Non-throwing operation result for public DimensionLib APIs.
/// </summary>
public sealed class DimensionLibResult
{
    private DimensionLibResult(bool success, string message, string errorCode)
    {
        Success = success;
        Message = message ?? string.Empty;
        ErrorCode = errorCode ?? string.Empty;
    }

    public bool Success { get; }

    public string Message { get; }

    public string ErrorCode { get; }

    public static DimensionLibResult Ok(string message = "")
    {
        return new DimensionLibResult(true, message, string.Empty);
    }

    public static DimensionLibResult Fail(string message, string errorCode = "")
    {
        return new DimensionLibResult(false, message, errorCode);
    }
}
