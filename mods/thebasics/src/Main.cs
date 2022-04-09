using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace thebasics
{
    public class Main : ModSystem
    {
        public static ICoreServerAPI Api;

        private ICoreServerAPI api
        {
            get { return Api; }
            set { Api = value; }
        }

        private ModConfig _config;

        private const string CONFIGNAME = "the_basics.json";

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Server;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            try
            {
                _config = api.LoadModConfig<ModConfig>(CONFIGNAME);
            }
            catch (Exception)
            {
                api.Server.LogError("The Basics: Failed to load mod config!");
                return;
            }

            if (_config == null)
            {
                api.Server.LogNotification(
                    "The Basics: Non-existant modconfig at 'ModConfig/" + CONFIGNAME + "', creating default and disabling mod...");
                api.StoreModConfig(new ModConfig(), CONFIGNAME);

                return;
            }

            this.api = api;

            Task.Run(async () => { await this.MainAsync(api); });
        }

        private async Task MainAsync(ICoreServerAPI api)
        {
            api.Event.GameWorldSave += Event_GameWorldSave;
            api.Event.ServerResume += Event_SaveFinished;

            await Task.Delay(-1);
        }

        private void Event_GameWorldSave()
        {
            if (_config.SendServerSaveAnnouncement)
            {
                api.SendMessageToGroup(GlobalConstants.GeneralChatGroup, this._config.TEXT_ServerSaveAnnouncement,
                    EnumChatType.Notification);
            }
        }

        private void Event_SaveFinished()
        {
            if (_config.SendServerSaveFinishedAnnouncement)
            {
                api.SendMessageToGroup(GlobalConstants.GeneralChatGroup, this._config.TEXT_ServerSaveFinished,
                    EnumChatType.Notification);
            }
        }
    }
}