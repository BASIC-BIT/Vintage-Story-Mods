using System.Text.RegularExpressions;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class AutoPunctuationTransformer : MessageTransformerBase
{
    private static readonly Regex AutoPunctuationRegex = new Regex(@"^(.*?)(.)([\s+|]*)$");
    
    public AutoPunctuationTransformer(RPProximityChatSystem chatSystem) : base(chatSystem)
    {
    }
    
    public override bool ShouldTransform(MessageContext context)
    {
        return !context.HasFlag(MessageContext.IS_OOC) && context.HasFlag(MessageContext.IS_ROLEPLAY) && ChatHelper.DoesMessageNeedPunctuation(context.Message);
    }
    
    public override MessageContext Transform(MessageContext context)
    {
        context.Message = AutoPunctuationRegex.Replace(context.Message, match =>
        {
            var possiblePunctuation = match.Groups[2].Value[0];
            var punctuation = _chatSystem.Config.ProximityChatModePunctuation[context.GetMetadata(MessageContext.CHAT_MODE, context.SendingPlayer.GetChatMode())];
            if(context.HasFlag(MessageContext.IS_EMOTE) || context.HasFlag(MessageContext.IS_ENVIRONMENTAL)){
                punctuation = "."; // Emotes and environmental messages don't need punctuation based on chat mode
            }
            return $"{match.Groups[1].Value}{possiblePunctuation}{(ChatHelper.IsPunctuation(possiblePunctuation) ? "" : punctuation)}{match.Groups[3].Value}";
        });
        
        return context;
    }
} 