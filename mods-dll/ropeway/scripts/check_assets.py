#!/usr/bin/env python3
"""Resolve every asset reference the mod makes, against the real game install.

WHY THIS IS NOT A TEST. The xunit suite already checks the handshakes it CAN see:
RopewayAssetContractTests.EveryTextureKeyAShapeUsesIsDeclaredWhereTheGameWillLookForIt
proves every `#key` a face names is declared somewhere. What no test in the repo can
prove is that the PATH behind that key names a file that exists - the .Tests project has
no idea where Vintage Story is installed, and a CI box has no game install at all. So a
`game:block/metal/riveted/iron2` typo passes the whole suite, ships, and draws the magenta
unknown-texture checker on every tower in the world. The only sign in game is one line on
the tesselation thread that nobody reads.

Same argument for the recipe codes: the loader logs a resolve error at server start and
carries on with a recipe that can never be crafted, which is invisible to a green build.

Run: python scripts/check_assets.py [--install D:/Games/Vintagestory]
Exit 0 clean, 1 with findings. Read-only; touches nothing under the game install.
"""

import argparse
import io
import json
import re
import sys
from pathlib import Path

MOD = Path(__file__).resolve().parent.parent
ASSETS = MOD / "assets" / "ropeway"

# The `game` DOMAIN is served by two folders on disk (assets/game and assets/survival) and
# most of what we borrow lives in survival/. Tried and rejected: pointing at the decompiled
# assets under vs/source - that tree carries the JSON and NOT one of the 9587 PNGs, so every
# texture would resolve to "missing" and the check would be worse than useless.
DOMAIN_DIRS = {"game": ("game", "survival", "creative")}

findings: list[str] = []


def fail(what: str) -> None:
    findings.append(what)


def load(path: Path):
    """Strict json.load, utf-8-sig. The game's own parser is lenient (JSON5-ish, and vanilla
    files lean on it hard) but OURS are hand-authored strict JSON and a trailing comma here
    means the block silently loses its attributes - multiblockStructure included."""
    with io.open(path, encoding="utf-8-sig") as handle:
        return json.load(handle)


# ---------------------------------------------------------------- the mod's own vocabulary

def variants(spec) -> list[str]:
    """Expand a blocktype/itemtype's variantgroups into the codes the game will register."""
    codes = [spec["code"]]
    for group in spec.get("variantgroups", []):
        states = group.get("states")
        if not states:
            # loadFromProperties - a vanilla world-property list we are not going to parse.
            return codes
        codes = [f"{code}-{state}" for code in codes for state in states]
    return codes


def collect_types(folder: str) -> dict[str, Path]:
    out = {}
    for path in sorted((ASSETS / folder).glob("*.json")):
        for code in variants(load(path)):
            out[code] = path
    return out


# ---------------------------------------------------------------- 1. texture paths

def texture_refs() -> list[tuple[str, str, str]]:
    """(file, key, path) for every texture path the mod declares.

    Two shapes of the same thing: blocktypes/itemtypes write {"key": {"base": "..."}} plus a
    `textureByType`/`texturesByType` variant, shapes write {"key": "..."} flat. Anything with
    an `overlays` list carries paths too.
    """
    refs = []

    def walk_map(file: str, mapping) -> None:
        for key, value in mapping.items():
            if key.startswith("//"):
                continue
            if isinstance(value, str):
                refs.append((file, key, value))
            elif isinstance(value, dict):
                if "base" in value:
                    refs.append((file, key, value["base"]))
                for overlay in value.get("overlays", []):
                    refs.append((file, key + ".overlay", overlay))

    for path in sorted(ASSETS.rglob("*.json")):
        data = load(path)
        if not isinstance(data, dict):
            continue
        file = str(path.relative_to(ASSETS)).replace("\\", "/")
        for prop in ("textures", "texture"):
            block = data.get(prop)
            if isinstance(block, dict):
                # a bare {"base": ...} under "texture", vs a map of named textures
                walk_map(file, {prop: block} if "base" in block else block)
        for prop in ("texturesByType", "textureByType"):
            for by in (data.get(prop) or {}).values():
                if isinstance(by, dict):
                    walk_map(file, {prop: by} if "base" in by else by)

    return refs


def resolve_asset(path: str, kind: str, ext: str, install: Path) -> bool:
    """kind is the asset subfolder ("textures", "shapes"); path is "domain:sub/name"."""
    domain, _, rest = path.partition(":")
    if not rest:
        domain, rest = "game", path
    if domain == "ropeway":
        return (ASSETS / kind / (rest + ext)).is_file()
    for folder in DOMAIN_DIRS.get(domain, (domain,)):
        if (install / folder / kind / (rest + ext)).is_file():
            return True
    return False


def check_textures(install: Path) -> int:
    refs = texture_refs()
    for file, key, path in refs:
        if "*" in path or "{" in path:
            fail(f"texture {file} #{key} -> {path} is a pattern this check cannot resolve")
        elif not resolve_asset(path, "textures", ".png", install):
            fail(f"MAGENTA: {file} #{key} -> {path} resolves to no PNG")
    return len(refs)


