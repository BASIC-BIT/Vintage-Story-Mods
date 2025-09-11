using ProtoBuf;
using thebasics.Extensions;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.ProximityChat.Models
{
    [ProtoContract]
    public class ColorTheme
    {
        [ProtoMember(1)]
        public string DefaultColor { get; set; }

        [ProtoMember(2)]
        public bool IsPlayerConfigurable { get; set; }

        [ProtoMember(3)]
        public string ModDataKey { get; set; }

        [ProtoMember(4)]
        public string PermissionRequired { get; set; }

        public ColorTheme()
        {
            DefaultColor = "#FFFFFF";
            IsPlayerConfigurable = false;
            ModDataKey = "";
            PermissionRequired = "chat";
        }

        public ColorTheme(string defaultColor, bool isPlayerConfigurable = false, string modDataKey = "", string permissionRequired = "chat")
        {
            DefaultColor = defaultColor;
            IsPlayerConfigurable = isPlayerConfigurable;
            ModDataKey = modDataKey;
            PermissionRequired = permissionRequired;
        }
        
        public string GetEffectiveColor(IServerPlayer player)
        {
            if (IsPlayerConfigurable && player != null && !string.IsNullOrEmpty(ModDataKey))
            {
                var playerColor = player.GetModData<string>(ModDataKey, null);
                if (!string.IsNullOrEmpty(playerColor))
                {
                    return playerColor;
                }
            }
            
            return DefaultColor;
        }
        
        public void SetPlayerColor(IServerPlayer player, string color)
        {
            if (player == null || string.IsNullOrEmpty(ModDataKey))
                return;
                
            if (!IsPlayerConfigurable)
                return;
                
            player.SetModData(ModDataKey, color);
        }
        
        public void ClearPlayerColor(IServerPlayer player)
        {
            if (player == null || string.IsNullOrEmpty(ModDataKey))
                return;
                
            player.RemoveModdata(ModDataKey);
        }
        
        public bool HasPlayerColor(IServerPlayer player)
        {
            if (player == null || string.IsNullOrEmpty(ModDataKey))
                return false;
                
            return player.GetModdata(ModDataKey) != null;
        }
    }
}