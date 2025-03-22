using System.Text.RegularExpressions;
using HarmonyLib;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class FormatTransformer : MessageTransformerBase
{
    private static readonly Regex AutoCapitalizationRegex = new Regex(@"^([\s+|]*)(.)(.*)$");
    private static readonly Regex AutoPunctuationRegex = new Regex(@"^(.*?)(.)([\s+|]*)$");
    
    public FormatTransformer(RPProximityChatSystem chatSystem) : base(chatSystem)
    {
    }
    
    public override bool ShouldTransform(MessageContext context)
    {
        return !context.HasFlag(MessageContext.IS_OOC);
    }
    
    public override MessageContext Transform(MessageContext context)
    {
        AutoCapitalize(context);
        AddEndPunctuation(context);
        ProcessAccents(context);
        
        return context;
    }

    private void AutoCapitalize(MessageContext context) {
        context.Message = AutoCapitalizationRegex.Replace(context.Message, match =>
        {
            var firstLetter = match.Groups[2].Value;
            return $"{match.Groups[1].Value}{firstLetter.ToUpper()}{match.Groups[3].Value}";
        });
    }
    
    private void AddEndPunctuation(MessageContext context) {
        
        if (ChatHelper.DoesMessageNeedPunctuation(context.Message))
        {
            context.Message = AutoPunctuationRegex.Replace(context.Message, match =>
            {
                var possiblePunctuation = match.Groups[2].Value[0];
                return $"{match.Groups[1].Value}{possiblePunctuation}{(ChatHelper.IsPunctuation(possiblePunctuation) ? "" : ".")}{match.Groups[3].Value}";
            });
        }
    }

    private void ProcessAccents(MessageContext context)
    {
        context.Message = Regex.Replace(context.Message, @"\|(.*?)\|", "<i>$1</i>");
        context.Message = Regex.Replace(context.Message, @"\|(.*)$", "<i>$1</i>");
        context.Message = Regex.Replace(context.Message, @"\+(.*?)\+", "<strong>$1</strong>");
        context.Message = Regex.Replace(context.Message, @"\+(.*)$", "<strong>$1</strong>");
    }
} 