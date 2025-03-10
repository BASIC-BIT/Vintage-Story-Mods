using System.Text.RegularExpressions;
using HarmonyLib;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class AutoCapitalizationTransformer : MessageTransformerBase
{
    private static readonly Regex AutoCapitalizationRegex = new Regex(@"^([\s+|]*)(.)(.*)$");
    
    public AutoCapitalizationTransformer(RPProximityChatSystem chatSystem) : base(chatSystem)
    {
    }
    
    public override bool ShouldTransform(MessageContext context)
    {
        return !context.HasFlag(MessageContext.IS_OOC) && !context.HasFlag(MessageContext.IS_EMOTE);
    }
    
    public override MessageContext Transform(MessageContext context)
    {
        context.Message = AutoCapitalizationRegex.Replace(context.Message, match =>
        {
            var firstLetter = match.Groups[2].Value;
            return $"{match.Groups[1].Value}{firstLetter.ToUpper()}{match.Groups[3].Value}";
        });
        
        return context;
    }
} 