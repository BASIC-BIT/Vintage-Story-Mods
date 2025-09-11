using System;
using System.Linq;
using Vintagestory.API.Common;

namespace thebasics.Utilities.Parsers
{
    public class ColorThemeArgParser : ArgumentParserBase
    {
        private readonly string[] _validThemes;
        private string themeValue;
        
        public ColorThemeArgParser(string argName, string[] validThemes, bool isRequired) : base(argName, isRequired)
        {
            _validThemes = validThemes;
        }
        
        public override object GetValue()
        {
            return themeValue;
        }
        
        public override void SetValue(object data)
        {
            themeValue = data as string;
        }
        
        public override string GetSyntaxExplanation(string indent)
        {
            return indent + GetSyntax() + " is one of: " + string.Join(", ", _validThemes.Distinct().OrderBy(t => t));
        }
        
        public override EnumParseResult TryProcess(TextCommandCallingArgs args, Action<AsyncParseResults> onReady = null)
        {
            var word = args.RawArgs.PopWord();
            if (string.IsNullOrEmpty(word))
            {
                if (isMandatoryArg)
                {
                    lastErrorMessage = "Missing theme name";
                    return EnumParseResult.Bad;
                }
                return EnumParseResult.Good;
            }
            
            themeValue = word;
            return EnumParseResult.Good;
        }
    }
    
    public class OptionalColorOrActionArgParser : ArgumentParserBase
    {
        private string actionValue;
        
        public OptionalColorOrActionArgParser(string argName) : base(argName, false)
        {
        }
        
        public override object GetValue()
        {
            return actionValue;
        }
        
        public override void SetValue(object data)
        {
            actionValue = data as string;
        }
        
        public override string GetSyntaxExplanation(string indent)
        {
            return indent + GetSyntax() + " is a hex color (e.g., #FF0000) or 'clear'";
        }
        
        public override EnumParseResult TryProcess(TextCommandCallingArgs args, Action<AsyncParseResults> onReady = null)
        {
            actionValue = args.RawArgs.PopWord();
            return EnumParseResult.Good;
        }
    }
}