using thebasics.Configs;
using Vintagestory.API.Server;

namespace thebasics.ModSystems
{
    public abstract class BaseSubSystem
    {
        protected ICoreServerAPI API;
        protected ModConfig Config;
        protected BaseBasicModSystem System;

        protected BaseSubSystem(BaseBasicModSystem system, ICoreServerAPI api, ModConfig config)
        {
            System = system;
            API = api;
            Config = config;
        }
    }
}