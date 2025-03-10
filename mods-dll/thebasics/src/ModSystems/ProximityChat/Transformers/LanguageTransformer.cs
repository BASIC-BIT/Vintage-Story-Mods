using thebasics.ModSystems.ProximityChat.Models;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class LanguageTransformer : MessageTransformerBase
{
    private readonly LanguageSystem _languageSystem;
    
    public LanguageTransformer(LanguageSystem languageSystem, RPProximityChatSystem chatSystem) : base(chatSystem)
    {
        _languageSystem = languageSystem;
    }
    
    public override bool ShouldTransform(MessageContext context)
    {
        return context.HasFlag(MessageContext.IS_ROLEPLAY) && !context.HasFlag(MessageContext.IS_EMOTE) && !context.HasFlag(MessageContext.IS_ENVIRONMENTAL) && !context.HasFlag(MessageContext.IS_OOC);
    }
    
    public override MessageContext Transform(MessageContext context)
    {
        var content = context.Message;
        _languageSystem.ProcessMessage(context.ReceivingPlayer, ref content, context.GetMetadata<Language>(MessageContext.LANGUAGE));
        
        context.Message = content;
        
        return context;
    }
} 