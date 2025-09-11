using System.Collections.Generic;
using ProtoBuf;
using thebasics.Configs;
using thebasics.ModSystems.ProximityChat.Models;

namespace thebasics.Config
{
    [ProtoContract]
    public class ColorThemes
    {
        [ProtoMember(1)]
        public ColorTheme NicknameTheme { get; set; }

        [ProtoMember(2)]
        public ColorTheme EmoteTheme { get; set; }

        [ProtoMember(3)]
        public ColorTheme EnvironmentalTheme { get; set; }

        [ProtoMember(4)]
        public ColorTheme OOCTheme { get; set; }

        [ProtoMember(5)]
        public ColorTheme GOOCTheme { get; set; }

        public void InitializeDefaultsIfNeeded(ModConfig config)
        {
            NicknameTheme ??= DefaultNicknameThemeIfUsingDefaultValues(config);
            EmoteTheme ??= DefaultEmoteThemeIfUsingDefaultValues(config);
            EnvironmentalTheme ??= DefaultEnvironmentalThemeIfUsingDefaultValues(config);
            OOCTheme ??= DefaultOOCThemeIfUsingDefaultValues(config);
            GOOCTheme ??= DefaultGOOCThemeIfUsingDefaultValues(config);
        }
        
        public Dictionary<string, ColorTheme> GetAllThemes()
        {
            var themes = new Dictionary<string, ColorTheme>();
            
            if (NicknameTheme != null)
            {
                themes["nickname"] = NicknameTheme;
                themes["nick"] = NicknameTheme;
            }
            
            if (EmoteTheme != null)
            {
                themes["emote"] = EmoteTheme;
            }
            
            if (EnvironmentalTheme != null)
            {
                themes["environmental"] = EnvironmentalTheme;
                themes["env"] = EnvironmentalTheme;
            }
            
            if (OOCTheme != null)
            {
                themes["ooc"] = OOCTheme;
            }
            
            if (GOOCTheme != null)
            {
                themes["gooc"] = GOOCTheme;
                themes["globalooc"] = GOOCTheme;
            }
            
            return themes;
        }

        private ColorTheme DefaultNicknameThemeIfUsingDefaultValues(ModConfig config)
        {
            return new ColorTheme(
                defaultColor: "#FFFFFF",
                isPlayerConfigurable: config?.ProximityChatAllowPlayersToChangeNicknameColors ?? true,
                modDataKey: "BASIC_NICKNAME_COLOR",
                permissionRequired: config?.ChangeNicknameColorPermission ?? "chat"
            );
        }

        private ColorTheme DefaultEmoteThemeIfUsingDefaultValues(ModConfig config)
        {
            return new ColorTheme(
                defaultColor: config?.EmoteColor ?? "#E9DDCE",
                isPlayerConfigurable: false,
                modDataKey: "BASIC_EMOTE_COLOR",
                permissionRequired: "chat"
            );
        }

        private ColorTheme DefaultEnvironmentalThemeIfUsingDefaultValues(ModConfig config)
        {
            return new ColorTheme(
                defaultColor: "#CCCCCC",
                isPlayerConfigurable: true,
                modDataKey: "BASIC_ENVIRONMENTAL_COLOR",
                permissionRequired: "chat"
            );
        }

        private ColorTheme DefaultOOCThemeIfUsingDefaultValues(ModConfig config)
        {
            return new ColorTheme(
                defaultColor: config?.OOCColor ?? "#eaf188",
                isPlayerConfigurable: false,
                modDataKey: "BASIC_OOC_COLOR",
                permissionRequired: "chat"
            );
        }

        private ColorTheme DefaultGOOCThemeIfUsingDefaultValues(ModConfig config)
        {
            return new ColorTheme(
                defaultColor: config?.GlobalOOCColor ?? "#f1b288",
                isPlayerConfigurable: false,
                modDataKey: "BASIC_GOOC_COLOR",
                permissionRequired: "chat"
            );
        }
    }
}