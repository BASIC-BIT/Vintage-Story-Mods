using System;
using System.Threading.Tasks;
using thaumstory.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace thaumstory
{
    public class Server : ModSystem
    {
        public static ICoreServerAPI Api;

        private ICoreServerAPI api
        {
            get
            {
                return Api;
            }
            set
            {
                Api = value;
            }
        }

        private ModConfig config;

        private const string CONFIGNAME = "thaumstory.json";

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            try
            {
                this.config = api.LoadModConfig<ModConfig>(CONFIGNAME);
            }
            catch (Exception e)
            {
                api.Server.LogError("Thaumstory: Failed to load mod config!");
                return;
            }

            if (this.config == null)
            {
                api.Server.LogNotification("Thaumstory: Non-existant modconfig at 'ModConfig/" + CONFIGNAME + "}', creating default and disabling mod...");
                api.StoreModConfig(new ModConfig(), CONFIGNAME);

                return;
            }

            this.api = api;

            Task.Run(async () =>
            {
                await this.MainAsync(api);
            });
        }

        private async Task MainAsync(ICoreServerAPI api)
        {
            // api.Event.GameWorldSave += Event_GameWorldSave;
            // api.Event.ServerResume += Event_SaveFinished;
            
            ((ICoreServerAPI) api).RegisterItemClass("ItemWand", typeof (ItemWand));
            ((ICoreServerAPI) api).RegisterEntity("EntityProjectileSpell", typeof (EntityProjectileSpell));

            await Task.Delay(-1);
        }

        // private void Event_GameWorldSave()
        // {
        //     if (config.SendServerSaveAnnouncement)
        //     {
        //         api.SendMessageToGroup(GlobalConstants.GeneralChatGroup, this.config.TEXT_ServerSaveAnnouncement, EnumChatType.Notification);
        //     }
        // }

        // private void Event_SaveFinished()
        // {
        //     if (config.SendServerSaveFinishedAnnouncement)
        //     {
        //         api.SendMessageToGroup(GlobalConstants.GeneralChatGroup, this.config.TEXT_ServerSaveFinished, EnumChatType.Notification);
        //     }
        // }
        

    }
}