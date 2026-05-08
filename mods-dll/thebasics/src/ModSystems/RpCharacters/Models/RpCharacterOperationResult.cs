namespace thebasics.ModSystems.RpCharacters.Models;

public class RpCharacterOperationResult
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public RpCharacterRecord Character { get; set; }

    public static RpCharacterOperationResult Ok(string message, RpCharacterRecord character = null)
    {
        return new RpCharacterOperationResult
        {
            Success = true,
            Message = message,
            Character = character
        };
    }

    public static RpCharacterOperationResult Error(string message)
    {
        return new RpCharacterOperationResult
        {
            Success = false,
            Message = message
        };
    }
}
