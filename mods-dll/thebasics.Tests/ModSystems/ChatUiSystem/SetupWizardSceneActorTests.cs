using thebasics.ModSystems.ChatUiSystem;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

public class SetupWizardSceneActorTests
{
    [Theory]
    [InlineData("teleport.homes", "Teleportation.HomeWarmupSeconds", 0d, 0.5)]
    [InlineData("teleport.homes", "Teleportation.HomeWarmupSeconds", 5d, 5.5)]
    [InlineData("teleport.requests", "Teleportation.TpaWarmupSeconds", 5d, 6.5)]
    [InlineData("teleport.stuck", "Teleportation.StuckWarmupSeconds", 300d, 300.5)]
    [InlineData("teleport.warmup", "Teleportation.HomeWarmupSeconds", 2d, 6.5)]
    public void ArrivalStartsTheWaveAtZeroAfterTheWait(string page, string warmupKey, double warmup, double arrival)
    {
        var draft = Draft();
        draft.Set("Teleportation.RegisterHomeCommands", "1");
        draft.Set("Teleportation.RegisterStuckCommand", "1");
        draft.Set("AllowPlayerTpa", "1");
        draft.Set("Teleportation.CancelWarmupOnDamage", "0");
        draft.Set(warmupKey, warmup.ToString(System.Globalization.CultureInfo.InvariantCulture));

        var start = Assert.Single(SetupWizardTravelPreview.Actors(page, draft, arrival, 310, 180));
        Assert.Equal("wave", start.Animation);
        Assert.Equal(0, start.AnimationTime);
        var next = Assert.Single(SetupWizardTravelPreview.Actors(page, draft, arrival + 0.25, 310, 180));
        Assert.Equal("wave", next.Animation);
        Assert.Equal(0.25, next.AnimationTime);
    }

    [Theory]
    [InlineData(130, 10)]
    [InlineData(180, 60)]
    public void TravelActorMovesOnlyWhenTheSameExampleReportsArrival(int height, double top)
    {
        var draft = Draft();
        draft.Set("Teleportation.RegisterHomeCommands", "1");
        draft.Set("Teleportation.HomeWarmupSeconds", "2");

        var waiting = Assert.Single(SetupWizardTravelPreview.Actors("teleport.homes", draft, 1, 310, height));
        Assert.Contains("Stand still", SetupWizardTravelPreview.Message("teleport.homes", draft, 1));
        Assert.Equal((44d, top, 42d, 70d), (waiting.X, waiting.Y, waiting.Width, waiting.Height));
        Assert.Equal("idle", waiting.Animation);

        var warping = Assert.Single(SetupWizardTravelPreview.Actors("teleport.homes", draft, 2, 310, height));
        Assert.Equal("Teleporting...", SetupWizardTravelPreview.Message("teleport.homes", draft, 2));
        Assert.Equal(waiting.X, warping.X);
        Assert.Equal("idle", warping.Animation);

        var arrived = Assert.Single(SetupWizardTravelPreview.Actors("teleport.homes", draft, 2.5, 310, height));
        Assert.Contains("Teleported to home", SetupWizardTravelPreview.Message("teleport.homes", draft, 2.5));
        Assert.Equal((223d, top), (arrived.X, arrived.Y));
        Assert.Equal("wave", arrived.Animation);
        Assert.Equal("pip", arrived.Slot);
        Assert.Equal("Pip", arrived.Identity);
    }

    [Theory]
    [InlineData(130, 10)]
    [InlineData(180, 37)]
    public void TopArrivalUsesTheRaisedDestinationOnlyInTheTallScene(int height, double top)
    {
        var draft = Draft();
        draft.Set("Teleportation.RegisterTopCommand", "1");
        draft.Set("Teleportation.TopWarmupSeconds", "0");
        var actor = Assert.Single(SetupWizardTravelPreview.Actors("teleport.top", draft, 0.5, 310, height));
        Assert.Equal((223d, top), (actor.X, actor.Y));
        Assert.Equal("wave", actor.Animation);
    }

