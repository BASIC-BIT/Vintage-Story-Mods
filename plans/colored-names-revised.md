# Revised Colored Names Implementation Plan

## Overview
This plan implements a simple but flexible colored names system that can be applied to either IC nicknames, OOC names, or both, based on server configuration. Colors are stored per-player but their application is controlled server-side.

## Implementation Steps

1. **Update ModConfig with Application Control**
   ```csharp
   public class ModConfig
   {
       // Existing config...
       
       // Color application configuration
       public bool ApplyColorsToNicknames { get; set; } = true;  // Apply colors to IC nicknames
       public bool ApplyColorsToPlayerNames { get; set; } = false;  // Apply colors to OOC names
       public bool AllowPlayerColorCustomization { get; set; } = true;  // Whether players can set their own colors
   }
   ```

2. **Enhance IServerPlayerExtensions**
   ```csharp
   public static class IServerPlayerExtensions
   {
       public static string GetFormattedName(this IServerPlayer player, bool isIC, ModConfig config)
       {
           string name = isIC ? player.GetNickname() : player.PlayerName;
           
           // Only apply color if configured for this name type
           bool applyColor = isIC ? config.ApplyColorsToNicknames : config.ApplyColorsToPlayerNames;
           if (!applyColor)
               return name;
               
           string color = player.GetNicknameColor();
           return string.IsNullOrEmpty(color) ? name : ChatHelper.Color(name, color);
       }

       // Update GetNicknameColor to return null for no color
       public static string GetNicknameColor(this IServerPlayer player)
       {
           return GetModData<string>(player, ModDataNicknameColor, null);
       }
   }
   ```

3. **Update ChatHelper**
   ```csharp
   public static class ChatHelper
   {
       public static string Color(string message, string color)
       {
           if (string.IsNullOrEmpty(color))
               return message;
               
           return $"<font color=\"{color}\">{message}</font>";
       }
   }
   ```

4. **Update Command Handlers**
   ```csharp
   private TextCommandResult HandleNicknameColor(TextCommandCallingArgs args)
   {
       if (!Config.AllowPlayerColorCustomization)
       {
           return new TextCommandResult
           {
               Status = EnumCommandStatus.Error,
               StatusMessage = "Color customization is disabled on this server.",
           };
       }

       var player = (IServerPlayer)args.Caller.Player;
       if (args.Parsers[0].IsMissing)
       {
           var color = player.GetNicknameColor();
           if (string.IsNullOrEmpty(color))
           {
               return new TextCommandResult
               {
                   Status = EnumCommandStatus.Success,
                   StatusMessage = "You don't have a custom color set.",
               };
           }
           return new TextCommandResult
           {
               Status = EnumCommandStatus.Success,
               StatusMessage = $"Your color is: {ChatHelper.Color(color, color)}",
           };
       }

       var newColor = (Color)args.Parsers[0].GetValue();
       var colorHex = ColorTranslator.ToHtml(newColor);
       player.SetNicknameColor(colorHex);
       
       return new TextCommandResult
       {
           Status = EnumCommandStatus.Success,
           StatusMessage = $"Color set to: {ChatHelper.Color(colorHex, colorHex)}",
       };
   }

   private TextCommandResult ClearNicknameColor(TextCommandCallingArgs args)
   {
       var player = (IServerPlayer)args.Caller.Player;
       player.ClearNicknameColor();
       return new TextCommandResult
       {
           Status = EnumCommandStatus.Success,
           StatusMessage = "Your color has been cleared.",
       };
   }
   ```

## Testing Plan

1. **Configuration Tests**
   - Test ApplyColorsToNicknames toggle
   - Test ApplyColorsToPlayerNames toggle
   - Test AllowPlayerColorCustomization toggle
   - Verify color application in all configuration combinations

2. **Color Application Tests**
   - Verify colors appear correctly in IC chat when enabled
   - Verify colors appear correctly in OOC chat when enabled
   - Verify no colors appear when disabled
   - Test color clearing functionality

3. **Edge Cases**
   - Test behavior with null/empty colors
   - Test color persistence through IC/OOC switches
   - Verify interaction with other chat features (languages, emotes, etc.)
   - Test behavior when toggling configuration options

## Migration Notes

1. **Server Configuration**
   - Existing color data remains valid
   - New configuration options default to most common use case
   - No database migration required

2. **Backward Compatibility**
   - Existing colored names continue working if enabled in config
   - Players keep their colors but application is controlled server-side
   - Clear upgrade path for server administrators

## Documentation Updates

1. **Server Admin Guide**
   - Document new configuration options
   - Explain color application control
   - Provide example configurations for common use cases

2. **User Guide**
   - Update color command documentation
   - Explain when colors will be visible
   - Document color clearing functionality

## Performance Impact
- No additional storage requirements
- No new processing overhead
- Configuration checks are minimal

## Security Considerations
- Color commands respect server configuration
- Input validation remains unchanged
- No new security implications 