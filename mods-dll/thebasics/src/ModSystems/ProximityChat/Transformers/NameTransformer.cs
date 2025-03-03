using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;
using Vintagestory.API.Server;
namespace thebasics.src.ModSystems.ProximityChat.Transformers;

// Update name formatting (real name or nickname, bold, colorized) for use in later transformers
public class NameTransformer : IMessageTransformer
{
    private readonly ModConfig _config;
    public NameTransformer(ModConfig config)
    {
        _config = config;
    }
    public MessageContext Transform(MessageContext context)
    {
        context.Metadata["formattedName"] = GetFormattedName(context.SendingPlayer, false, _config);
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