    [Fact]
    public void TravelOpacityAndCancellationFollowTheScenePhase()
    {
        var draft = Draft();
        draft.Set("Teleportation.RegisterHomeCommands", "1");
        draft.Set("Teleportation.HomeWarmupSeconds", "2");
        var warp = Assert.Single(SetupWizardTravelPreview.Actors("teleport.homes", draft, 2.25, 310, 180));
        Assert.Equal((float)(Math.Sqrt(0.5)), warp.Opacity, 5);
        Assert.Equal(2.25, warp.AnimationTime);

        draft.Set("Teleportation.RegisterHomeCommands", "0");
        var disabled = Assert.Single(SetupWizardTravelPreview.Actors("teleport.homes", draft, 2.25, 310, 180));
        Assert.Equal(0.55f, disabled.Opacity);
        Assert.Equal("idle", disabled.Animation);
        Assert.Equal(44, disabled.X);

        draft.Set("Teleportation.RegisterStuckCommand", "1");
        draft.Set("Teleportation.StuckBlockedByOnlinePrivilege", "controlserver");
        Assert.Equal(0.55f, Assert.Single(SetupWizardTravelPreview.Actors("teleport.stuckpolicy", draft, 2, 310, 180)).Opacity);
        draft.Set("Teleportation.RegisterBackCommand", "1");
        draft.Set("Teleportation.BackExpiresAfterSeconds", "29");
        Assert.Equal(0.55f, Assert.Single(SetupWizardTravelPreview.Actors("teleport.back", draft, 2, 310, 180)).Opacity);

        var cancelled = Assert.Single(SetupWizardTravelPreview.Actors("teleport.warmup", draft, 3, 310, 180));
        Assert.Contains("cancelled because you moved", SetupWizardTravelPreview.Message("teleport.warmup", draft, 3));
        Assert.Equal((52d, 60d), (cancelled.X, cancelled.Y));
        Assert.Equal("idle", cancelled.Animation);

        draft.Set("Teleportation.CancelWarmupOnDamage", "0");
        var continued = Assert.Single(SetupWizardTravelPreview.Actors("teleport.warmup", draft, 6.25, 310, 180));
        Assert.Equal("Teleporting...", SetupWizardTravelPreview.Message("teleport.warmup", draft, 6.25));
        Assert.Equal(2.25, continued.AnimationTime);
        Assert.Equal(warp.Opacity, continued.Opacity);
    }

    [Theory]
    [InlineData(130, 0, 53.2, 93.2, 4.5)]
    [InlineData(130, 4, 69, 113.8, 38.1)]
    [InlineData(130, 8, 60.2, 100.2, 4.5)]
    [InlineData(180, 0, 53.2, 93.2, 29.5)]
    [InlineData(180, 4, 69, 113.8, 63.1)]
    [InlineData(180, 8, 60.2, 100.2, 29.5)]
    public void SpeechActorsShareTheReadableBillboardPlacement(int height, double time, double speakerX, double listenerX, double listenerY)
    {
        var draft = Draft();
        foreach (var page in new[] { "chat.ranges", "chat.obfuscation", "chat.signlanguage" })
        {
            var actors = SetupWizardSpeechPreview.Actors(page, draft, time, 310, height);
            Assert.Equal(2, actors.Length);
            Assert.Equal(("pip", "Pip", (height - 25) / 2d - 48, 30d, 48d),
                (actors[0].Slot, actors[0].Identity, actors[0].Y, actors[0].Width, actors[0].Height));
            Assert.Equal(speakerX, actors[0].X, 5);
            Assert.Equal("mara", actors[1].Slot);
            Assert.Equal("Mara", actors[1].Identity);
            Assert.Equal(listenerX, actors[1].X, 5);
            Assert.Equal(listenerY, actors[1].Y, 5);
            Assert.All(actors, actor => Assert.Equal(1f, actor.Opacity));
        }
    }

