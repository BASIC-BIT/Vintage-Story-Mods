using thebasics.Extensions;
using thebasics.ModSystems.CharacterSheets;
using thebasics.ModSystems.ProximityChat.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.ProximityChat.Transformers;

// Require nicknames if we're doing RP chat
public class NicknameRequirementTransformer : MessageTransformerBase
{
    public NicknameRequirementTransformer(RPProximityChatSystem chatSystem) : base(chatSystem)
    {
    }

    public override bool ShouldTransform(MessageContext context)
    {
        if (!context.HasFlag(MessageContext.IS_ROLEPLAY))
        {
            return false;
        }

        if (_config.EnableCharacterSheets)
        {
            return _config.CharacterSheetRequireRequiredFieldsForRoleplay && CharacterSheetSystem.GetMissingRequiredFieldLabels(context.SendingPlayer, _config).Length > 0;
        }

        return !_config.DisableNicknames && !context.SendingPlayer.HasNickname();
    }

    public override MessageContext Transform(MessageContext context)
    {
        var missingFields = CharacterSheetSystem.GetMissingRequiredFieldLabels(context.SendingPlayer, _config);
        if (_config.EnableCharacterSheets && missingFields.Length > 0)
        {
            context.SendingPlayer.SendMessage(
                _chatSystem.ProximityChatId,
                Lang.Get("thebasics:charsheet-required-warning", string.Join(", ", missingFields)),
                EnumChatType.CommandError
            );

            context.State = MessageContextState.STOP;
            return context;
        }

        // Send nickname requirement warning directly to the player
        context.SendingPlayer.SendMessage(
            _chatSystem.ProximityChatId,
            Lang.Get("thebasics:chat-nickrequirement-warning"),
            EnumChatType.CommandError
        );

        // Stop processing this message
        context.State = MessageContextState.STOP;

        return context;
    }
}
