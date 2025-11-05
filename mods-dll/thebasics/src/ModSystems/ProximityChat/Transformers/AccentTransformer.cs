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
        var delimiters = _config.ChatDelimiters;
        var processedMessage = context.Message;
        var isSpeech = context.HasFlag(MessageContext.IS_SPEECH);
        string speechText = null;

        if (isSpeech && !context.TryGetSpeechText(out speechText))
        {
            speechText = processedMessage;
        }

        // Handle italic formatting
        if (!string.IsNullOrEmpty(delimiters.Italic.Start))
        {
            var italicStartEscaped = Regex.Escape(delimiters.Italic.Start);
            var italicEndRaw = string.IsNullOrEmpty(delimiters.Italic.End) ? delimiters.Italic.Start : delimiters.Italic.End;
            var italicEndEscaped = Regex.Escape(italicEndRaw);

            processedMessage = Regex.Replace(processedMessage, $"{italicStartEscaped}(.*?){italicEndEscaped}", "<i>$1</i>");
            processedMessage = Regex.Replace(processedMessage, $"{italicStartEscaped}(.*)$", "<i>$1</i>");

            if (speechText != null)
            {
                speechText = speechText.Replace(delimiters.Italic.Start, string.Empty);
                speechText = speechText.Replace(italicEndRaw, string.Empty);
            }
        }

        // Handle bold formatting
        if (!string.IsNullOrEmpty(delimiters.Bold.Start))
        {
            var boldStartEscaped = Regex.Escape(delimiters.Bold.Start);
            var boldEndRaw = string.IsNullOrEmpty(delimiters.Bold.End) ? delimiters.Bold.Start : delimiters.Bold.End;
            var boldEndEscaped = Regex.Escape(boldEndRaw);

            processedMessage = Regex.Replace(processedMessage, $"{boldStartEscaped}(.*?){boldEndEscaped}", "<strong>$1</strong>");
            processedMessage = Regex.Replace(processedMessage, $"{boldStartEscaped}(.*)$", "<strong>$1</strong>");

            if (speechText != null)
            {
                speechText = speechText.Replace(delimiters.Bold.Start, string.Empty);
                speechText = speechText.Replace(boldEndRaw, string.Empty);
            }
        }

        context.Message = processedMessage;

        if (speechText != null)
        {
            context.SetSpeechText(speechText);
        }

        return context;
    }
}
