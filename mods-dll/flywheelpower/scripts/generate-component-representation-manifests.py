#!/usr/bin/env python3
"""Generate complete transform evidence manifests for Flywheel release collectibles."""

from __future__ import annotations

import argparse
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
MANIFEST_ROOT = ROOT / "model-render"

COLLECTIBLES = [
    {
        "key": "bearing-fitting",
        "shapes": ["../assets/flywheelpower/shapes/item/bearing-fitting.json"],
        "definition": "../assets/flywheelpower/itemtypes/bearingfittings.json",
        "variant": "bearingfittings-iron",
        "textures": {"metal": "game:block/metal/sheet/iron1"},
    },
    {
        "key": "bearing-compact",
        "shapes": ["../assets/flywheelpower/shapes/item/flywheel-bearing-compact.json"],
        "definition": "../assets/flywheelpower/itemtypes/flywheelbearing.json",
        "variant": "flywheelbearing-compact-copper",
        "textures": {
            "metal": "game:block/metal/ingot/copper",
            "bearing": "game:block/metal/ingot/copper",
            "wood": "game:block/wood/planks/generic",
        },
    },
    {
        "key": "bearing-full",
        "shapes": ["../assets/flywheelpower/shapes/item/flywheel-bearing-full.json"],
        "definition": "../assets/flywheelpower/itemtypes/flywheelbearing.json",
        "variant": "flywheelbearing-full-steel",
        "textures": {
            "metal": "game:block/metal/ingot/steel",
            "bearing": "game:block/metal/ingot/steel",
            "wood": "game:block/wood/planks/generic",
        },
    },
    {
        "key": "rim-compact",
        "shapes": ["../assets/flywheelpower/shapes/item/flywheel-rim-compact.json"],
        "definition": "../assets/flywheelpower/itemtypes/flywheelrim.json",
        "variant": "flywheelrim-compact-steel",
        "textures": {"rim": "game:block/metal/ingot/steel"},
    },
    {
        "key": "rim-full",
        "shapes": ["../assets/flywheelpower/shapes/item/flywheel-rim-full.json"],
        "definition": "../assets/flywheelpower/itemtypes/flywheelrim.json",
        "variant": "flywheelrim-full-meteoriciron",
        "textures": {"rim": "game:block/metal/ingot/meteoriciron"},
    },
    {
        "key": "web-full",
        "shapes": ["../assets/flywheelpower/shapes/item/flywheel-web-full.json"],
        "definition": "../assets/flywheelpower/itemtypes/flywheelweb.json",
        "variant": "flywheelweb-full",
        "textures": {"wood": "game:block/wood/planks/generic"},
    },
    {
        "key": "stand-compact",
        "shapes": ["../assets/flywheelpower/shapes/block/compact-flywheel-frame-horizontal.json"],
        "definition": "../assets/flywheelpower/blocktypes/flywheelstand.json",
        "variant": "flywheelstand-compact-ud",
        "textures": {
            "metal": "game:block/metal/tarnished/sheet/iron1",
            "wood": "game:block/wood/planks/generic",
        },
    },
    {
        "key": "stand-full",
        "shapes": ["../assets/flywheelpower/shapes/block/flywheel-frame-horizontal.json"],
        "definition": "../assets/flywheelpower/blocktypes/flywheelstand.json",
        "variant": "flywheelstand-full-ud",
        "textures": {
            "metal": "game:block/metal/tarnished/sheet/iron1",
            "wood": "game:block/wood/planks/generic",
        },
    },
    {
        "key": "assembly-compact",
        "shapes": [
            "../assets/flywheelpower/shapes/block/compact-flywheel-wheel-coupled.json",
            "../assets/flywheelpower/shapes/block/flywheel-axle.json",
        ],
        "definition": "../assets/flywheelpower/blocktypes/compactflywheel.json",
        "variant": "compactflywheel-steel-steel-ud",
        "textures": {
            "wheel": "game:block/metal/ingot/steel",
            "metal": "game:block/metal/ingot/steel",
            "bearing": "game:block/metal/ingot/steel",
            "chalk": "game:block/cloth/wool/red1",
            "wood": "game:block/wood/planks/generic",
        },
    },
    {
        "key": "assembly-full",
        "shapes": [
            "../assets/flywheelpower/shapes/block/flywheel-wheel-coupled.json",
            "../assets/flywheelpower/shapes/block/flywheel-axle.json",
        ],
        "definition": "../assets/flywheelpower/blocktypes/flywheel.json",
        "variant": "flywheel-iron-steel-ud",
        "textures": {
            "wheel": "game:block/metal/ingot/iron",
            "metal": "game:block/metal/ingot/steel",
            "bearing": "game:block/metal/ingot/steel",
            "chalk": "game:block/cloth/wool/red1",
            "wood": "game:block/wood/planks/generic",
        },
    },
]

TRANSFORMS = {
    "gui": ("guiTransform", "gui-transform", None),
    "ground": ("groundTransform", "ground-transform", None),
    "fp": ("fpHandTransform", "first-person-held-transform", "grip-proxy"),
}


def transform_manifest(part: dict, code: str) -> dict:
    property_name, representation, reference = TRANSFORMS[code]
    transform = {
        "definition": part["definition"],
        "property": property_name,
        "variantCode": part["variant"],
    }
    if reference:
        transform["reference"] = reference
    return {
        "name": f"flywheel-{part['key']}-{code}-transform",
        "representation": representation,
        "shapes": part["shapes"],
        "textures": part["textures"],
        "collectibleTransform": transform,
    }


def seraph_manifest(part: dict) -> dict:
    return {
        "name": f"flywheel-{part['key']}-seraph-held",
        "representation": "third-person-seraph-held-scene",
        "shapes": part["shapes"],
        "textures": part["textures"],
        "seraphHeldScene": {
            "collectibleDefinition": part["definition"],
            "transformProperty": "tpHandTransform",
            "variantCode": part["variant"],
            "seraphShape": "game:entity/humanoid/seraph-hairless",
            "seraphTexture": "game:entity/humanoid/seraph-naked-hairless",
            "attachment": "RightHand",
            "animationFrame": 0,
        },
    }


def generated_manifests() -> dict[Path, dict]:
    manifests: dict[Path, dict] = {}
    for part in COLLECTIBLES:
        for code in TRANSFORMS:
            path = MANIFEST_ROOT / f"representation-{part['key']}-{code}.json"
            manifests[path] = transform_manifest(part, code)
        path = MANIFEST_ROOT / f"representation-{part['key']}-seraph.json"
        manifests[path] = seraph_manifest(part)
    return manifests


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()

    stale: list[Path] = []
    for path, manifest in generated_manifests().items():
        expected = json.dumps(manifest, indent=2) + "\n"
        if args.check:
            if not path.exists() or path.read_text(encoding="utf-8") != expected:
                stale.append(path)
        else:
            path.write_text(expected, encoding="utf-8")

    if stale:
        print("Stale or missing collectible representation manifests:")
        for path in stale:
            print(f"  {path.relative_to(ROOT)}")
        return 1

    action = "Verified" if args.check else "Generated"
    print(f"{action} {len(generated_manifests())} collectible representation manifests")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
