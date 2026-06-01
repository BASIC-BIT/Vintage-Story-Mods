namespace DimensionLib.Api;

/// <summary>
/// Non-throwing operation result that carries a value on success.
/// </summary>
public sealed class DimensionLibResult<T>
{
    private DimensionLibResult(bool success, T value, string message, string errorCode)
    {
        Success = success;
        Value = value;
        Message = message ?? string.Empty;
        ErrorCode = errorCode ?? string.Empty;
    }

    public bool Success { get; }

    public T Value { get; }

    public string Message { get; }

    public string ErrorCode { get; }

    public static DimensionLibResult<T> Ok(T value, string message = "")
    {
        return new DimensionLibResult<T>(true, value, message, string.Empty);
    }

    public static DimensionLibResult<T> Fail(string message, string errorCode = "")
    {
        return new DimensionLibResult<T>(false, default, message, errorCode);
    }
}
