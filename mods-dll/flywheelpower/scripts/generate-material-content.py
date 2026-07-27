#!/usr/bin/env python3
"""Generate Flywheel Power's material matrix and its player-facing content."""

from __future__ import annotations

import argparse
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]

BRONZES = ["tinbronze", "bismuthbronze", "blackbronze"]
METALS = ["copper", *BRONZES, "iron", "meteoriciron", "steel"]
FULL_WHEELS = ["wood", *METALS]
COMPACT_WHEELS = ["wood", "stone", *METALS]
FULL_HUBS = ["iron", "meteoriciron", "steel"]
COMPACT_HUBS = METALS
TIER = {
    "wood": 0,
    "stone": 0,
    "copper": 1,
    "tinbronze": 2,
    "bismuthbronze": 2,
    "blackbronze": 2,
    "iron": 3,
    "meteoriciron": 3,
    "steel": 4,
}
DISPLAY = {
    "wood": "Wooden",
    "stone": "Stone",
    "copper": "Copper",
    "tinbronze": "Tin Bronze",
    "bismuthbronze": "Bismuth Bronze",
    "blackbronze": "Black Bronze",
    "iron": "Iron",
    "meteoriciron": "Meteoric Iron",
    "steel": "Steel",
}


def valid(wheel: str, hub: str) -> bool:
    return TIER[hub] >= TIER[wheel]


def valid_pairs(compact: bool) -> list[tuple[str, str]]:
    wheels = COMPACT_WHEELS if compact else FULL_WHEELS
    hubs = COMPACT_HUBS if compact else FULL_HUBS
    return [(wheel, hub) for wheel in wheels for hub in hubs if valid(wheel, hub)]


def texture(material: str) -> str:
    if material == "wood":
        return "game:block/wood/planks/generic"
    if material == "stone":
        return "game:block/stone/rock/granite1"
    return f"game:block/metal/ingot/{material}"


