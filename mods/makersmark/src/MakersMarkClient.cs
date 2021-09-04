using System;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace makersmark
{
    public class MakersMarkClient : ModSystem
    {
        
        public static ICoreClientAPI Api;

        private ICoreClientAPI api
        {
            get { return Api; }
            set { Api = value; }
        }

        private ModConfig config;

        private const string CONFIGNAME = "thaumstory.json";

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Client;
        }


        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            try
            {
                this.config = api.LoadModConfig<ModConfig>(CONFIGNAME);
            }
            catch (Exception e)
            {
                api.Logger.Error("Thaumstory: Failed to load mod config!");
                return;
            }

            if (this.config == null)
            {
                api.Logger.Notification("Thaumstory: Non-existant modconfig at 'ModConfig/" + CONFIGNAME +
                                        "}', creating default and disabling mod...");
                api.StoreModConfig(new ModConfig(), CONFIGNAME);

                return;
            }

            this.api = api;

            Task.Run(async () => { await this.MainAsync(api); });
        }

        private async Task MainAsync(ICoreClientAPI api)
        {
            api.Event.MatchesGridRecipe += Event_MatchesGridRecipe;
            // api.Event.ServerResume += Event_SaveFinished;

            // ((ICoreClientAPI) api).RegisterItemClass("ItemWand", typeof (ItemWand));
            // ((ICoreClientAPI) api).RegisterEntity("EntityProjectileSpell", typeof (EntityProjectileSpell));

            await Task.Delay(-1);
        }

        private bool Event_MatchesGridRecipe(
            IPlayer player,
            GridRecipe recipe,
            ItemSlot[] ingredients,
            int gridWidth)
        {
            return ;
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