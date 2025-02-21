using thebasics.ModSystems.ProximityChat.Models;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class LanguageTransformer : IMessageTransformer
{
    private readonly LanguageSystem _languageSystem;
    
    public LanguageTransformer(LanguageSystem languageSystem)
    {
        _languageSystem = languageSystem;
    }
    
    public MessageContext Transform(MessageContext context)
    {
        var content = context.Message;
        var lang = _languageSystem.GetSpeakingLanguage(context.SendingPlayer, context.GroupId, ref content);
        _languageSystem.ProcessMessage(context.ReceivingPlayer, ref content, lang);
        
        context.Message = content;
        context.Metadata["language"] = lang;
        
        return context;
    }
} 