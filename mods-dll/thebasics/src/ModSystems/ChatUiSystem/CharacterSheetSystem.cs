using System;
using System.Collections.Generic;
using System.Linq;
using thebasics.Extensions;
using thebasics.Models;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.ChatUiSystem
{
    public class CharacterSheetSystem : BaseBasicModSystem
    {
        private ICoreClientAPI capi;
        private ICoreServerAPI sapi;
        private CharacterSheetDialog characterSheetDialog;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return true; // Load on both client and server
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            
            api.RegisterCommand("charsheet", "Opens the character sheet dialog", "",
                (int groupId, CmdArgs args) => { OnClientCharSheetCommand(); });

            // Register network handlers
            api.Network.RegisterChannel("charactersheet")
                .RegisterMessageType<CharacterSheetRequestPacket>()
                .RegisterMessageType<CharacterSheetResponsePacket>();
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            
            api.RegisterCommand("viewchar", "View another player's character sheet", "",
                (IServerPlayer player, int groupId, CmdArgs args) => OnServerViewCharCommand(player, args[0]));

            // Register network handlers
            api.Network.RegisterChannel("charactersheet")
                .RegisterMessageType<CharacterSheetRequestPacket>()
                .RegisterMessageType<CharacterSheetResponsePacket>();
        }

        protected override void BasicStartServerSide()
        {
            // Implementation of abstract method
        }

        private void OnClientCharSheetCommand()
        {
            if (characterSheetDialog != null)
            {
                characterSheetDialog.TryOpen();
            }
        }

        private TextCommandResult OnServerViewCharCommand(IServerPlayer player, string playerName)
        {
            IServerPlayer targetPlayer = sapi.World.PlayerByUid(playerName) as IServerPlayer;

            if (targetPlayer == null)
            {
                return TextCommandResult.Error($"Player {playerName} not found");
            }

            var packet = new CharacterSheetRequestPacket
            {
                TargetPlayerUid = targetPlayer.PlayerUID
            };

            sapi.Network.GetChannel("charactersheet")
                .SendPacket(packet, player);

            return TextCommandResult.Success();
        }

        private void OnClientReceivedCharacterSheet(CharacterSheetModel sheet)
        {
            characterSheetDialog?.TryClose();
            characterSheetDialog = new CharacterSheetDialog(capi, sheet, () =>
            {
                // Send updated sheet back to server
                capi.Network.GetChannel("charactersheet")
                    .SendPacket(new CharacterSheetResponsePacket() { Sheet = sheet });
            });
            characterSheetDialog.TryOpen();
        }

        private void OnServerReceivedCharacterSheet(IServerPlayer player, CharacterSheetModel sheet)
        {
            player.SetCharacterSheet(sheet);
        }

        public class CharacterSheetRequestPacket
        {
            public string TargetPlayerUid { get; set; }
        }

        public class CharacterSheetResponsePacket
        {
            public CharacterSheetModel Sheet { get; set; }
        }
    }
} 