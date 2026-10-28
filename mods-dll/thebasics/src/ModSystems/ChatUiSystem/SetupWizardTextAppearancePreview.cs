using System;
using System.Collections.Generic;
using System.Linq;
using thebasics.Configs;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.ModSystems.ProximityChat.Transformers;
using thebasics.Utilities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
namespace thebasics.ModSystems.ChatUiSystem;

internal static class SetupWizardTextAppearancePreview
{
    internal static ModConfig Config(SetupWizardDraft draft)
    {
        var config = new ModConfig(); config.InitializeDefaultsIfNeeded();
        ConfigAdminSaveWorkflow.ApplyValues(config, draft.Values);
        return config;
    }

    internal static string[] Messages(SetupWizardDraft draft)
    {
        var config = Config(draft);
        var nickname = NameTransformer.FormatName("Pip", "#91C5DF", true, config);
        var account = NameTransformer.FormatName("PipPlayer", "#91C5DF", false, config);
        var lines = new List<string> { nickname + ": Meet me by the gate.", account + ": Back in a minute." };
        lines.Add("<i>Pip: The market is open.<br>");
        lines.Add("Mara: Meet you by the bridge.<br>");
        foreach (var mode in new[] { ProximityChatMode.Whisper, ProximityChatMode.Normal, ProximityChatMode.Yell })
        {
            var label = mode == ProximityChatMode.Normal ? "Talk" : mode.ToString();
            string Sample(double distance) => config.EnableDistanceFontSizeSystem
                ? $"<font size=\"{DistanceFontSizeTransformer.CalculateFontSize(config, mode, distance)}\">Meet me by the gate.</font>"
                : "Meet me by the gate.";
            var far = ModConfig.IsUnlimitedRange(config.GetModeDistance(mode)) ? 28 : Math.Max(1, (int)(config.GetModeDistance(mode) * .8));
            lines.Add(label + " at 1 block: " + Sample(1));
            lines.Add(label + " at " + far + " blocks: " + Sample(far));
        }
        return lines.ToArray();
    }

    internal static RichTextComponentBase[] Components(ICoreClientAPI api, SetupWizardDraft draft)
    {
        var font = CairoFont.WhiteDetailText().WithFontSize(14);
        var messages = Messages(draft);
        return Config(draft).IsolateChatMessageFormatting
            ? ChatUiSystem.BuildChatHistoryComponents(api, messages, font)
            : VtmlUtil.Richtextify(api, string.Join("\r\n", messages), font);
    }

    internal static string Sight(SetupWizardDraft draft)
    {
        var config = Config(draft);
        var glass = new Block { BlockId = 1, Code = new AssetLocation("game:glass-clear"), RenderPass = EnumChunkRenderPass.Transparent };
        var stone = new Block { BlockId = 2, Code = new AssetLocation("game:stone-granite"), RenderPass = EnumChunkRenderPass.Opaque };
        var policy = SightBlockPolicy.Resolve([glass, stone], config.SightPassThroughBlockCodePatterns, config.SightBlockingBlockCodePatterns);
        string Status(Block block) => policy.ShouldStop(block, true) ? "blocked" : "clear";
        return "Sight through glass: " + Status(glass) + "<br>Sight through stone: " + Status(stone);
    }
}
