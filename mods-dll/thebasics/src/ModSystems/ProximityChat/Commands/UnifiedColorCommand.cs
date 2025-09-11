using System;
using System.Drawing;
using System.Linq;
using thebasics.Config;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;
using thebasics.Utilities.Parsers;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.ProximityChat.Commands
{
    public class UnifiedColorCommand
    {
        private readonly ColorThemes _colorThemes;
        private readonly ICoreServerAPI _api;
        
        public UnifiedColorCommand(ColorThemes colorThemes, ICoreServerAPI api)
        {
            _colorThemes = colorThemes;
            _api = api;
        }
        
        public TextCommandResult HandleColorCommand(TextCommandCallingArgs args)
        {
            var player = (IServerPlayer)args.Caller.Player;
            var allThemes = _colorThemes.GetAllThemes();
            
            if (args.Parsers[0].IsMissing)
            {
                var availableThemes = string.Join(", ", allThemes
                    .Where(kvp => kvp.Value.IsPlayerConfigurable)
                    .Select(kvp => kvp.Key)
                    .Distinct()
                    .OrderBy(name => name));
                    
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = $"Usage: /color [theme] [color|clear]. Available themes: {availableThemes}"
                };
            }
            
            var themeName = ((string)args.Parsers[0].GetValue()).ToLowerInvariant();
            
            if (!allThemes.TryGetValue(themeName, out var theme))
            {
                var availableThemes = string.Join(", ", allThemes.Keys.Distinct().OrderBy(name => name));
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = $"Unknown theme '{themeName}'. Available themes: {availableThemes}"
                };
            }
            
            if (!theme.IsPlayerConfigurable)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = $"The '{themeName}' color is not configurable by players"
                };
            }
            
            if (!player.HasPrivilege(theme.PermissionRequired))
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = $"You don't have permission to change '{themeName}' color"
                };
            }
            
            if (args.Parsers[1].IsMissing)
            {
                return GetColorForTheme(player, theme, themeName);
            }
            
            var action = (string)args.Parsers[1].GetValue();
            
            if (action.Equals("clear", StringComparison.OrdinalIgnoreCase))
            {
                return ClearColorForTheme(player, theme, themeName);
            }
            
            return SetColorForTheme(player, theme, themeName, action);
        }
        
        private TextCommandResult GetColorForTheme(IServerPlayer player, ColorTheme theme, string themeName)
        {
            if (!theme.HasPlayerColor(player))
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = $"You don't have a {themeName} color set! You can set it with `/color {themeName} [color]`"
                };
            }
            
            var color = theme.GetEffectiveColor(player);
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = $"Your {themeName} color is: {ChatHelper.Color(color, color)}"
            };
        }
        
        private TextCommandResult SetColorForTheme(IServerPlayer player, ColorTheme theme, string themeName, string colorStr)
        {
            Color newColor;
            try
            {
                newColor = ColorTranslator.FromHtml(colorStr);
            }
            catch
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = "Invalid color format. Use hex format like #FF0000"
                };
            }
            
            var colorHex = ColorTranslator.ToHtml(newColor);
            if (colorHex.Contains('<') || colorHex.Contains('>'))
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = "Invalid color"
                };
            }
            
            theme.SetPlayerColor(player, colorHex);
            
            if (themeName.StartsWith("nick"))
            {
                SwapOutNameTag(player);
            }
            
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = $"{char.ToUpper(themeName[0])}{themeName.Substring(1)} color set to: {ChatHelper.Color(colorHex, colorHex)}"
            };
        }
        
        private TextCommandResult ClearColorForTheme(IServerPlayer player, ColorTheme theme, string themeName)
        {
            theme.ClearPlayerColor(player);
            
            if (themeName.StartsWith("nick"))
            {
                SwapOutNameTag(player);
            }
            
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = $"Your {themeName} color has been cleared"
            };
        }
        
        private void SwapOutNameTag(IServerPlayer player)
        {
            var behavior = player.Entity.GetBehavior<Vintagestory.GameContent.EntityBehaviorNameTag>();
            if (behavior != null && _api.ModLoader.GetModSystem<RPProximityChatSystem>() != null)
            {
                var rpSystem = _api.ModLoader.GetModSystem<RPProximityChatSystem>();
                var config = rpSystem.Config;
                
                if (config.ShowNicknameInNametag)
                {
                    var nickname = player.GetNickname();
                    var displayName = config.ShowPlayerNameInNametag ? $"{nickname} ({player.PlayerName})" : nickname;
                    behavior.SetName(displayName);
                }
            }
        }
        
        public TextCommandResult HandleAdminColorCommand(TextCommandCallingArgs args)
        {
            var allThemes = _colorThemes.GetAllThemes();
            
            if (args.Parsers[0].IsMissing || args.Parsers[1].IsMissing)
            {
                var availableThemes = string.Join(", ", allThemes.Keys.Distinct().OrderBy(name => name));
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = $"Usage: /admincolor [player] [theme] [color|clear]. Available themes: {availableThemes}"
                };
            }
            
            var targetPlayer = _api.GetPlayerByUID(((PlayerUidName[])args.Parsers[0].GetValue())[0].Uid);
            if (targetPlayer == null)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = "Cannot find player"
                };
            }
            
            var themeName = ((string)args.Parsers[1].GetValue()).ToLowerInvariant();
            
            if (!allThemes.TryGetValue(themeName, out var theme))
            {
                var availableThemes = string.Join(", ", allThemes.Keys.Distinct().OrderBy(name => name));
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = $"Unknown theme '{themeName}'. Available themes: {availableThemes}"
                };
            }
            
            if (args.Parsers[2].IsMissing)
            {
                return GetColorForPlayer(targetPlayer, theme, themeName);
            }
            
            var action = (string)args.Parsers[2].GetValue();
            
            if (action.Equals("clear", StringComparison.OrdinalIgnoreCase))
            {
                return ClearColorForPlayer(targetPlayer, theme, themeName);
            }
            
            return SetColorForPlayer(targetPlayer, theme, themeName, action);
        }
        
        private TextCommandResult GetColorForPlayer(IServerPlayer player, ColorTheme theme, string themeName)
        {
            if (!theme.HasPlayerColor(player))
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = $"Player {player.PlayerName} does not have a {themeName} color set"
                };
            }
            
            var color = theme.GetEffectiveColor(player);
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = $"Player {player.PlayerName} {themeName} color is: {ChatHelper.Color(color, color)}"
            };
        }
        
        private TextCommandResult SetColorForPlayer(IServerPlayer player, ColorTheme theme, string themeName, string colorStr)
        {
            var oldColor = theme.GetEffectiveColor(player);
            
            Color newColor;
            try
            {
                newColor = ColorTranslator.FromHtml(colorStr);
            }
            catch
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = "Invalid color format. Use hex format like #FF0000"
                };
            }
            
            var colorHex = ColorTranslator.ToHtml(newColor);
            if (colorHex.Contains('<') || colorHex.Contains('>'))
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = "Invalid color"
                };
            }
            
            theme.SetPlayerColor(player, colorHex);
            
            if (themeName.StartsWith("nick"))
            {
                SwapOutNameTag(player);
            }
            
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = $"Player {player.PlayerName} {themeName} color has been set to: {ChatHelper.Color(colorHex, colorHex)}. Old color: {oldColor}"
            };
        }
        
        private TextCommandResult ClearColorForPlayer(IServerPlayer player, ColorTheme theme, string themeName)
        {
            theme.ClearPlayerColor(player);
            
            if (themeName.StartsWith("nick"))
            {
                SwapOutNameTag(player);
            }
            
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = $"Player {player.PlayerName} {themeName} color has been cleared"
            };
        }
    }
}