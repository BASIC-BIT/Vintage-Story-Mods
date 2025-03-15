using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;
using Vintagestory.API.Server;
namespace thebasics.ModSystems.ProximityChat.Transformers;

// Update name formatting (real name or nickname, bold, colorized) for use in later transformers
public class NameTransformer : MessageTransformerBase
{
    
    public NameTransformer(RPProximityChatSystem chatSystem) : base(chatSystem)
    {
    }

    public override bool ShouldTransform(MessageContext context)
    {
        return true;
    }

    public override MessageContext Transform(MessageContext context)
    {
        bool isIC = context.HasFlag(MessageContext.IS_ROLEPLAY) ||
            context.HasFlag(MessageContext.IS_EMOTE) ||
            (context.HasFlag(MessageContext.IS_OOC) && _config.UseNicknameInOOC) ||
            (context.HasFlag(MessageContext.IS_GLOBAL_OOC) && _config.UseNicknameInGlobalOOC);
        
        context.SetMetadata(MessageContext.FORMATTED_NAME, GetFormattedName(context.SendingPlayer, isIC, _config));
        return context;
    }
    
    public string GetFormattedName(IServerPlayer player, bool isIC, ModConfig config)
    {
        string name = isIC ? player.GetNickname() : player.PlayerName;
        
        string color = player.GetNicknameColor();
        bool applyColor = !string.IsNullOrEmpty(color) && (isIC ? config.ApplyColorsToNicknames : config.ApplyColorsToPlayerNames);
        
        if(config.BoldNicknames){
            name = ChatHelper.Strong(name);
        }

        if(applyColor){
            name = ChatHelper.Color(name, color);
        }
        return name;
    }
}