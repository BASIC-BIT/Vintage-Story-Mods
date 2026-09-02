using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace thebasics.Utilities;

/// <summary>
/// Resolves server-configured sight overrides once, then answers visibility checks by block ID.
/// Rendering metadata is a useful default, but cannot describe custom or element-level rendering.
/// </summary>
internal sealed class SightBlockPolicy
{
    internal const int MaxConflictExamples = 10;

    private readonly HashSet<int> _passThroughBlockIds;
    private readonly HashSet<int> _blockingBlockIds;

    private SightBlockPolicy(
        HashSet<int> passThroughBlockIds,
        HashSet<int> blockingBlockIds,
        bool requiresNoBoxFallback,
        IReadOnlyList<string> unmatchedPatterns,
        IReadOnlyList<string> conflictingBlockCodes,
        int conflictingBlockCodeCount)
    {
        _passThroughBlockIds = passThroughBlockIds;
        _blockingBlockIds = blockingBlockIds;
        RequiresNoBoxFallback = requiresNoBoxFallback;
        UnmatchedPatterns = unmatchedPatterns;
        ConflictingBlockCodes = conflictingBlockCodes;
        ConflictingBlockCodeCount = conflictingBlockCodeCount;
        GeneralFilter = (_, block) => ShouldStop(block, foliagePasses: true);
        StrictFilter = (_, block) => ShouldStop(block, foliagePasses: false);
    }

    public IReadOnlyList<string> UnmatchedPatterns { get; }

    public IReadOnlyList<string> ConflictingBlockCodes { get; }

    public int ConflictingBlockCodeCount { get; }

    public bool HasBlockingOverrides => _blockingBlockIds.Count > 0;

    public bool RequiresNoBoxFallback { get; }

    public BlockFilter GeneralFilter { get; }

    public BlockFilter StrictFilter { get; }

    public static SightBlockPolicy Resolve(
        IEnumerable<Block> blocks,
        IEnumerable<string> passThroughPatterns,
        IEnumerable<string> blockingPatterns)
    {
        var blockArray = (blocks ?? Array.Empty<Block>()).Where(block => block?.Code != null && block.Id != 0).ToArray();
        var unmatched = new List<string>();
        var passThrough = ResolveBlockIds(blockArray, passThroughPatterns, unmatched);
        var blocking = ResolveBlockIds(blockArray, blockingPatterns, unmatched);
        var conflictingCodes = blockArray
            .Where(block => passThrough.Contains(block.Id) && blocking.Contains(block.Id))
            .Select(block => block.Code.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var requiresNoBoxFallback = blockArray.Any(block =>
            blocking.Contains(block.Id) && !string.IsNullOrWhiteSpace(block.EntityClass));

        return new SightBlockPolicy(
            passThrough,
            blocking,
            requiresNoBoxFallback,
            unmatched.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            conflictingCodes.Take(MaxConflictExamples).ToArray(),
            conflictingCodes.Length);
    }

    public bool IsExplicitlyBlocking(Block block) => block != null && _blockingBlockIds.Contains(block.Id);

    public bool ShouldStop(Block block, bool foliagePasses)
    {
        if (block == null || block.Id == 0)
        {
            return false;
        }

        if (_blockingBlockIds.Contains(block.Id))
        {
            return true;
        }

        if (_passThroughBlockIds.Contains(block.Id))
        {
            return false;
        }

        if (foliagePasses && block.BlockMaterial is EnumBlockMaterial.Leaves or EnumBlockMaterial.Plant)
        {
            return false;
        }

        return block.RenderPass is not (EnumChunkRenderPass.Transparent
                                     or EnumChunkRenderPass.BlendNoCull
                                     or EnumChunkRenderPass.Liquid);
    }

    private static HashSet<int> ResolveBlockIds(Block[] blocks, IEnumerable<string> patterns, List<string> unmatched)
    {
        var result = new HashSet<int>();
        foreach (var configuredPattern in (patterns ?? Array.Empty<string>()).Where(pattern => !string.IsNullOrWhiteSpace(pattern)))
        {
            var patternText = configuredPattern.Trim();
            var pattern = new AssetLocation(patternText);
            if (patternText.IndexOf(':') <= 0 || !pattern.Valid)
            {
                unmatched.Add(patternText);
                continue;
            }

            var matched = false;
            foreach (var block in blocks.Where(block => WildcardUtil.Match(pattern, block.Code)))
            {
                matched = true;
                result.Add(block.Id);
            }

            if (!matched)
            {
                unmatched.Add(pattern.ToString());
            }
        }

        return result;
    }
}
