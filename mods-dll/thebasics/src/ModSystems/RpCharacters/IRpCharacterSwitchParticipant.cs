using thebasics.ModSystems.RpCharacters.Models;

namespace thebasics.ModSystems.RpCharacters;

public interface IRpCharacterSwitchParticipant
{
    string Code { get; }

    int Order { get; }

    RpCharacterOperationResult Validate(RpCharacterSwitchContext context);

    void Capture(RpCharacterSwitchContext context, RpCharacterRecord record);

    void Restore(RpCharacterSwitchContext context, RpCharacterRecord record);
}
