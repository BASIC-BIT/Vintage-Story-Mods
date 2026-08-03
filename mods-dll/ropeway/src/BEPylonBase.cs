using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Ropeway;

/// <summary>
/// Per-tower state: the multiblock validation tick and the 0-2 spans this tower carries.
/// Modelled on BlockEntityBeeHiveKiln - Initialize loads the structure, Init sets up the tick and the
/// rotation, FromTreeAttributes re-Inits on the client.
/// <para>
/// This lives on the GROUND-PLACED footing, which is the tower's one canonical position: LoadedTowers,
/// LineCache, RopewayLine.Towers, every persisted span and the cabin's LineKey are all footing positions.
/// The sheave up on the crossarm is an inert cell of the pattern, and <see cref="SpanMath.AnchorOf"/> is
/// the only thing that turns a footing position into the height the rope actually runs at.
/// </para>
/// </summary>
public class BEPylonBase : BlockEntity
{
    public const int MaxSpansPerTower = 2;

    /// <summary>Longest name a tower keeps. A place name, not a paragraph - it has to fit a picker row.</summary>
    public const int MaxNameLength = 24;

    public bool StructureComplete;

    /// <summary>Player-set label, or null when unnamed. Sanitised on the way in, synced with the tree.</summary>
    public string TowerName;

    /// <summary>Peer towers this one is linked to. Unordered, 0-2 entries, mirrored on the peer.</summary>
    public readonly List<BlockPos> Spans = new();

    private MultiblockStructure structure;
    private MultiblockStructure highlightedStructure;
    private IPlayer highlightFor;
    private string side;

    public double MaxSpan => Block?.Attributes?["maxSpan"].AsDouble(48) ?? 48;

    public double MaxLineLength => Block?.Attributes?["maxLineLength"].AsDouble(512) ?? 512;

    public int MaxCandidates => Block?.Attributes?["maxCandidates"].AsInt(16) ?? 16;

    public double RopePerBlock => Block?.Attributes?["ropePerBlock"].AsDouble(0.25) ?? 0.25;

    /// <summary>
    /// The axis the cabin threads through the frame on, as one of its two facings. The crossarm is the
    /// structure's +/-X offsets and the frame is one block deep, so the passage runs along the tower's
    /// local Z - which the side variant names directly. With the rear gantry gone this is the only thing
    /// orientation still decides, and getting it 90 degrees out is a tower the line cannot pass through.
    /// </summary>
    public BlockFacing PassageFacing => BlockFacing.FromCode(side ?? "north") ?? BlockFacing.NORTH;

    /// <summary>A tower is an end tower - a station - iff it is complete and carries exactly one span.</summary>
    public bool IsEndpoint => StructureComplete && Spans.Count == 1;