def shape_refs() -> set[tuple[str, str]]:
    """(file, shape path) for every blocktype/itemtype that names its own geometry."""
    seen = set()
    for path in sorted(ASSETS.rglob("*.json")):
        data = load(path)
        if not isinstance(data, dict):
            continue
        file = str(path.relative_to(ASSETS)).replace("\\", "/")
        for spec in [data.get("shape")] + list((data.get("shapeByType") or {}).values()):
            if isinstance(spec, dict) and "base" in spec:
                seen.add((file, spec["base"]))
    return seen


def check_shapes(install: Path) -> int:
    """Same failure mode one level up: a shape that does not resolve draws nothing at all."""
    seen = shape_refs()
    for file, base in sorted(seen):
        if not resolve_asset(base, "shapes", ".json", install):
            fail(f"shape {file} -> {base} resolves to no JSON")
    return len(seen)


def check_palette() -> int:
    """One key, one sprite, mod-wide.

    A texture key is a PALETTE entry, not a local name: `girder` means riveted iron on all
    twenty faces that ask for it, or it means nothing. Two spellings of the same material is
    how a mod ends up with a crossarm and a post in subtly different greys, and nothing else
    in this file would notice - both paths resolve, so `check_textures` passes.

    ONE exclusion, and it is a rule rather than an allowlist: a file whose own `shape` is
    VANILLA is speaking that shape's vocabulary, not ours. `haulrope` reuses
    `game:item/resource/rope`, whose faces name `rope` and expect the item sprite; the mod's
    own `rope` is `game:block/cloth/reedrope`, the drawn cable. Satisfying a borrowed shape
    is not a palette choice, and overriding it to match would put a block sprite on a vanilla
    item model. Nothing is hardcoded here - add another borrowed shape and it is excluded too.
    """
    borrowed = {file for file, base in shape_refs() if not base.startswith("ropeway:")}
    palette: dict[str, set[str]] = {}
    for file, key, path in texture_refs():
        if file in borrowed:
            continue
        palette.setdefault(key, set()).add(path)
    for key, sprites in sorted(palette.items()):
        if len(sprites) > 1:
            fail(f"palette #{key} names {len(sprites)} sprites: {', '.join(sorted(sprites))}")
    return len(palette)


# ---------------------------------------------------------------- 2. recipe codes

def vanilla_codes(install: Path) -> set[str]:
    """Top-level `code` of every vanilla block/item type.

    Regex rather than a parser ON PURPOSE: vanilla JSON is JSON5 (bare keys, single quotes,
    comments) and json.load cannot read a line of it. Anchoring on <=2 units of leading
    indentation keeps most variantgroup codes out of the index, so a typo'd ingredient is
    not rescued by a coincidence.

    ponytail: the anchor leaks on a handful of shallowly-indented vanilla files (2 of 671
    codes measured - metalpartsandscraps' "metal"), which can only ever ACCEPT a bad code,
    never reject a good one. A real JSON5 parse is the upgrade if that ever matters; the six
    families this mod actually asks for were each confirmed against their defining file.
    """
    pattern = re.compile(r'^[\t ]{0,2}"?code"?\s*:\s*["\']([a-z0-9-]+)["\']', re.M)
    codes = set()
    for folder in DOMAIN_DIRS["game"]:
        for kind in ("blocktypes", "itemtypes"):
            root = install / folder / kind
            if not root.is_dir():
                continue
            for path in root.rglob("*.json"):
                codes.update(pattern.findall(path.read_text(encoding="utf-8-sig", errors="replace")))
    return codes


def check_recipes(mod_codes: set[str], install: Path) -> int:
    vanilla = vanilla_codes(install)

    def resolves(code: str) -> bool:
        domain, _, rest = code.partition(":")
        if not rest:
            domain, rest = "game", code
        # A wildcard names a FAMILY: `metalplate-*` is satisfied by any variant of the
        # `metalplate` base type, so the base is what has to exist.
        base = rest.split("-")[0] if ("*" in rest or "@" in rest) else rest
        if domain == "ropeway":
            return base in mod_codes or any(c.split("-")[0] == base for c in mod_codes)
        return base in vanilla

    checked = 0
    for path in sorted((ASSETS / "recipes").rglob("*.json")):
        recipe = load(path)
        name = path.name
        for slot, ingredient in recipe["ingredients"].items():
            checked += 1
            code = ingredient["code"]
            if not resolves(code):
                fail(f"recipe {name} slot {slot}: ingredient {code} matches nothing")
            # The bug that logged three resolve errors at every server start. A `name` on a
            # wildcard makes the loader expand the file into one recipe per metal, and two
            # ingredients sharing one name overwrite each other in the mapping.
            if "name" in ingredient:
                fail(f"recipe {name} slot {slot}: wildcard carries a `name`, which cartesian-expands it")

        checked += 1
        out = recipe["output"]["code"]
        bare = out.split(":")[-1]
        if bare not in mod_codes:
            fail(f"recipe {name}: output {out} is not a block or item this mod registers")
        if ":" in out and not out.startswith("ropeway:"):
            fail(f"recipe {name}: output {out} is not in this mod's domain")

        # The grid itself: a pattern that does not fill width x height never matches.
        rows = recipe["ingredientPattern"].split(",")
        if len(rows) != recipe["height"] or any(len(r) != recipe["width"] for r in rows):
            fail(f"recipe {name}: pattern {recipe['ingredientPattern']} is not "
                 f"{recipe['width']}x{recipe['height']}")
        used = {c for row in rows for c in row if c != "_"}
        for slot in used - set(recipe["ingredients"]):
            fail(f"recipe {name}: pattern uses '{slot}' with no ingredient declared")
        for slot in set(recipe["ingredients"]) - used:
            fail(f"recipe {name}: ingredient '{slot}' is declared and never used")

    return checked


