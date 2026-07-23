using FluentAssertions;
using NSubstitute;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat;
using thebasics.ModSystems.ProximityChat.Models;
using Vintagestory.API.Server;

namespace thebasics.Tests.ModSystems.ProximityChat;

public class LanguageReconciliationTests
{
    [Fact]
    public void Catalog_IncludesBuiltInSignAndProtectsItsNameAndPrefix()
    {
        var config = CreateConfig(
            new Language("Babble", "Reserved name", "other", [], "#000000"),
            new Language("Sign", "Reserved name", "gesture", [], "#000000"),
            new Language("Gesture", "Reserved prefix", "sign", [], "#000000"));

        var languages = LanguageCatalog.GetAll(config, allowBabble: false);

        languages.Should().ContainSingle(language => language == LanguageSystem.SignLanguage);
        languages.Should().NotContain(language => language.Name == "Babble" || language.Name == "Gesture");
        languages.Should().Contain(language => language.Name == "Common");
    }

    [Fact]
    public void Catalog_IsCaseInsensitiveAndDoesNotLetRejectedDuplicatesPoisonLaterEntries()
    {
        var config = CreateConfig(
            new Language("Trade", "First", "trade", [], "#000000"),
            new Language("Discarded", "Duplicate prefix", "TRADE", [], "#000000"),
            new Language("Discarded", "Usable after rejected duplicate", "discarded", [], "#000000"));

        var languages = LanguageCatalog.GetAll(config, allowBabble: false);

        languages.Select(language => language.Name).Should().Contain(["Common", "Trade", "Discarded", "Sign"]);
    }

    [Fact]
    public void Catalog_HandlesMissingConfiguredLanguages()
    {
        var config = new ModConfig { Languages = null! };

        var languages = LanguageCatalog.GetAll(config, allowBabble: true);

        languages.Should().Equal(LanguageSystem.SignLanguage, LanguageSystem.BabbleLang);
    }

    [Fact]
    public void ReconcileLanguageNames_PreservesSignAheadOfStaleRenameAndCanonicalizesCase()
    {
        var languagesByName = LanguageCatalog.GetReconciliationMap(CreateConfig());
        var renameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Sign"] = "Gesture",
            ["Old Trade"] = "Trade"
        };

        var reconciled = RPProximityChatSystem.ReconcileLanguageNames(
            ["sign", "Old Trade", "Missing", "SIGN"],
            renameMap,
            languagesByName);

        reconciled.Should().Equal("Sign", "Trade");
    }

    [Fact]
    public void ReconcileLanguageNames_AppliesConfiguredRenameWhenOldNameIsReused()
    {
        var languagesByName = LanguageCatalog.GetReconciliationMap(CreateConfig(
            new Language("Old Trade", "Replacement language", "old", ["ol"], "#E9DDCE")));
        var renameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Old Trade"] = "Trade"
        };

        var reconciled = RPProximityChatSystem.ReconcileLanguageNames(
            ["Old Trade"],
            renameMap,
            languagesByName);

        reconciled.Should().Equal("Trade");
    }

    [Fact]
    public void ResolveDefault_RequiresKnownLanguageAndFallsBackToFirstKnown()
    {
        var languagesByName = LanguageCatalog.GetReconciliationMap(CreateConfig());

        var resolved = RPProximityChatSystem.ResolveReconciledDefaultLanguage(
            "Trade",
            new Dictionary<string, string>(),
            languagesByName,
            ["Sign"]);

        resolved.Should().BeSameAs(LanguageSystem.SignLanguage);
    }

    [Fact]
    public void ResolveDefault_UsesBabbleWhenPlayerKnowsNoLanguages()
    {
        var resolved = RPProximityChatSystem.ResolveReconciledDefaultLanguage(
            "Sign",
            new Dictionary<string, string>(),
            LanguageCatalog.GetReconciliationMap(CreateConfig()),
            []);

        resolved.Should().BeSameAs(LanguageSystem.BabbleLang);
    }

    [Fact]
    public void ReconcilePlayerLanguages_PersistsSignAndSignDefaultAcrossJoin()
    {
        var player = CreatePlayer();
        player.SetLanguages(["Sign"]);
        player.SetDefaultLanguage(LanguageSystem.SignLanguage);

        RPProximityChatSystem.ReconcilePlayerLanguages(
            player,
            CreateConfig(),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Sign"] = "Gesture"
            });

        player.GetLanguages().Should().Equal("Sign");
        player.GetDefaultLanguageName().Should().Be("Sign");
        player.GetDefaultLanguage(CreateConfig()).Should().BeSameAs(LanguageSystem.SignLanguage);
    }

    [Fact]
    public void ReconcilePlayerLanguages_InitializesMissingLegacyState()
    {
        var player = CreatePlayer();

        RPProximityChatSystem.ReconcilePlayerLanguages(
            player,
            CreateConfig(),
            new Dictionary<string, string>());

        player.GetLanguages().Should().Equal("Common");
        player.GetDefaultLanguageName().Should().Be("Common");
    }

    [Fact]
    public void LanguageMutations_AreCaseInsensitive()
    {
        var player = CreatePlayer();
        player.SetLanguages(["sign"]);

        player.KnowsLanguage(LanguageSystem.SignLanguage).Should().BeTrue();
        player.AddLanguage(LanguageSystem.SignLanguage);
        player.GetLanguages().Should().ContainSingle();

        player.RemoveLanguage(LanguageSystem.SignLanguage);
        player.GetLanguages().Should().BeEmpty();
    }

    [Fact]
    public void DefaultLanguageLookup_IsCaseInsensitiveForSign()
    {
        var player = CreatePlayer();
        IServerPlayerExtensions.SetModData(player, "BASIC_DEFAULT_LANGUAGE", "sIgN");

        player.GetDefaultLanguage(CreateConfig()).Should().BeSameAs(LanguageSystem.SignLanguage);
    }

    private static ModConfig CreateConfig(params Language[] additionalLanguages)
    {
        return new ModConfig
        {
            Languages = new List<Language>
            {
                new("Common", "The universal language", "c", ["al"], "#E9DDCE", true),
                new("Trade", "A trade language", "trade", ["tar"], "#D4A96A")
            }.Concat(additionalLanguages).ToList()
        };
    }

    private static IServerPlayer CreatePlayer()
    {
        var player = Substitute.For<IServerPlayer>();
        var modData = new Dictionary<string, byte[]>();
        player.GetModdata(Arg.Any<string>()).Returns(call =>
            modData.TryGetValue(call.Arg<string>(), out var value) ? value : null);
        player.When(call => call.SetModdata(Arg.Any<string>(), Arg.Any<byte[]>()))
            .Do(call =>
            {
                var key = call.ArgAt<string>(0);
                var value = call.ArgAt<byte[]>(1);
                if (value == null)
                {
                    modData.Remove(key);
                }
                else
                {
                    modData[key] = value;
                }
            });
        player.When(call => call.RemoveModdata(Arg.Any<string>()))
            .Do(call => modData.Remove(call.ArgAt<string>(0)));
        return player;
    }
}
