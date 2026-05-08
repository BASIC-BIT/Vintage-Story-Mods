using thebasics.ModSystems.RpCharacters.Models;

namespace thebasics.ModSystems.RpCharacters;

public class RpCharacterProjectionParticipant : IRpCharacterSwitchParticipant
{
    private readonly RpCharacterService _service;

    public RpCharacterProjectionParticipant(RpCharacterService service)
    {
        _service = service;
    }

    public string Code => "thebasics:projection";

    public int Order => 0;

    public RpCharacterOperationResult Validate(RpCharacterSwitchContext context)
    {
        return RpCharacterOperationResult.Ok(string.Empty);
    }

    public void Capture(RpCharacterSwitchContext context, RpCharacterRecord record)
    {
        record.Projection = _service.CaptureProjection(context.Player);
    }

    public void Restore(RpCharacterSwitchContext context, RpCharacterRecord record)
    {
        _service.RestoreProjection(context.Player, record.Projection);
    }
}