def check_handbook_codes(mod_codes: set[str]) -> int:
    """<itemstack code="..."> in a handbook page. A code that does not resolve draws an empty
    slot on the page, silently."""
    checked = 0
    for path in sorted((ASSETS / "config" / "handbook").glob("*.json")):
        text = json.dumps(load(path))
        for group in re.findall(r'<itemstack code=\\?"([^"\\]+)', text):
            for code in group.split("|"):
                checked += 1
                if code.startswith("ropeway:") and code.split(":")[-1] not in mod_codes:
                    fail(f"handbook {path.name}: <itemstack> code {code} does not resolve")
    return checked


# ---------------------------------------------------------------- 3. lang keys

def check_lang(mod_codes: set[str]) -> int:
    lang = load(ASSETS / "lang" / "en.json")

    # Every string literal in the C#, minus comment lines - Lang.Get is not the only caller
    # (Binding() and CompassKey() both take a key and hand it on) and matching only Lang.Get
    # would miss exactly the keys nothing else covers.
    used = set()
    for path in sorted((MOD / "src").rglob("*.cs")):
        for line in path.read_text(encoding="utf-8-sig").splitlines():
            stripped = line.lstrip()
            if stripped.startswith(("//", "*", "/*")):
                continue
            used.update(re.findall(r'"(ropeway:[a-z0-9][a-z0-9.-]*)"', line))

    # Asset codes share the prefix and are not lang keys. They are checked as codes instead.
    used = {k for k in used if k.split(":")[1] not in mod_codes}

    for key in sorted(used - set(lang)):
        fail(f"lang: C# asks for {key}, en.json has no such key")

    # The other direction, only for keys the C# is the sole consumer of. block-*/item-* are
    # the game's own naming convention and handbook-title-* are named from the page JSON.
    referenced = set(used)
    for path in sorted((ASSETS / "config" / "handbook").glob("*.json")):
        referenced.add(load(path)["title"])
    for code in mod_codes:
        referenced.update({f"block-{code}", f"item-{code}", f"item-creature-{code}"})

    for key in sorted(set(lang) - referenced):
        if key.startswith("game:"):
            continue  # a vanilla key we are ADDING to, e.g. the handbook category name
        fail(f"lang: en.json defines {key}, nothing references it")

    # And the naming half: every registered block and item wants a display name, or the game
    # shows the raw code in the creative menu and the handbook.
    for path, prefix in (("blocktypes", "block"), ("itemtypes", "item")):
        for code in collect_types(path):
            if f"{prefix}-{code}" not in lang:
                fail(f"lang: {prefix} {code} has no {prefix}-{code} name")

    return len(lang)


# ---------------------------------------------------------------- driver

def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--install", default="D:/Games/Vintagestory",
                        help="game install root; its assets/ is the only texture library there is")
    args = parser.parse_args()
    install = Path(args.install) / "assets"
    if not install.is_dir():
        print(f"no game assets at {install}", file=sys.stderr)
        return 2

    parsed = 0
    for path in sorted(ASSETS.rglob("*.json")):
        try:
            load(path)
            parsed += 1
        except (json.JSONDecodeError, UnicodeDecodeError) as e:
            fail(f"json {path.relative_to(ASSETS)}: {e}")

    blocks = collect_types("blocktypes")
    items = collect_types("itemtypes")
    entities = collect_types("entities")
    codes = set(blocks) | set(items) | set(entities)

    textures = check_textures(install)
    shapes = check_shapes(install)
    palette = check_palette()
    ingredients = check_recipes(codes, install)
    stacks = check_handbook_codes(codes)
    keys = check_lang(codes)

    print(f"json parsed        {parsed}")
    print(f"texture paths      {textures}")
    print(f"shape refs         {shapes}")
    print(f"palette keys       {palette}")
    print(f"recipe codes       {ingredients}")
    print(f"handbook itemstack {stacks}")
    print(f"lang keys          {keys}")
    print(f"block/item codes   {len(codes)}")

    for finding in findings:
        print("FAIL " + finding)
    print(("FAILED: %d" % len(findings)) if findings else "OK")
    return 1 if findings else 0


if __name__ == "__main__":
    sys.exit(main())
