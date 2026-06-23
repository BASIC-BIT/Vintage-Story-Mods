# BasicConfig

Shared in-game configuration UI and command registry for BASIC mods.

Consumer mods reference `basicconfig.csproj` at compile time and declare a runtime `basicconfig` dependency in `modinfo.json`. Each consumer registers its config schema with `BasicConfigModSystem`; server admins can then open registered panels through `/basicconfig` or the consumer-specific config command.
