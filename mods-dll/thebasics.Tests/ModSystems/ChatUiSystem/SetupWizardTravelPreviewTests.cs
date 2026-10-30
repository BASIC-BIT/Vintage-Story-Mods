using Cairo;
using TheBasics.GuiPreview;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.Tests.GuiPreview;
using Vintagestory.API.Client;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class SetupWizardTravelPreviewTests
{
    [Fact]
    public void EveryTravelPageHasAnExampleWithoutChangingTheDraft()
    {
        var draft = Draft();
        var values = draft.Values.Select(value => (value.Key, value.Value)).ToArray();
        foreach (var page in SetupWizardCatalog.Pages.Where(page => page.Id.StartsWith("teleport.", StringComparison.Ordinal)))
        {
            Assert.True(SetupWizardTravelPreview.Supports(page.Id));
            Assert.NotEmpty(SetupWizardTravelPreview.Message(page.Id, draft, 2));
        }
        Assert.Equal(values, draft.Values.Select(value => (value.Key, value.Value)));
        Assert.False(draft.IsDirty);
        Assert.False(SetupWizardTravelPreview.Supports("chat.basics"));
        Assert.Empty(SetupWizardTravelPreview.Message("chat.basics", draft, 0));
    }

    [Theory]
    [InlineData("teleport.homes", "Teleportation.RegisterHomeCommands", "home")]
    [InlineData("teleport.spawn", "Teleportation.RegisterSpawnCommands", "spawn")]
    [InlineData("teleport.top", "Teleportation.RegisterTopCommand", "top")]
    [InlineData("teleport.back", "Teleportation.RegisterBackCommand", "back")]
    [InlineData("teleport.stuck", "Teleportation.RegisterStuckCommand", "stuck")]
    public void DisabledCommandsDoNotDemonstrateSuccessfulTravel(string page, string key, string command)
    {
        var draft = Draft();
        draft.Set(key, "0");
        Assert.Equal("/" + command + " commands are off.", SetupWizardTravelPreview.Message(page, draft, 7));
    }

    [Fact]
    public void ZeroWarmupSkipsTheWaitAndGearIsPaidAtTheCorrectStage()
    {
        var draft = Draft();
        draft.Set("Teleportation.RegisterHomeCommands", "1");
        draft.Set("HomeSpawnRequireTemporalGear", "1");
        draft.Set("Teleportation.HomeWarmupSeconds", "5");
        Assert.Contains("gear will be spent on arrival", SetupWizardTravelPreview.Message("teleport.homes", draft, 2));
        Assert.Contains("Stand still for 3 seconds", SetupWizardTravelPreview.Message("teleport.homes", draft, 2));
        draft.Set("Teleportation.HomeWarmupSeconds", "0");
        Assert.DoesNotContain("Stand still", SetupWizardTravelPreview.Message("teleport.homes", draft, 0));
        Assert.Equal("Teleporting...", SetupWizardTravelPreview.Message("teleport.homes", draft, 0));
        Assert.Contains("Teleported to home", SetupWizardTravelPreview.Message("teleport.homes", draft, 0.5));

        draft.Set("TpaRequireTemporalGear", "1");
        draft.Set("Teleportation.TpaWarmupSeconds", "5");
        Assert.Contains("gear paid with the request", SetupWizardTravelPreview.Message("teleport.requests", draft, 0));
        Assert.Contains("accepted", SetupWizardTravelPreview.Message("teleport.requests", draft, 2));
        Assert.Contains("gear already paid", SetupWizardTravelPreview.Message("teleport.requests", draft, 2));
        draft.Set("Teleportation.TpaWarmupSeconds", "0");
        Assert.DoesNotContain("Stand still", SetupWizardTravelPreview.Message("teleport.requests", draft, 1));
    }

    [Fact]
    public void RequestExpiryReturnsTheGearAndKeepsCalendarUnitsDistinct()
    {
        var draft = Draft();
        draft.Set("TpaUseTimeout", "1");
        draft.Set("TpaTimeoutMinutes", "0.1");
        draft.Set("TpaRequireTemporalGear", "1");
        draft.Set("TpaUseCooldown", "1");
        draft.Set("TpaCooldownInGameHours", "0.5");
        var pending = SetupWizardTravelPreview.Message("teleport.requesttiming", draft, 5.5);
        Assert.Contains("0.5 real seconds", pending);
        Assert.Contains("0.5 in-game hours", pending);
        Assert.DoesNotContain("expired", pending);
        var expired = SetupWizardTravelPreview.Message("teleport.requesttiming", draft, 6);
        Assert.Contains("expired", expired);
        Assert.Contains("gear returned", expired);
        draft.Set("TpaUseTimeout", "0");
        Assert.Contains("no expiry", SetupWizardTravelPreview.Message("teleport.requesttiming", draft, 6));
        draft.Set("TpaUseCooldown", "0");
        Assert.Contains("No request cooldown", SetupWizardTravelPreview.Message("teleport.requesttiming", draft, 6));
    }

    [Fact]
    public void MovementAlwaysCancelsWhileDamageAndInteractionFollowTheirSettings()
    {
        var draft = Draft();
        draft.Set("Teleportation.CancelWarmupOnDamage", "0");
        draft.Set("Teleportation.CancelWarmupOnInteraction", "0");
        Assert.Contains("cancelled because you moved", SetupWizardTravelPreview.Message("teleport.warmup", draft, 3));
        Assert.Contains("Teleported", SetupWizardTravelPreview.Message("teleport.warmup", draft, 7));
        Assert.Contains("Teleported", SetupWizardTravelPreview.Message("teleport.warmup", draft, 11));
        draft.Set("Teleportation.CancelWarmupOnDamage", "1");
        Assert.Contains("cancelled because you took damage", SetupWizardTravelPreview.Message("teleport.warmup", draft, 7));
        draft.Set("Teleportation.CancelWarmupOnInteraction", "1");
        Assert.Contains("cancelled because you interacted", SetupWizardTravelPreview.Message("teleport.warmup", draft, 11));
    }

    [Fact]
    public void HomeLimitsAndBackExpiryHaveConcreteExamples()
    {
        var draft = Draft();
        draft.Set("Teleportation.RegisterHomeCommands", "1");
        draft.Set("Teleportation.MaxHomes", "7");
        Assert.Contains("Saved homes: 7 of 7", SetupWizardTravelPreview.Message("teleport.homes", draft, 2));
        draft.Set("Teleportation.RegisterBackCommand", "1");
        draft.Set("Teleportation.BackExpiresAfterSeconds", "29");
        Assert.Contains("location has expired", SetupWizardTravelPreview.Message("teleport.back", draft, 2));
        draft.Set("Teleportation.BackExpiresAfterSeconds", "30");
        Assert.DoesNotContain("location has expired", SetupWizardTravelPreview.Message("teleport.back", draft, 2));
        draft.Set("Teleportation.BackExpiresAfterSeconds", "0");
        Assert.Contains("do not expire", SetupWizardTravelPreview.Message("teleport.back", draft, 7));
    }

    [Fact]
    public void StaffBlockStuckButNoGearIsEverRequiredForStuck()
    {
        var draft = Draft();
        draft.Set("Teleportation.RegisterStuckCommand", "1");
        draft.Set("HomeSpawnRequireTemporalGear", "1");
        Assert.Contains("admin is online", SetupWizardTravelPreview.Message("teleport.stuckpolicy", draft, 2));
        draft.Set("Teleportation.StuckBlockedByOnlinePrivilege", "");
        Assert.Contains("Stand still for 298 seconds", SetupWizardTravelPreview.Message("teleport.stuckpolicy", draft, 2));
        Assert.DoesNotContain("gear", SetupWizardTravelPreview.Message("teleport.stuckpolicy", draft, 2));
        draft.Set("Teleportation.StuckReminderIntervalSeconds", "30");
        Assert.Contains("Reminders every 30 real seconds", SetupWizardTravelPreview.Message("teleport.stuck", draft, 2));
        draft.Set("Teleportation.StuckReminderIntervalSeconds", "0");
        Assert.DoesNotContain("Reminders", SetupWizardTravelPreview.Message("teleport.stuck", draft, 2));
    }

    [VisualTheory]
    [InlineData(130)]
    [InlineData(180)]
    public void SceneIsDeterministicAndCancellationChangesTheTravelerDestination(int height)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, 1);
        var draft = Draft();
        using var cancelled = Render(draft, height);
        using var repeated = Render(draft, height);
        Assert.Equal(cancelled.Data, repeated.Data);
        draft.Set("Teleportation.CancelWarmupOnDamage", "0");
        using var arrived = Render(draft, height);
        Assert.False(cancelled.Data.SequenceEqual(arrived.Data));
        Assert.Contains(arrived.Data.Where((_, index) => index % 4 == 3), alpha => alpha > 0);
        Assert.Empty(host.UnsupportedCalls);
        Assert.Empty(host.RecordedEffects);
    }

    [VisualTheory]
    [InlineData(130)]
    [InlineData(180)]
    public void TravelerAndArrivalRaysStayInsideTheSceneAtEveryDestination(int height)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, 1);
        var draft = Draft();
        foreach (var command in new[] { "HomeCommands", "SpawnCommands", "TopCommand", "BackCommand", "StuckCommand" })
            draft.Set("Teleportation.Register" + command, "1");
        foreach (var page in new[] { "teleport.top", "teleport.tools", "teleport.utilities" })
            foreach (var time in new[] { 0d, 6d, 7d, 15d, 23d })
            {
                using var scene = Render(draft, height, page, time);
                Assert.All(scene.Data[..scene.Stride].Where((_, index) => index % 4 == 3), alpha => Assert.Equal(0, alpha));
                Assert.All(scene.Data[^scene.Stride..].Where((_, index) => index % 4 == 3), alpha => Assert.Equal(0, alpha));
            }
        Assert.Empty(host.UnsupportedCalls);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-10)]
    [InlineData(double.MaxValue)]
    public void NonFiniteOrUnboundedTimeDoesNotEscapeTheExample(double time)
    {
        var draft = Draft();
        foreach (var page in new[] { "teleport.tools", "teleport.utilities", "teleport.warmup" })
            Assert.NotEmpty(SetupWizardTravelPreview.Message(page, draft, time));
    }

    private static SetupWizardDraft Draft() => new(SetupWizardCaptureScenes.DefaultValues());
    private static ImageSurface Render(SetupWizardDraft draft, int height, string page = "teleport.warmup", double time = 7)
    {
        var surface = new ImageSurface(Format.Argb32, 310, height);
        using var context = new Context(surface);
        SetupWizardTravelPreview.Draw(context, surface, ElementBounds.FixedSize(310, height), page, draft, time);
        surface.Flush();
        return surface;
    }
}
