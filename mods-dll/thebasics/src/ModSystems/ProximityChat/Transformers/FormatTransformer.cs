using System.Text.RegularExpressions;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class FormatTransformer : IMessageTransformer
{
    private readonly RPProximityChatSystem _chatSystem;
    
    public FormatTransformer(RPProximityChatSystem chatSystem)
    {
        _chatSystem = chatSystem;
    }
    
    public MessageContext Transform(MessageContext context)
    {
        var content = context.Message;
        
        // Handle auto-capitalization
        var autoCapitalizationRegex = new Regex(@"^([\s+|]*)(.)(.*)$");
        content = autoCapitalizationRegex.Replace(content, match =>
        {
            var firstLetter = match.Groups[2].Value;
            return $"{match.Groups[1].Value}{firstLetter.ToUpper()}{match.Groups[3].Value}";
        });
        
        // Only add auto-punctuation for emotes and environmental messages
        // Regular chat messages will get punctuation from ChatModeTransformer
        bool isRegularChat = !context.Metadata.ContainsKey("isEmote") && !context.Metadata.ContainsKey("isEnvironmental");
        
        if (!isRegularChat && ChatHelper.DoesMessageNeedPunctuation(content))
        {
            var autoPunctuationRegex = new Regex(@"^(.*?)(.)([\s+|]*)$");
            content = autoPunctuationRegex.Replace(content, match =>
            {
                var possiblePunctuation = match.Groups[2].Value[0];
                return $"{match.Groups[1].Value}{possiblePunctuation}{(ChatHelper.IsPunctuation(possiblePunctuation) ? "" : ".")}{match.Groups[3].Value}";
            });
        }
        
        // Process accents and formatting
        content = ProcessAccents(content);
        
        context.Message = content;
        return context;
    }

    private string ProcessAccents(string message)
    {
        message = Regex.Replace(message, @"\|(.*?)\|", "<i>$1</i>");
        message = Regex.Replace(message, @"\|(.*)$", "<i>$1</i>");
        message = Regex.Replace(message, @"\+(.*?)\+", "<strong>$1</strong>");
        message = Regex.Replace(message, @"\+(.*)$", "<strong>$1</strong>");
        return message;
    }
} 