def blocktype(compact: bool) -> dict:
    code = "compactflywheel" if compact else "flywheel"
    size = "compact" if compact else "full"
    wheels = COMPACT_WHEELS if compact else FULL_WHEELS
    hubs = COMPACT_HUBS if compact else FULL_HUBS
    pairs = valid_pairs(compact)
    wheel_shape = "compact-flywheel-wheel-coupled" if compact else "flywheel-wheel-coupled"
    frame_prefix = "compact-" if compact else ""

    data = {
        "code": code,
        "class": "BlockCompactFlywheel" if compact else "BlockFlywheel",
        "entityClass": "Generic",
        "entityBehaviors": [{
            "name": "MPFlywheel",
            "properties": {
                "axleShape": {"base": "flywheelpower:block/flywheel-axle"},
                "flywheelShape": {"base": f"flywheelpower:block/{wheel_shape}"},
                "horizontalStandShape": {"base": f"flywheelpower:block/{frame_prefix}flywheel-frame-horizontal"},
                "verticalStandShape": {"base": f"flywheelpower:block/{frame_prefix}flywheel-frame-vertical"},
                "slipCoupled": True,
                "inertia": 8.0,
                "couplingStrength": 0.55 if compact else 0.8,
                "maxTransferTorque": 0.18 if compact else 0.35,
                "baseBearingLoss": 0.0005 if compact else 0.001,
                "viscousBearingLoss": 0.0015 if compact else 0.003,
                "windageLoss": 0.0005 if compact else 0.0015,
                "safeSpeed": 4.5 if compact else 3.5,
            },
        }],
        "attributes": {
            "mechanicalPower": {
                "renderer": f"flywheelpower-{size}-iron-ironhub",
            },
        },
        "attributesByType": {
            f"*-{wheel}-{hub}-*": {
                "mechanicalPower": {
                    "renderer": f"flywheelpower-{size}-{wheel}-{hub}hub",
                },
            }
            for wheel, hub in pairs
        },
        "variantgroups": [
            {"code": "material", "states": wheels},
            {"code": "hub", "states": hubs},
            {"code": "rotation", "states": ["ud", "ns", "we"]},
        ],
        "skipVariants": [
            f"{code}-{wheel}-{hub}-*"
            for wheel in wheels
            for hub in hubs
            if not valid(wheel, hub)
        ],
        "creativeinventory": {"general": ["*-ud"], "mechanics": ["*-ud"]},
        "shapeInventory": {
            "base": f"flywheelpower:block/{wheel_shape}",
            "overlays": [{"base": "flywheelpower:block/flywheel-axle"}],
        },
        "shapeByType": {
            "*-ns": {"base": f"flywheelpower:block/{frame_prefix}flywheel-frame-horizontal", "rotateY": 90},
            "*-ud": {"base": f"flywheelpower:block/{frame_prefix}flywheel-frame-vertical", "rotateY": 0},
            "*-we": {"base": f"flywheelpower:block/{frame_prefix}flywheel-frame-horizontal", "rotateY": 0},
        },
        "blockmaterial": "Metal",
        "textures": {
            "wheel": {"base": "game:block/metal/ingot/iron"},
            "metal": {"base": "game:block/metal/ingot/iron"},
            "bearing": {"base": "game:block/metal/tarnished/iron-riveted1"},
            "chalk": {"base": "game:block/cloth/wool/red1"},
            "wood": {"base": "game:block/wood/planks/generic"},
        },
        "texturesByType": {
            f"*-{wheel}-{hub}-*": {
                "wheel": {"base": texture(wheel)},
                "metal": {"base": texture(hub)},
                "bearing": {"base": "game:block/metal/tarnished/iron-riveted1"},
                "wood": {"base": "game:block/wood/planks/generic"},
            }
            for wheel, hub in pairs
        },
        "sidesolid": {"all": False},
        "sideopaque": {"all": False},
        "rainPermeable": True,
        "resistance": 5,
        "lightAbsorption": 0,
        "maxStackSize": 16 if compact else 8,
        "drops": [
            {"type": "block", "code": f"flywheelstand-{size}-ud", "quantity": {"avg": 1}},
            {"type": "block", "code": f"{code}-{{material}}-{{hub}}-ud", "quantity": {"avg": 1}},
        ],
        "collisionSelectionBoxByType": {
            "*-we": {"x1": 0, "y1": 0.0625, "z1": 0.0625, "x2": 1, "y2": 0.9375, "z2": 0.9375},
            "*-ns": {"x1": 0, "y1": 0.0625, "z1": 0.0625, "x2": 1, "y2": 0.9375, "z2": 0.9375, "rotateY": 90},
            "*-ud": {"x1": 0.0625, "y1": 0, "z1": 0.0625, "x2": 0.9375, "y2": 1, "z2": 0.9375},
        },
        "sounds": {
            "hit": "game:block/metalhit",
            "break": "game:block/metalbreak",
            "place": "game:block/metalplace",
            "walk": "game:walk/stone",
        },
    }
    if not compact:
        data["materialDensity"] = 7800
        data["groundTransform"] = {
            "translation": {"x": 0, "y": 0, "z": 0},
            "rotation": {"x": -90, "y": 0, "z": 0},
            "origin": {"x": 0.5, "y": 0.5, "z": 0.5},
            "scale": 2.2,
        }
        data["guiTransform"] = {"rotation": {"x": -55, "y": -10, "z": -43}, "scale": 1.35}
        data["heldTpIdleAnimation"] = "holdbothhandslarge"
        data["heldRightReadyAnimation"] = "heldblockready"
        data["heldTpUseAnimation"] = "twohandplaceblock"
        data["tpHandTransform"] = {
            "translation": {"x": -1.35, "y": -0.85, "z": -0.75},
            "rotation": {"x": 10, "y": 18, "z": -80},
            "origin": {"x": 0.5, "y": 0.45, "z": 0.5},
            "scale": 0.42,
        }
    return data


def bearing_itemtype() -> dict:
    return {
        "code": "flywheelbearing",
        "variantgroups": [
            {"code": "size", "states": ["full", "compact"]},
            {"code": "hub", "states": METALS},
        ],
        "skipVariants": [f"flywheelbearing-full-{hub}" for hub in METALS if hub not in FULL_HUBS],
        "shape": {"base": "game:item/plate"},
        "textureByType": {f"*-{hub}": {"base": texture(hub)} for hub in METALS},
        "creativeinventory": {"general": ["*"], "items": ["*"], "mechanics": ["*"]},
        "maxstacksize": 8,
        "guiTransform": {
            "rotation": {"x": -30, "y": -44, "z": -180},
            "origin": {"x": 0.5, "y": 0.0625, "z": 0.5},
            "scale": 2.3,
        },
    }