    [Theory]
    [InlineData(130)]
    [InlineData(180)]
    public void NearbySpeechBillboardsSeparateWhileGroundDotsKeepTheTrueThreeBlockDistance(int height)
    {
        var draft = Draft();
        var listener = SetupWizardSpeechPreview.Listener(0.75);
        var positions = SetupWizardSpeechPreview.PlaceActors(listener, height);
        Assert.Equal(84, positions.Speaker.MapX);
        Assert.Equal(92.4, positions.Listener.MapX, 5);
        Assert.Equal((height - 25) / 2d, positions.Speaker.MapY);
        Assert.Equal(positions.Speaker.MapY, positions.Listener.MapY);
        Assert.Equal(40, positions.Listener.CenterX - positions.Speaker.CenterX, 5);
        Assert.Equal(3, listener.GridDistance);
        Assert.Equal("3 blocks away", SetupWizardSpeechPreview.DistanceLabel("chat.ranges", listener));

        foreach (var page in new[] { "chat.ranges", "chat.obfuscation", "chat.signlanguage" })
        {
            var actors = SetupWizardSpeechPreview.Actors(page, draft, 0.75, 310, height);
            Assert.True(actors[0].X + actors[0].Width <= actors[1].X);
            Assert.Equal(positions.Speaker.CenterX, actors[0].X + actors[0].Width / 2, 5);
            Assert.Equal(positions.Listener.CenterX, actors[1].X + actors[1].Width / 2, 5);
            Assert.Equal(SetupWizardSpeechPreview.Message(page, draft, 0),
                SetupWizardSpeechPreview.Message(page, draft, 0.75));
        }
    }

    [Fact]
    public void ActorSamplingIsLocalAndRejectsPagesWithoutAScene()
    {
        var draft = Draft();
        var values = draft.Values.Select(value => (value.Key, value.Value)).ToArray();
        Assert.Empty(SetupWizardTravelPreview.Actors("chat.ranges", draft, 0, 310, 180));
        Assert.Empty(SetupWizardSpeechPreview.Actors("chat.language", draft, 0, 310, 180));
        Assert.Empty(SetupWizardSpeechPreview.Actors("chat.ranges", null!, 0, 310, 180));
        Assert.Empty(SetupWizardTravelPreview.Actors("teleport.homes", null!, 0, 310, 180));
        foreach (var time in new[] { -10d, double.NaN, double.PositiveInfinity })
        {
            Assert.Equal(SetupWizardTravelPreview.Actors("teleport.homes", draft, 0, 310, 180),
                SetupWizardTravelPreview.Actors("teleport.homes", draft, time, 310, 180));
            Assert.Equal(SetupWizardSpeechPreview.Actors("chat.ranges", draft, 0, 310, 180),
                SetupWizardSpeechPreview.Actors("chat.ranges", draft, time, 310, 180));
        }
        Assert.InRange(Assert.Single(SetupWizardTravelPreview.Actors("teleport.homes", draft, double.MaxValue, 310, 180)).AnimationTime, 0, 60);
        Assert.All(SetupWizardSpeechPreview.Actors("chat.ranges", draft, double.MaxValue, 310, 180),
            actor => Assert.InRange(actor.AnimationTime, 0, 60));
        Assert.Equal(SetupWizardSpeechPreview.Actors("chat.ranges", draft, 0, 310, 180),
            SetupWizardSpeechPreview.Actors("chat.ranges", draft, 60, 310, 180));
        foreach (var size in new[] { 0d, -1, double.NaN, double.PositiveInfinity })
        {
            Assert.Empty(SetupWizardTravelPreview.Actors("teleport.homes", draft, 0, size, 180));
            Assert.Empty(SetupWizardSpeechPreview.Actors("chat.ranges", draft, 0, 310, size));
        }
        Assert.Equal(values, draft.Values.Select(value => (value.Key, value.Value)));
        Assert.False(draft.IsDirty);
    }

    private static SetupWizardDraft Draft() => new(SetupWizardCaptureScenes.DefaultValues());
}
