using System.Runtime.CompilerServices;
using FluentAssertions;
using NSubstitute;
using ProtoBuf;
using thebasics.Configs;
using thebasics.Models;
using thebasics.ModSystems.AdminConfig;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.Server;

namespace thebasics.Tests.ModSystems.AdminConfig;

public class ConfigAdminPrivilegeSuggestionsTests
{
    [Fact]
    public void SnapshotIncludesRegisteredPrivilegeEvenWhenNoRoleHasIt()
    {
        var api = Substitute.For<ICoreServerAPI>();
        var world = (ServerMain)RuntimeHelpers.GetUninitializedObject(typeof(ServerMain));
        world.AllPrivileges = ["chat", "myMod.Unassigned"];
        world.PrivilegeDescriptions = new Dictionary<string, string>
        {
            ["myMod.Unassigned"] = "Use the custom tool."
        };
        api.World.Returns(world);

        var result = ConfigAdminPrivilegeSuggestions.Snapshot(api, null);

        result.Select(entry => entry.Code).Should().Equal("chat", "myMod.Unassigned");
        result.Single(entry => entry.Code == "myMod.Unassigned").Description.Should().Be("Use the custom tool.");
    }

    [Fact]
    public void SnapshotFallbackIncludesRoleAndRuntimeGrantsWithoutTreatingRolesAsPrivileges()
    {
        var api = Substitute.For<ICoreServerAPI>();
        var role = Substitute.For<IPlayerRole>();
        role.Code.Returns("moderator-role");
        role.Name.Returns("Moderator");
        role.Privileges.Returns(["custom.Persistent"]);
        role.RuntimePrivileges.Returns(["custom.Runtime"]);
        api.Server.Config.Roles.Returns([role]);
        var config = new ModConfig { CharacterSheetAdminPermission = "Custom.Current" };
        config.InitializeDefaultsIfNeeded();

        var result = ConfigAdminPrivilegeSuggestions.Snapshot(api, config);

        result.Select(entry => entry.Code).Should().Contain(["chat", "custom.Persistent", "custom.Runtime", "Custom.Current"]);
        result.Select(entry => entry.Code).Should().NotContain(["moderator-role", "Moderator"]);
    }

    [Fact]
    public void CollectDeduplicatesExactCodesAndPreservesCaseWhileSorting()
    {
        var result = ConfigAdminPrivilegeSuggestions.Collect(["zeta", "Alpha", "chat", "", "  "],
            new Dictionary<string, string> { ["Alpha"] = "Use Alpha." },
            ["chat", "alpha", null!], ["zeta", "Custom.Permission"]);

        result.Select(entry => entry.Code).Should().Equal("Alpha", "alpha", "chat", "Custom.Permission", "zeta");
        result.Single(entry => entry.Code == "Alpha").Description.Should().Be("Use Alpha.");
        result.Single(entry => entry.Code == "alpha").Description.Should().BeEmpty();
    }

    [Fact]
    public void CollectDoesNotTruncateCustomRegistryCodes()
    {
        var codes = Enumerable.Range(0, 700).Select(index => $"custom-{index}").ToArray();

        ConfigAdminPrivilegeSuggestions.Collect(codes, null, [], [])
            .Select(entry => entry.Code).Should().BeEquivalentTo(codes);
    }

    [Fact]
    public void ClientKeepsServerDescriptionsAndUnknownCurrentValues()
    {
        var result = ConfigAdminPrivilegeSuggestions.ForClient(
            [new() { Code = "custom.Admin", Description = "Manage custom tools." }],
            ["custom.Current", "custom.Admin"]);

        result.Select(entry => entry.Code).Should().Equal("custom.Admin", "custom.Current");
        result[0].Description.Should().Be("Manage custom tools.");
    }

    [Fact]
    public void ClientWithoutServerSuggestionsFallsBackToBuiltinsAndCurrentValues()
    {
        var result = ConfigAdminPrivilegeSuggestions.ForClient(null, ["Unregistered.Current"]);

        result.Select(entry => entry.Code).Should().Contain([Privilege.chat, Privilege.root, "Unregistered.Current"]);
    }

    [Fact]
    public void SnapshotDoesNotInitializeOrNormalizeConfiguredTeleportPermissions()
    {
        var config = new ModConfig { Teleportation = null! };
        ConfigAdminPrivilegeSuggestions.Snapshot(null, config);
        config.Teleportation.Should().BeNull();

        config.Teleportation = new TeleportationConfig { StuckCommandPrivilege = "Custom.Exact " };
        ConfigAdminPrivilegeSuggestions.Snapshot(null, config).Select(entry => entry.Code)
            .Should().Contain("Custom.Exact ");
        config.Teleportation.StuckCommandPrivilege.Should().Be("Custom.Exact ");
    }

    [Fact]
    public void PrivilegeFieldsRemainFreeTextIncludingCharacterPermissionsAndUnknownValues()
    {
        var config = new ModConfig();
        config.InitializeDefaultsIfNeeded();
        var privilegeFields = ConfigAdminSettingRegistry.Settings.Where(setting => setting.IsPrivilege).ToList();
        privilegeFields.Select(setting => setting.Key).Should().Contain([
            "CharacterSheetSetPermission", "CharacterSheetAdminPermission", "TpaRequestPrivilege",
            "Teleportation.StuckBlockedByOnlinePrivilege", "ChatHistoryPermission"]);
        ConfigAdminSettingRegistry.Settings.Where(setting =>
                setting.Key.EndsWith("Permission", StringComparison.Ordinal) || setting.Key.EndsWith("Privilege", StringComparison.Ordinal))
            .Should().OnlyContain(setting => setting.IsPrivilege);
        privilegeFields.Should().OnlyContain(setting => setting.Kind == ConfigAdminSettingKind.Text);
        foreach (var setting in privilegeFields)
        {
            setting.TrySetValue(config, "Custom.Unregistered value ", out var error).Should().BeTrue(error);
            setting.GetValue(config).Should().Be("Custom.Unregistered value ");
        }
        ConfigAdminSettingRegistry.Settings.Single(setting => setting.Key == "ProseNicknameToken").IsPrivilege.Should().BeFalse();
    }

    [Fact]
    public void SnapshotMessagesRoundtripPrivilegeCodesAndDescriptions()
    {
        List<ConfigAdminPrivilegeSuggestion> suggestions = [new()
        {
            Code = "custom.CaseSensitive",
            Description = "Use the custom tool."
        }];
        var open = Serializer.DeepClone(new TheBasicsConfigAdminOpenMessage { PrivilegeSuggestions = suggestions });
        var result = Serializer.DeepClone(new TheBasicsConfigAdminResultMessage { PrivilegeSuggestions = suggestions });
        var wizard = Serializer.DeepClone(new TheBasicsSetupWizardResultMessage { PrivilegeSuggestions = suggestions });

        open.PrivilegeSuggestions.Should().BeEquivalentTo(suggestions);
        result.PrivilegeSuggestions.Should().BeEquivalentTo(suggestions);
        wizard.PrivilegeSuggestions.Should().BeEquivalentTo(suggestions);
    }
}