def rim_itemtype() -> dict:
    materials = ["wood", "stone", *METALS]
    return {
        "code": "flywheelrim",
        "variantgroups": [
            {"code": "size", "states": ["full", "compact"]},
            {"code": "material", "states": materials},
        ],
        "skipVariants": ["flywheelrim-full-stone"],
        "shape": {"base": "game:item/plate"},
        "textureByType": {f"*-{material}": {"base": texture(material)} for material in materials},
        "creativeinventory": {"general": ["*"], "items": ["*"], "mechanics": ["*"]},
        "maxstacksize": 8,
        "guiTransform": {
            "rotation": {"x": -30, "y": -44, "z": -180},
            "origin": {"x": 0.5, "y": 0.0625, "z": 0.5},
            "scale": 2.8,
        },
    }


def bearing_recipe(size: str, hub: str) -> dict:
    full = size == "full"
    return {
        "ingredientPattern": "PFP,AHA,P_P" if full else "PF_,AHA",
        "ingredients": {
            "P": {"type": "item", "code": f"game:metalplate-{hub}"},
            "F": {"type": "item", "code": "game:fat-rendered"},
            "A": {"type": "block", "code": "game:woodenaxle-ud"},
            "H": {
                "type": "item",
                "code": "game:hammer-*",
                "isTool": True,
                "toolDurabilityCost": 4 if full else 2,
            },
        },
        "width": 3,
        "height": 3 if full else 2,
        "output": {"type": "item", "code": f"flywheelbearing-{size}-{hub}"},
    }


def components_recipes() -> list[dict]:
    recipes = [
        *(bearing_recipe("full", hub) for hub in FULL_HUBS),
        *(bearing_recipe("compact", hub) for hub in COMPACT_HUBS),
        {
            "ingredientPattern": "PPP,PSP,PPP",
            "ingredients": {
                "P": {"type": "block", "code": "game:planks-*", "quantity": 1},
                "S": {"type": "item", "code": "game:saw-*", "isTool": True, "toolDurabilityCost": 8},
            },
            "width": 3,
            "height": 3,
            "output": {"type": "item", "code": "flywheelweb-full"},
        },
        {
            "ingredientPattern": "P_P,PSP",
            "ingredients": {
                "P": {"type": "block", "code": "game:planks-*", "quantity": 1},
                "S": {"type": "item", "code": "game:saw-*", "isTool": True, "toolDurabilityCost": 4},
            },
            "width": 3,
            "height": 2,
            "output": {"type": "item", "code": "flywheelweb-compact"},
        },
    ]
    for size, full in (("full", True), ("compact", False)):
        recipes.extend([
            {
                "ingredientPattern": "PPP,PHP,PPP" if full else "P_P,PHP",
                "ingredients": {
                    "P": {
                        "type": "item",
                        "code": "game:metalplate-*",
                        "name": "metal",
                        "allowedVariants": METALS,
                    },
                    "H": {
                        "type": "item",
                        "code": "game:hammer-*",
                        "isTool": True,
                        "toolDurabilityCost": 8 if full else 4,
                    },
                },
                "width": 3,
                "height": 3 if full else 2,
                "output": {"type": "item", "code": f"flywheelrim-{size}-{{metal}}"},
            },
            {
                "ingredientPattern": "PPP,PSP,PPP" if full else "P_P,PSP",
                "ingredients": {
                    "P": {"type": "block", "code": "game:planks-*", "quantity": 1},
                    "S": {
                        "type": "item",
                        "code": "game:saw-*",
                        "isTool": True,
                        "toolDurabilityCost": 8 if full else 4,
                    },
                },
                "width": 3,
                "height": 3 if full else 2,
                "output": {"type": "item", "code": f"flywheelrim-{size}-wood"},
            },
        ])
    recipes.append({
        "ingredientPattern": "R_R,CR_",
        "ingredients": {
            "R": {"type": "block", "code": "game:rockpolished-granite"},
            "C": {"type": "item", "code": "game:chisel-*", "isTool": True, "toolDurabilityCost": 4},
        },
        "width": 3,
        "height": 2,
        "output": {"type": "item", "code": "flywheelrim-compact-stone"},
    })
    return recipes