    public RopewayModSystem ModSystem => Api?.ModLoader?.GetModSystem<RopewayModSystem>();

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);

        // AsObject<T> on a missing key yields null - a variant that forgot the attribute must not NRE.
        structure = Block?.Attributes?["multiblockStructure"]?.AsObject<MultiblockStructure>();
        side ??= Block?.Variant["side"];

        if (structure != null && side != null) Init();

        var modSystem = ModSystem;
        if (modSystem == null) return;

        modSystem.LoadedTowers[Pos.Copy()] = this;

        // A line resolved while this tower's chunk was unloaded is a truncated line. Drop it.
        modSystem.InvalidateLine(Pos);
        modSystem.LineCache.Remove(Pos);
        foreach (var peer in Spans)
        {
            modSystem.InvalidateLine(peer);
            modSystem.LineCache.Remove(peer);
        }
    }

    private void Init()
    {
        if (Api.Side == EnumAppSide.Server)
        {
            RegisterGameTickListener(OnServerTick1s, 1000, 0);
        }
        else
        {
            RegisterGameTickListener(OnClientTick500ms, 500, 0);
        }

        structure.InitForUse(side switch
        {
            "east" => 270,
            "south" => 180,
            "west" => 90,
            _ => 0
        });
    }

    private void OnServerTick1s(float dt)
    {
        Validate();
    }

    /// <summary>
    /// The overlay is the primary build-guidance channel, so it has to follow the blocks the player is
    /// placing - a one-shot snapshot leaves ghost cells glowing on top of blocks that are already there.
    /// HighlightBlocks replaces the whole slot, so re-issuing it is the entire update. Idle until someone
    /// has actually asked for highlights on this tower.
    /// </summary>
    private void OnClientTick500ms(float dt)
    {
        if (highlightFor == null) return;

        if (IncompleteCount() == 0)
        {
            ClearHighlights(highlightFor);
            return;
        }

        Highlight(highlightFor);
    }

    /// <summary>Re-runs the multiblock check and syncs the result if it changed. Server side.</summary>
    public bool Validate()
    {
        if (structure?.TransformedOffsets == null) return StructureComplete;

        var before = StructureComplete;
        StructureComplete = structure.InCompleteBlockCount(Api.World, Pos) == 0;
        if (before != StructureComplete) MarkDirty();
        return StructureComplete;
    }

    /// <summary>How many cells are missing or wrong. -1 when the structure attribute is absent.</summary>
    public int IncompleteCount()
    {
        if (structure?.TransformedOffsets == null) return -1;
        return structure.InCompleteBlockCount(Api.World, Pos);
    }

    /// <summary>
    /// Client-only build guidance: the wanted block's own colour for missing cells, red for wrong ones.
    /// HighlightIncompleteParts dereferences <c>world.Api as ICoreClientAPI</c>, so it NREs server side.
    /// </summary>
    public void ShowIncompleteParts(IPlayer byPlayer)
    {
        if (Api is not ICoreClientAPI || byPlayer == null || structure?.TransformedOffsets == null) return;

        highlightFor = byPlayer;
        Highlight(byPlayer);
    }

    private void Highlight(IPlayer byPlayer)
    {
        highlightedStructure = structure;
        try
        {
            highlightedStructure.HighlightIncompleteParts(Api.World, byPlayer, Pos);
        }
        catch (Exception e)
        {
            // HighlightIncompleteParts indexes SearchBlocks(wildcard)[0] on every missing cell, so a
            // blockNumbers key that resolves to nothing is an IndexOutOfRangeException. Never crash the
            // client over build guidance.
            highlightFor = null;
            Api.Logger.Error("Ropeway: could not highlight tower at {0}: {1}", Pos, e.Message);
        }
    }

    public void ClearHighlights(IPlayer byPlayer)
    {
        highlightFor = null;
        if (Api is ICoreClientAPI && byPlayer != null) highlightedStructure?.ClearHighlights(Api.World, byPlayer);
    }

    /// <summary>
    /// Half-thickness of the drawn cable, in blocks. Public because the cabin's jaw is authored closed on
    /// this surface with 0.04 unit of clearance, so the two drift apart silently if only one of them moves -
    /// <c>RopewayAssetContractTests.TheCabinFitsThroughTheTower</c> is what notices.
    /// </summary>
    public const float CableRadius = 0.06f;

    /// <summary>
    /// Draws this tower's half of every span it carries. Each end draws only to the midpoint rather than one
    /// end drawing the whole cable, which means there is never coincident geometry to z-fight and a span
    /// whose far chunk is unloaded still shows the half you are standing next to.
    /// ponytail: drawn by the footing and pushed up <see cref="SpanMath.SheaveHeight"/>, not by the sheave.
    /// The sheave has no block entity since the controller moved to the ground, and giving it one purely to
    /// hold a mesh would mean a second position to key spans by - which is the exact class of bug the one
    /// canonical position exists to prevent. One offset, the same constant AnchorOf uses.
    /// ponytail: straight, not sagging - the cabin travels the straight chord and IsSpanClear certifies a
    /// straight corridor, so a drawn catenary would be a cable that lies about where the cabin goes. Sag
    /// becomes worth drawing when the cabin follows a curve too, and then all three must share one function.
    /// ponytail: a long cable is one mesh hanging off one block entity, so it vanishes when its own chunk
    /// leaves the view frustum even while the cable is still on screen. Emitting per-chunk segments is the
    /// fix if that reads badly in play.
    /// </summary>
    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
    {
        var replacedDefault = base.OnTesselation(mesher, tessThreadTesselator);
        if (Spans.Count == 0 || Block == null) return replacedDefault;

        TextureAtlasPosition texPos;
        try
        {
            texPos = tessThreadTesselator.GetTextureSource(Block)?["rope"];
        }
        catch (Exception e)
        {
            // Runs on the tesselation thread - never take the chunk mesher down over a missing texture.
            Api?.Logger.Warning("Ropeway: no rope texture for the cable at {0}: {1}", Pos, e.Message);
            return replacedDefault;
        }

        if (texPos == null) return replacedDefault;

        foreach (var peer in Spans)
        {
            if (peer == null) continue;

            // Half the span. InternalY on both sides keeps this right for a tower inside a pocket dimension.
            var mesh = BuildHalfCable(
                (peer.X - Pos.X) * 0.5,
                (peer.InternalY - Pos.InternalY) * 0.5,
                (peer.Z - Pos.Z) * 0.5,
                texPos);

            if (mesh != null) mesher.AddMeshData(mesh);
        }

        return replacedDefault;
    }

    /// <summary>
    /// A thin box from this tower's sheave to the midpoint of the span, in coordinates local to the FOOTING
    /// block that draws it - hence the <see cref="SpanMath.SheaveHeight"/> in the translate, which is what
    /// makes the drawn cable and <see cref="SpanMath.AnchorOf"/> agree. Deltas are half the span, and they
    /// need no correction: both ends are footings, so footing-to-footing and sheave-to-sheave are the same
    /// vector. Null when the peer lands on top of us. Static and therefore unit-tested: this mesh's failure
    /// mode is that it renders nothing at all, silently.
    /// </summary>
    public static MeshData BuildHalfCable(double dx, double dy, double dz, TextureAtlasPosition texPos)
    {
        var length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        if (length < 0.01) return null;

        // ScaleCubeMesh does `xyz = xyz * scale + scale`, so the box lands corner-at-origin; the translate
        // argument is what puts it back around the origin the rotations below turn about.
        var mesh = CubeMeshUtil.GetCube(
            CableRadius, CableRadius, (float)(length / 2),
            new Vec3f(-CableRadius, -CableRadius, (float)(-length / 2)));

        // GetCube leaves XyzFaces empty, and the chunk tesselator emits geometry only inside
        // `for (l = 0; l < sourceMesh.XyzFacesCount; l++)` (JsonTesselator.cs:709) - so without this the
        // cable copies zero vertices into the chunk mesh, silently, with no exception and no log line.
        CubeMeshUtil.SetXyzFacesAndPacketNormals(mesh);

        // ...and once that loop runs it indexes Season/ClimateColorMapIds per face (JsonTesselator.cs:834),
        // which GetCube leaves zero-length. Without this, fixing the face count only trades an invisible
        // cable for an IndexOutOfRangeException.
        mesh.WithColorMaps();

        // Aim the box's +Z down the span: pitch about X, then yaw about Y. Two calls rather than one
        // Rotate(rx, ry, 0) so the composition order is explicit here instead of a property of Mat4f.RotateXYZ.
        SpanMath.CableAngles(dx, dy, dz, out var radX, out var radY);
        mesh.Rotate(new Vec3f(0, 0, 0), radX, 0, 0);
        mesh.Rotate(new Vec3f(0, 0, 0), 0, radY, 0);

        mesh.Translate((float)(0.5 + dx / 2), (float)(0.5 + SpanMath.SheaveHeight + dy / 2), (float)(0.5 + dz / 2));

        // Flat-sample the texture instead of letting the cube's own UVs through. ScaleCubeMesh multiplies
        // the UVs by the axis scale (CubeMeshUtil.cs:230-251), so a half-span 24 blocks long leaves them
        // running 0..48 - and SetTexPos maps u through `x1 + u * (x2 - x1)`, which puts everything past 1
        // OUTSIDE this texture's sub-region of the block atlas, sampling whatever sprites are next to it.
        // That is the striping. 0.5 lands on the middle of the sprite, far from its edges and therefore
        // safe under mipmapping; a 2-pixel-thick cable has nowhere to show lengthwise detail anyway, and
        // normalising to 0..1 instead would smear one 32x32 sprite over the whole span.
        Array.Fill(mesh.Uv, 0.5f);
        mesh.SetTexPos(texPos);
        return mesh;
    }

    public bool HasSpanTo(BlockPos peer)
    {
        if (peer == null) return false;
        for (var i = 0; i < Spans.Count; i++)
        {
            if (peer.Equals(Spans[i])) return true;
        }

        return false;
    }

    public void AddSpan(BlockPos peer)
    {
        if (peer == null || HasSpanTo(peer) || Spans.Count >= MaxSpansPerTower) return;
        Spans.Add(peer.Copy());

        // redrawOnClient: the cable is chunk mesh, so without this the span is invisible until a reload.
        MarkDirty(true);
    }

    public void RemoveSpan(BlockPos peer)
    {
        if (peer == null) return;
        for (var i = Spans.Count - 1; i >= 0; i--)
        {
            if (peer.Equals(Spans[i])) Spans.RemoveAt(i);
        }

        // redrawOnClient: otherwise a cut cable keeps hanging there until the chunk reloads.
        MarkDirty(true);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);

        StructureComplete = tree.GetBool("structureComplete");
        side = tree.GetString("side") ?? side;
        TowerName = tree.GetString("towerName");

        Spans.Clear();
        var count = tree.GetInt("spanCount");
        for (var i = 0; i < count; i++)
        {
            var peer = ReadPos(tree, "span" + i);
            if (peer != null) Spans.Add(peer);
        }

        // Operand order matters: Api is null on the first chunk-load call, so the cast check must come first.
        if (Api is ICoreClientAPI && structure != null && structure.TransformedOffsets == null && side != null)
        {
            Init();
        }

        // The cable is chunk mesh, so a Spans list that arrives after the chunk has already been tesselated
        // stays invisible until something else dirties the block. Vanilla's idiom for exactly this is
        // BlockEntityDisplay.cs:119-126. Cheap: MarkBlockDirty only queues a re-tesselation.
        if (Api is ICoreClientAPI) Api.World.BlockAccessor.MarkBlockDirty(Pos);
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);

        tree.SetBool("structureComplete", StructureComplete);
        if (side != null) tree.SetString("side", side);
        if (TowerName != null) tree.SetString("towerName", TowerName);

        tree.SetInt("spanCount", Spans.Count);
        for (var i = 0; i < Spans.Count; i++) WritePos(tree, "span" + i, Spans[i]);
    }

    /// <summary>
    /// Server-side rename. False when nothing changed, so the caller can skip the sync. MarkDirty without
    /// redrawOnClient: a name is not geometry, and re-tesselating every cable on a rename would be silly.
    /// </summary>
    public bool Rename(string raw)
    {
        var name = SanitiseName(raw);
        if (name == TowerName) return false;

        TowerName = name;
        MarkDirty();
        return true;
    }

    /// <summary>
    /// Player text arriving over the network, so this is a trust boundary: control characters corrupt the
    /// chat and GUI text renderers, and an unbounded string would be persisted verbatim on every tower and
    /// re-sent to every client in range. Null when nothing readable survives. Pure, and therefore tested.
    /// This is the single chokepoint every display path routes through, which is why the VTML strip lives
    /// here and not per surface.
    /// </summary>
    public static string SanitiseName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;

        var builder = new StringBuilder(raw.Length);
        foreach (var c in raw)
        {
            // A name reaches two VTML-rendered surfaces - GetBlockInfo's rich-text panel and the
            // span-linked / span-cut chat lines, which HudDialogChat composes with AddRichtext - and 24
            // characters is enough for <font color="red"> or a short <a href>. No tag can survive without
            // its brackets, and a place name has no use for them.
            if (c == '<' || c == '>') continue;

            // Every flavour of whitespace collapses to one plain space - a tab or a newline inside a
            // one-line GUI label is a layout bug, and runs of spaces are just padding to fake a longer name.
            if (char.IsControl(c) || char.IsWhiteSpace(c))
            {
                if (builder.Length > 0 && builder[builder.Length - 1] != ' ') builder.Append(' ');
                continue;
            }

            builder.Append(c);
        }

        var name = builder.ToString().Trim();
        if (name.Length > MaxNameLength) name = name.Substring(0, MaxNameLength).TrimEnd();

        // Cutting to a fixed length can land between the two halves of a surrogate pair, which renders as a
        // replacement glyph rather than the character the player typed.
        if (name.Length > 0 && char.IsHighSurrogate(name[name.Length - 1])) name = name.Substring(0, name.Length - 1);

        return name.Length == 0 ? null : name;
    }

    // TreeAttributeUtil.SetBlockPos writes InternalY but GetBlockPos reads it back as a plain Y with
    // dimension 0, which silently corrupts any tower inside a pocket dimension. Store the four ints.
    public static void WritePos(ITreeAttribute tree, string key, BlockPos pos)
    {
        tree.SetInt(key + "X", pos.X);
        tree.SetInt(key + "Y", pos.Y);
        tree.SetInt(key + "Z", pos.Z);
        tree.SetInt(key + "D", pos.dimension);
    }

    public static BlockPos ReadPos(ITreeAttribute tree, string key)
    {
        if (!tree.HasAttribute(key + "X")) return null;
        return new BlockPos(tree.GetInt(key + "X"), tree.GetInt(key + "Y"), tree.GetInt(key + "Z"), tree.GetInt(key + "D"));
    }

    public override void OnBlockRemoved()
    {
        // Block.OnBlockBroken is not the only way a tower dies: OnBlockExploded and every SetBlock(0, pos)
        // - worldedit, schematics, other mods - reach here instead, and would leave the peer's persisted
        // Spans naming a tower that no longer exists, forever, with nothing able to repair it.
        // Idempotent: the break path already unlinked, so UnlinkAll early-returns on an empty Spans.
        // Must precede Forget(), which drops this tower out of LoadedTowers that UnlinkAll resolves through
        // - the block itself is already air here, so the BlockAccessor fallback finds nothing.
        // refundTo null: no player on these paths, and nobody gets paid for a bomb.
        if (Api?.Side == EnumAppSide.Server) ModSystem?.LinkService?.UnlinkAll(Pos, null);

        base.OnBlockRemoved();
        Forget();
    }

    public override void OnBlockUnloaded()
    {
        base.OnBlockUnloaded();
        Forget();
    }

    private void Forget()
    {
        highlightFor = null;
        if (Api is ICoreClientAPI capi) highlightedStructure?.ClearHighlights(Api.World, capi.World.Player);

        var modSystem = ModSystem;
        if (modSystem == null) return;

        modSystem.InvalidateLine(Pos);
        modSystem.LineCache.Remove(Pos);
        modSystem.LoadedTowers.Remove(Pos);
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        base.GetBlockInfo(forPlayer, dsc);

        // No "unnamed" placeholder line: from in front of the tower there is no bearing to fall back to that
        // the player does not already have, so the honest fallback is to say nothing.
        if (TowerName != null) dsc.AppendLine(Lang.Get("ropeway:blockinfo-name", TowerName));

        if (!StructureComplete)
        {
            var missing = Math.Max(0, IncompleteCount());
            dsc.AppendLine(Lang.Get("ropeway:tower-incomplete", missing));

            // The frame is one block deep and symmetric, so nothing about a placed footing shows which way
            // its crossarm will go until the braces are up. Naming the passage axis is what lets a player
            // face the tower down the line BEFORE building it rather than after.
            dsc.AppendLine(Lang.Get(
                "ropeway:blockinfo-passage",
                Lang.Get("game:facing-" + PassageFacing.Code),
                Lang.Get("game:facing-" + PassageFacing.Opposite.Code)));
            return;
        }

        dsc.AppendLine(Lang.Get("ropeway:tower-complete"));
        dsc.AppendLine(Lang.Get("ropeway:blockinfo-spans", Spans.Count));

        if (IsEndpoint) dsc.AppendLine(Lang.Get("ropeway:blockinfo-endpoint"));

        var line = RopewayLine.GetOrBuild(ModSystem, Pos);
        if (line != null)
        {
            dsc.AppendLine(Lang.Get("ropeway:blockinfo-line", (int)Math.Round(line.TotalLength), line.Towers.Length));

            // Otherwise the shortfall is invisible: the panel reports a line that stops at a tower which is
            // only the end of the loaded part of it, and a cabin holding there looks broken rather than held.
            if (line.Truncated) dsc.AppendLine(Lang.Get("ropeway:blockinfo-line-truncated"));

            AppendCabinLine(line, dsc);
        }
    }

    /// <summary>
    /// Where the cabin is relative to THIS tower, now that every tower on the line can call it. Says nothing
    /// at all when no cabin is loaded: the far end of a long line is outside the client's entity tracking
    /// range, so "elsewhere" would be a claim this cannot tell apart from "there is no cabin".
    /// </summary>
    private void AppendCabinLine(RopewayLine line, StringBuilder dsc)
    {
        var cabin = EntityRopewayCabin.FindOn(Api.World, line);
        if (cabin == null) return;

        // Position rather than Travelled: Travelled is on the server-only half of the entity's attributes,
        // so the copy a client holds is whatever it was at spawn time.
        var anchor = SpanMath.AnchorOf(Pos);
        var dx = anchor.X - cabin.Pos.X;
        var dz = anchor.Z - cabin.Pos.Z;
        if (dx * dx + dz * dz <= 1)
        {
            dsc.AppendLine(Lang.Get("ropeway:blockinfo-cabin-here"));
            return;
        }

        // Destination is a distance from the cabin's OWN Towers[0], so it only names a place on this line
        // when this line was walked from that same base. A client whose chunk view is short walks a
        // contiguous run of the chain and no more, so a matching base is enough: every tower it does have
        // then carries the same Cumulative the server computed. A different base means the client is
        // measuring from somewhere else entirely, and "coming here" would be a guess.
        var index = line.IndexOf(Pos);
        var bound = index >= 0 && cabin.HasDestination && line.Towers[0].Equals(cabin.LineKey)
            && Math.Abs(cabin.Destination - line.Cumulative[index]) < EntityRopewayCabin.ArrivalTolerance;

        dsc.AppendLine(Lang.Get(bound ? "ropeway:blockinfo-cabin-coming" : "ropeway:blockinfo-cabin-elsewhere"));
    }
}
