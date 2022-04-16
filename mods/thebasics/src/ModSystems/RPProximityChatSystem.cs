using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using thebasics.Extensions;
using thebasics.Models;
using thebasics.Utilities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace thebasics.ModSystems
{
    public class RPProximityChatSystem : BaseBasicModSystem
    {
        private const string ProximityGroupName = "Proximity";
        private PlayerGroup _proximityGroup;

        protected override void BasicStartServerSide()
        {
            HookEvents();
            RegisterCommands();
            SetupProximityGroup();
        }

        private void RegisterCommands()
        {
            API.RegisterCommand("pmessage", "Sends a message to all players in a specific area", null,
                OnPMessageHandler, Privilege.announce);
            API.RegisterCommand(new[] {"nick", "setnick", "nickname"}, "Get or set your nickname", "", SetNickname);
            API.RegisterCommand("clearnick", "Clear your nickname", "", ClearNickname);
            API.RegisterCommand(new[] {"me", "m"}, "Send a proximity emote message", "", Emote);
            API.RegisterCommand("it", "Send a proximity environment message", "", EnvironmentMessage);
            API.RegisterCommand(new[] {"yell", "y"}, "Set your chat mode to Yelling, or yell a single message", "",
                Yell);
            API.RegisterCommand(new[] {"whisper", "w"},
                "Set your chat mode to Whispering, or whisper a single message", "",
                Whisper);
            API.RegisterCommand(new[] {"say", "normal", "s"},
                "Set your chat mode back to normal, or say a single message", "", Say);
            API.RegisterCommand(new[] {"hands", "h"}, "Set your chat mode to Sign Language, or sign a single message",
                "", Sign);
            API.RegisterCommand("emotemode", "Turn Emote-only mode on or off", "/emotemode [on|off]", EmoteMode);
            API.RegisterCommand("rptext", "Turn the whole RP system on or off for your messages", "/rptext [on|off]",
                RpTextEnabled);
        }

        private void HookEvents()
        {
            API.Event.PlayerChat += Event_PlayerChat;
            API.Event.PlayerJoin += Event_PlayerJoin;
        }

        private void SetupProximityGroup()
        {
            _proximityGroup = API.Groups.GetPlayerGroupByName(ProximityGroupName);
            if (_proximityGroup == null)
            {
                _proximityGroup = new PlayerGroup()
                {
                    Name = ProximityGroupName,
                    OwnerUID = null
                };
                API.Groups.AddPlayerGroup(_proximityGroup);
                _proximityGroup.Md5Identifier = GameMath.Md5Hash(_proximityGroup.Uid + "null");
            }
        }

        private void OnPMessageHandler(IServerPlayer player, int groupId, CmdArgs args)
        {
            Vec3d spawnpos = API.World.DefaultSpawnPosition.XYZ;
            spawnpos.Y = 0;
            Vec3d targetpos = null;
            if (player.Entity == null)
            {
                targetpos = args.PopFlexiblePos(spawnpos, spawnpos);
            }
            else
            {
                targetpos = args.PopFlexiblePos(player.Entity.Pos.XYZ, spawnpos);
            }

            if (targetpos == null)
            {
                player.SendMessage(groupId, @"Invalid position supplied. Syntax: [coord] [coord] [coord] whereas
                                             [coord] may be ~[decimal] or =[decimal] or [decimal]. 
                                             ~ denotes a position relative to the player 
                                             = denotes an absolute position 
                                             no prefix denotes a position relative to the map middle",
                    EnumChatType.CommandError);
                return;
            }

            var blockRadius = args.PopInt();
            if (!blockRadius.HasValue)
            {
                player.SendMessage(groupId,
                    "Invalid radius supplied. Syntax: =[abscoord] =[abscoord] =[abscoord] [radius]",
                    EnumChatType.CommandError);
                return;
            }

            var message = args.PopAll();
            if (string.IsNullOrEmpty(message))
            {
                player.SendMessage(groupId,
                    "Invalid message supplied. Syntax: =[abscoord] =[abscoord] =[abscoord] [radius] [message]",
                    EnumChatType.CommandError);
                return;
            }

            foreach (var nearbyPlayerData in API.World.AllOnlinePlayers
                         .Select(x => new {Position = x.Entity.ServerPos, Player = (IServerPlayer) x})
                         .Where(x => Math.Abs(x.Position.DistanceTo(targetpos)) < blockRadius))
            {
                nearbyPlayerData.Player.SendMessage(this._proximityGroup.Uid, message, EnumChatType.CommandSuccess);
            }
        }

        private void Event_PlayerJoin(IServerPlayer byPlayer)
        {
            var proximityGroup = API.Groups.GetPlayerGroupByName(ProximityGroupName);
            var playerProximityGroup = byPlayer.GetGroup(proximityGroup.Uid);
            if (playerProximityGroup == null)
            {
                var newMembership = new PlayerGroupMembership()
                {
                    GroupName = proximityGroup.Name,
                    GroupUid = proximityGroup.Uid,
                    Level = EnumPlayerGroupMemberShip.Member
                };
                byPlayer.ServerData.PlayerGroupMemberShips.Add(proximityGroup.Uid, newMembership);
                proximityGroup.OnlinePlayers.Add(byPlayer);
            }
        }

        private void Event_PlayerChat(IServerPlayer byPlayer, int channelId, ref string message, ref string data,
            Vintagestory.API.Datastructures.BoolRef consumed)
        {
            var proximityGroup = API.Groups.GetPlayerGroupByName(ProximityGroupName);
            if (proximityGroup.Uid == channelId)
            {
                if (byPlayer.GetRpTextEnabled())
                {
                    if (byPlayer.HasNickname())
                    {
                        var content = GetMessage(message);
                        var isEmote = content[0] == '*';
                        var isOOC = content[0] == '(';
                        if (isEmote)
                        {
                            content = content.Remove(0, 1);
                            message = GetFullEmoteMessage(byPlayer, content);
                        }
                        else if (isOOC)
                        {
                        }
                        else
                        {
                            message = GetFullRPMessage(byPlayer, content);
                        }

                        SendLocalChat(byPlayer, message, data: data);
                    }
                    else
                    {
                        byPlayer.SendMessage(channelId,
                            "You need a nickname to use proximity chat!  You can set it with `/nick MyName`",
                            EnumChatType.CommandError);
                    }
                }
                else
                {
                    SendLocalChat(byPlayer, message, data: data);
                }

                consumed.value = true;
            }
        }

        private string GetFullRPMessage(IServerPlayer player, string content, ProximityChatMode? tempMode = null)
        {
            if (player.GetEmoteMode())
            {
                return GetFullEmoteMessage(player, content);
            }

            var message = new StringBuilder();
            message.Append(GetFormattedNickname(player));
            message.Append(" ");
            message.Append(GetProximityChatVerb(player, tempMode));
            message.Append(" ");
            message.Append(Config.ProximityChatModeQuotationStart[player.GetChatMode(tempMode)]);
            message.Append(GetRPMessage(player, content, tempMode));
            message.Append(Config.ProximityChatModeQuotationEnd[player.GetChatMode(tempMode)]);

            return message.ToString();
        }

        private string GetFormattedNickname(IServerPlayer player)
        {
            var nick = player.GetNickname();
            return Config.BoldNicknames ? ChatHelper.Strong(nick) : nick;
        }

        private string GetFullEmoteMessage(IServerPlayer player, string content)
        {
            return ChatHelper.Build(GetFormattedNickname(player), " ", GetEmoteMessage(content));
        }

        private void SendLocalChat(IServerPlayer byPlayer, string message, ProximityChatMode? tempMode = null,
            EnumChatType chatType = EnumChatType.OthersMessage, string data = null)
        {
            var proximityGroup = API.Groups.GetPlayerGroupByName(ProximityGroupName);
            foreach (var player in API.World.AllOnlinePlayers.Where(x =>
                         x.Entity.Pos.AsBlockPos.ManhattenDistance(byPlayer.Entity.Pos.AsBlockPos) <
                         GetProximityChatRange(byPlayer, tempMode)))
            {
                var serverPlayer = player as IServerPlayer;

                serverPlayer.SendMessage(proximityGroup.Uid, message, chatType, data);
            }
        }

        private int GetProximityChatRange(IServerPlayer player, ProximityChatMode? tempMode = null)
        {
            return Config.ProximityChatModeDistances[player.GetChatMode(tempMode)];
        }

        private string GetProximityChatVerb(IServerPlayer player, ProximityChatMode? tempMode = null)
        {
            return Config.ProximityChatModeVerbs[player.GetChatMode(tempMode)].GetRandomElement();
        }

        private string GetProximityChatPunctuation(IServerPlayer player, ProximityChatMode? tempMode = null)
        {
            return Config.ProximityChatModePunctuation[player.GetChatMode(tempMode)];
        }

        private string GetRPMessage(IServerPlayer player, string message, ProximityChatMode? tempMode = null)
        {
            var builder = new StringBuilder();

            builder.Append(message.Substring(0, 1).ToUpper() + message.Substring(1, message.Length - 1));

            if (ChatHelper.DoesMessageNeedPunctuation(message))
            {
                builder.Append(GetProximityChatPunctuation(player, tempMode));
            }

            return builder.ToString();
        }

        private string GetEmoteMessage(string message)
        {
            var builder = new StringBuilder();

            builder.Append(message.Trim());
            if (ChatHelper.DoesMessageNeedPunctuation(message))
            {
                builder.Append(".");
            }

            return builder.ToString();
        }

        private string GetMessage(string message)
        {
            var foundText = new Regex(@".*?> (.+)$").Match(message);

            return foundText.Groups[1].Value.Trim();
        }

        private void SetNickname(IServerPlayer player, int groupId, CmdArgs args)
        {
            if (args.Length == 0)
            {
                if (player.HasNickname())
                {
                    player.SendMessage(groupId, ChatHelper.Build("Your nickname is: ", player.GetNickname()),
                        EnumChatType.Notification);
                }
                else
                {
                    player.SendMessage(groupId, "You don't have a nickname!  You can set it with `/nick MyName`",
                        EnumChatType.Notification);
                }
            }
            else
            {
                var nickname = args.PopAll();
                player.SetNickname(nickname);
                player.SendMessage(groupId,
                    ChatHelper.Build("Okay, your nickname is set to ", ChatHelper.Quote(nickname)),
                    EnumChatType.Notification);
            }
        }

        private void Emote(IServerPlayer byPlayer, int groupId, CmdArgs args)
        {
            if (byPlayer.HasNickname())
            {
                SendLocalChat(byPlayer, GetFullEmoteMessage(byPlayer, args.PopAll()));
            }
            else
            {
                byPlayer.SendMessage(groupId, "You need a nickname to use emotes!  You can set it with `/nick MyName`",
                    EnumChatType.CommandError);
            }
        }

        private void EnvironmentMessage(IServerPlayer byPlayer, int groupId, CmdArgs args)
        {
            SendLocalChat(byPlayer,
                ChatHelper.Wrap(GetRPMessage(byPlayer, args.PopAll(), ProximityChatMode.Normal), "*"),
                chatType: EnumChatType.Notification);
        }

        private void ClearNickname(IServerPlayer player, int groupId, CmdArgs args)
        {
            player.ClearNickname();
            player.SendMessage(groupId, "Your nickname has been cleared.", EnumChatType.Notification);
        }

        private void Yell(IServerPlayer player, int groupId, CmdArgs args)
        {
            if (args.Length > 0)
            {
                SendLocalChat(player, GetFullRPMessage(player, args.PopAll(), ProximityChatMode.Yell),
                    ProximityChatMode.Yell);
            }
            else
            {
                player.SetChatMode(ProximityChatMode.Yell);
                player.SendMessage(groupId, "You are now yelling.", EnumChatType.Notification);
            }
        }

        private void Sign(IServerPlayer player, int groupId, CmdArgs args)
        {
            if (args.Length > 0)
            {
                SendLocalChat(player, GetFullRPMessage(player, args.PopAll(), ProximityChatMode.Sign),
                    ProximityChatMode.Sign);
            }
            else
            {
                player.SetChatMode(ProximityChatMode.Sign);
                player.SendMessage(groupId, "You are now signing.", EnumChatType.Notification);
            }
        }

        private void Whisper(IServerPlayer player, int groupId, CmdArgs args)
        {
            if (args.Length > 0)
            {
                SendLocalChat(player, GetFullRPMessage(player, args.PopAll(), ProximityChatMode.Whisper),
                    ProximityChatMode.Whisper);
            }
            else
            {
                player.SetChatMode(ProximityChatMode.Whisper);
                player.SendMessage(groupId, "You are now whispering.", EnumChatType.Notification);
            }
        }

        private void Say(IServerPlayer player, int groupId, CmdArgs args)
        {
            if (args.Length > 0)
            {
                SendLocalChat(player, GetFullRPMessage(player, args.PopAll(), ProximityChatMode.Normal),
                    ProximityChatMode.Normal);
            }
            else
            {
                player.SetChatMode(ProximityChatMode.Normal);
                player.SendMessage(groupId, "You are now talking normally.", EnumChatType.Notification);
            }
        }

        private void EmoteMode(IServerPlayer player, int groupId, CmdArgs args)
        {
            if (args.Length != 1)
            {
                player.SendMessage(groupId, "Usage: /emotemode [on|off]", EnumChatType.CommandError);
                return;
            }

            var value = args[0].ToLower();

            if (value != "on" && value != "off")
            {
                player.SendMessage(groupId, "Usage: /emotemode [on|off]", EnumChatType.CommandError);
                return;
            }

            var emoteMode = value == "on";

            player.SetEmoteMode(emoteMode);
            player.SendMessage(groupId, ChatHelper.Build("Emote mode is now ", value), EnumChatType.Notification);
        }

        private void RpTextEnabled(IServerPlayer player, int groupId, CmdArgs args)
        {
            if (args.Length != 1)
            {
                player.SendMessage(groupId, "Usage: /rptext [on|off]", EnumChatType.CommandError);
                return;
            }

            var value = args[0].ToLower();

            if (value != "on" && value != "off")
            {
                player.SendMessage(groupId, "Usage: /rptext [on|off]", EnumChatType.CommandError);
                return;
            }

            var rpTextEnabled = value == "on";

            player.SetRpTextEnabled(rpTextEnabled);
            player.SendMessage(groupId, ChatHelper.Build("RP Text is now ", value, " for your messages."),
                EnumChatType.Notification);
        }
    }
}