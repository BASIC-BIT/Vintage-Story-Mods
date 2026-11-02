using System;
using System.Collections.Generic;
using System.Linq;
using thebasics.Configs;
using thebasics.Models;
using Vintagestory.API.Server;
using Vintagestory.Server;

namespace thebasics.ModSystems.AdminConfig;

public static class ConfigAdminPrivilegeSuggestions
{
    public static List<ConfigAdminPrivilegeSuggestion> Snapshot(ICoreServerAPI api, ModConfig config)
    {
        // The public API exposes role grants, but the full registry is on ServerMain. A mod can
        // register a privilege without granting it to any role, so role grants alone are incomplete.
        var server = api?.World as ServerMain;
        var roles = api?.Server?.Config?.Roles;
        var rolePrivileges = roles?.Where(role => role != null)
            .SelectMany(role => (role.Privileges ?? []).Concat(role.RuntimePrivileges ?? []));
        IEnumerable<string> currentValues = config == null ? [] : ConfigAdminSettingRegistry.Settings
            .Where(setting => setting.IsPrivilege).Select(setting => setting.GetValue(config));
        IEnumerable<string> registeredCodes = server?.AllPrivileges;
        return Collect(registeredCodes ?? Privilege.AllCodes(),
            server?.PrivilegeDescriptions, rolePrivileges, currentValues);
    }

    public static List<ConfigAdminPrivilegeSuggestion> Collect(IEnumerable<string> registeredCodes,
        IReadOnlyDictionary<string, string> descriptions, IEnumerable<string> rolePrivileges,
        IEnumerable<string> configuredValues)
    {
        return (registeredCodes ?? []).Concat(rolePrivileges ?? []).Concat(configuredValues ?? [])
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(code => code, StringComparer.Ordinal)
            .Select(code => new ConfigAdminPrivilegeSuggestion
            {
                Code = code,
                Description = descriptions != null && descriptions.TryGetValue(code, out var description)
                    ? description ?? string.Empty : string.Empty
            }).ToList();
    }

    public static List<ConfigAdminPrivilegeSuggestion> ForClient(
        IEnumerable<ConfigAdminPrivilegeSuggestion> suggestions, IEnumerable<string> currentValues)
    {
        var entries = suggestions?.Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.Code)).ToList() ?? [];
        var descriptions = entries.GroupBy(entry => entry.Code, StringComparer.Ordinal)
            .ToDictionary(group => group.Key,
                group => group.FirstOrDefault(entry => !string.IsNullOrWhiteSpace(entry.Description))?.Description ?? string.Empty,
                StringComparer.Ordinal);
        return Collect(entries.Count == 0 ? Privilege.AllCodes() : entries.Select(entry => entry.Code),
            descriptions, [], currentValues);
    }
}
