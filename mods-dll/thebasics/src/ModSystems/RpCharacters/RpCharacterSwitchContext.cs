using thebasics.Configs;
using thebasics.ModSystems.RpCharacters.Models;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.RpCharacters;

public class RpCharacterSwitchContext
{
    public RpCharacterSwitchContext(IServerPlayer player, ModConfig config, RpCharacterRegistry registry, RpCharacterRecord active, RpCharacterRecord target)
    {
        Player = player;
        Config = config;
        Registry = registry;
        Active = active;
        Target = target;
    }

    public IServerPlayer Player { get; }

    public ModConfig Config { get; }

    public RpCharacterRegistry Registry { get; }

    public RpCharacterRecord Active { get; }

    public RpCharacterRecord Target { get; }
}
