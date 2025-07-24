using System.Text.RegularExpressions;
using thebasics.Configs;
using thebasics.ModSystems.ProximityChat.Models;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class AccentTransformer : MessageTransformerBase
{   
    public AccentTransformer(RPProximityChatSystem chatSystem) : base(chatSystem)
    {
    }
    
    public override bool ShouldTransform(MessageContext context)
    {
        return true;
    }
    
    public override MessageContext Transform(MessageContext context)
    {
        // Handle italic formatting
        var delimiters = _config.ChatDelimiters;
        if (!string.IsNullOrEmpty(delimiters.Italic.Start))
        {
            var italicStart = Regex.Escape(delimiters.Italic.Start);
            var italicEnd = string.IsNullOrEmpty(delimiters.Italic.End) ? italicStart : Regex.Escape(delimiters.Italic.End);
            
            // Replace paired delimiters
            context.Message = Regex.Replace(context.Message, $"{italicStart}(.*?){italicEnd}", "<i>$1</i>");
            // Replace unpaired delimiter at end of string
            context.Message = Regex.Replace(context.Message, $"{italicStart}(.*)$", "<i>$1</i>");
        }
        
        // Handle bold formatting
        if (!string.IsNullOrEmpty(delimiters.Bold.Start))
        {
            var boldStart = Regex.Escape(delimiters.Bold.Start);
            var boldEnd = string.IsNullOrEmpty(delimiters.Bold.End) ? boldStart : Regex.Escape(delimiters.Bold.End);
            
            // Replace paired delimiters
            context.Message = Regex.Replace(context.Message, $"{boldStart}(.*?){boldEnd}", "<strong>$1</strong>");
            // Replace unpaired delimiter at end of string
            context.Message = Regex.Replace(context.Message, $"{boldStart}(.*)$", "<strong>$1</strong>");
        }
        
        return context;
    }
} 