def assembly_recipes() -> list[dict]:
    recipes = []
    for compact in (False, True):
        size = "compact" if compact else "full"
        block = "compactflywheel" if compact else "flywheel"
        web = f"flywheelweb-{size}"
        for wheel, hub in valid_pairs(compact):
            recipes.append({
                "ingredientPattern": "R,W,B",
                "ingredients": {
                    "R": {"type": "item", "code": f"flywheelpower:flywheelrim-{size}-{wheel}"},
                    "W": {"type": "item", "code": f"flywheelpower:{web}"},
                    "B": {"type": "item", "code": f"flywheelpower:flywheelbearing-{size}-{hub}"},
                },
                "width": 1,
                "height": 3,
                "output": {"type": "block", "code": f"flywheelpower:{block}-{wheel}-{hub}-ud"},
            })
    return recipes


def language() -> dict:
    path = ROOT / "assets/flywheelpower/lang/en.json"
    existing = json.loads(path.read_text(encoding="utf-8"))
    generated_prefixes = (
        "item-flywheelbearing-",
        "item-flywheelrim-",
        "block-flywheel-",
        "block-compactflywheel-",
    )
    result = {key: value for key, value in existing.items() if not key.startswith(generated_prefixes)}

    for hub in FULL_HUBS:
        result[f"item-flywheelbearing-full-{hub}"] = f"Full-Size {DISPLAY[hub]} Hub and Bearing Set"
    for hub in COMPACT_HUBS:
        result[f"item-flywheelbearing-compact-{hub}"] = f"Compact {DISPLAY[hub]} Hub and Bearing Set"
    for wheel in FULL_WHEELS:
        result[f"item-flywheelrim-full-{wheel}"] = (
            "Full-Size Wooden Rim"
            if wheel == "wood"
            else f"Full-Size Curved {DISPLAY[wheel]} Tyre"
        )
    for wheel in COMPACT_WHEELS:
        result[f"item-flywheelrim-compact-{wheel}"] = f"Compact {DISPLAY[wheel]} Wheel Blank"

    for compact in (False, True):
        prefix = "block-compactflywheel" if compact else "block-flywheel"
        compact_label = "Compact " if compact else ""
        for wheel, hub in valid_pairs(compact):
            result[f"{prefix}-{wheel}-{hub}-*"] = (
                f"{compact_label}{DISPLAY[wheel]} Friction-Coupled Flywheel ({DISPLAY[hub]} Hub)"
            )
    return result


def outputs() -> dict[Path, object]:
    return {
        ROOT / "assets/flywheelpower/blocktypes/flywheel.json": blocktype(False),
        ROOT / "assets/flywheelpower/blocktypes/compactflywheel.json": blocktype(True),
        ROOT / "assets/flywheelpower/itemtypes/flywheelbearing.json": bearing_itemtype(),
        ROOT / "assets/flywheelpower/itemtypes/flywheelrim.json": rim_itemtype(),
        ROOT / "assets/flywheelpower/recipes/grid/flywheel-components.json": components_recipes(),
        ROOT / "assets/flywheelpower/recipes/grid/flywheel-assembly.json": assembly_recipes(),
        ROOT / "assets/flywheelpower/lang/en.json": language(),
    }


def serialized(value: object) -> str:
    return json.dumps(value, indent=2, ensure_ascii=False) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()

    drift = []
    for path, value in outputs().items():
        expected = serialized(value)
        if args.check:
            if not path.exists() or path.read_text(encoding="utf-8") != expected:
                drift.append(path.relative_to(ROOT))
        else:
            path.write_text(expected, encoding="utf-8", newline="\n")

    if drift:
        print("Generated material content is stale:")
        for path in drift:
            print(f"  {path}")
        return 1
    if args.check:
        print("Verified generated Flywheel material content")
    else:
        print("Generated Flywheel material content")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
