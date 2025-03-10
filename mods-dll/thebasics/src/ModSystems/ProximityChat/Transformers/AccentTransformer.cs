using System.Text.RegularExpressions;
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
        context.Message = Regex.Replace(context.Message, @"\|(.*?)\|", "<i>$1</i>");
        context.Message = Regex.Replace(context.Message, @"\|(.*)$", "<i>$1</i>");
        context.Message = Regex.Replace(context.Message, @"\+(.*?)\+", "<strong>$1</strong>");
        context.Message = Regex.Replace(context.Message, @"\+(.*)$", "<strong>$1</strong>");
        
        return context;
    }
} 