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


def component_transforms(gui_scale: float) -> dict:
    return {
        "guiTransform": {
            "rotation": {"x": -20, "y": -35, "z": -15},
            "origin": {"x": 0.5, "y": 0.5, "z": 0.5},
            "scale": gui_scale,
        },
        "groundTransform": {
            "translation": {"x": 0, "y": 0.05, "z": 0},
            "rotation": {"x": 90, "y": 0, "z": 0},
            "origin": {"x": 0.5, "y": 0.5, "z": 0.5},
            "scale": 1.5,
        },
        "fpHandTransform": {
            "translation": {"x": 0.05, "y": 0, "z": 0},
            "rotation": {"x": 180, "y": 90, "z": -30},
            "origin": {"x": 0.5, "y": 0.5, "z": 0.5},
            "scale": 0.9,
        },
        "tpHandTransform": {
            "translation": {"x": -1.25, "y": -1.25, "z": -1.15},
            "rotation": {"x": 0, "y": -62, "z": 18},
            "origin": {"x": 0.5, "y": 0.5, "z": 0.5},
            "scale": 0.42,
        },
    }


def large_component_held_pose(
    full_pattern: str,
    translation: tuple[float, float, float] = (-0.625, -0.625, -0.575),
) -> dict:
    translation_x, translation_y, translation_z = translation
    return {
        "heldTpIdleAnimationByType": {
            full_pattern: "holdbothhandslarge",
        },
        "tpHandTransformByType": {
            full_pattern: {
                "translation": {
                    "x": translation_x,
                    "y": translation_y,
                    "z": translation_z,
                },
                "scale": 0.84,
            },
        },
    }


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
        "scale": 2.2 if not compact else 2.8,
    }
    data["guiTransform"] = {
        "rotation": {"x": -55, "y": -10, "z": -43},
        "scale": 1.35 if not compact else 1.8,
    }
    data["fpHandTransform"] = {
        "translation": {"x": 0, "y": -0.15, "z": 0},
        "rotation": {"x": 180, "y": 90, "z": -30},
        "origin": {"x": 0.5, "y": 0.5, "z": 0.5},
        "scale": 0.42 if not compact else 0.58,
    }
    data["heldTpIdleAnimation"] = "holdbothhandslarge"
    data["heldRightReadyAnimation"] = "heldblockready"
    data["heldTpUseAnimation"] = "twohandplaceblock"
    data["tpHandTransform"] = {
        "translation": {
            "x": -1.35 if not compact else -1.0,
            "y": -0.85 if not compact else -0.72,
            "z": -0.75 if not compact else -0.7,
        },
        "rotation": {"x": 10, "y": 18, "z": -80},
        "origin": {"x": 0.5, "y": 0.45, "z": 0.5},
        "scale": 0.42 if not compact else 0.58,
    }
    return data


def bearing_itemtype() -> dict:
    data = {
        "code": "flywheelbearing",
        "variantgroups": [
            {"code": "size", "states": ["full", "compact"]},
            {"code": "hub", "states": METALS},
        ],
        "skipVariants": [f"flywheelbearing-full-{hub}" for hub in METALS if hub not in FULL_HUBS],
        "shapeByType": {
            "flywheelbearing-full-*": {"base": "flywheelpower:item/flywheel-bearing-full"},
            "flywheelbearing-compact-*": {"base": "flywheelpower:item/flywheel-bearing-compact"},
        },
        "textures": {
            "metal": {"base": "game:block/metal/ingot/iron"},
            "bearing": {"base": "game:block/metal/tarnished/iron-riveted1"},
            "wood": {"base": "game:block/wood/planks/generic"},
        },
        "texturesByType": {
            f"*-{hub}": {
                "metal": {"base": texture(hub)},
                "bearing": {"base": "game:block/metal/tarnished/iron-riveted1"},
                "wood": {"base": "game:block/wood/planks/generic"},
            }
            for hub in METALS
        },
        "creativeinventory": {"general": ["*"], "items": ["*"], "mechanics": ["*"]},
        "maxstacksize": 8,
    }
    data.update(component_transforms(1.65))
    data.update(large_component_held_pose("flywheelbearing-full-*"))
    return data


def bearing_fittings_itemtype() -> dict:
    data = {
        "code": "bearingfittings",
        "variantgroups": [{"code": "metal", "states": METALS}],
        "shape": {"base": "flywheelpower:item/bearing-fitting"},
        "textures": {"metal": {"base": "game:block/metal/sheet/iron1"}},
        "texturesByType": {
            f"*-{metal}": {"metal": {"base": f"game:block/metal/sheet/{metal}1"}}
            for metal in METALS
        },
        "creativeinventory": {"general": ["*"], "items": ["*"], "mechanics": ["*"]},
        "maxstacksize": 32,
    }
    data.update(component_transforms(1.8))
    return data


def web_itemtype() -> dict:
    data = {
        "code": "flywheelweb",
        "variantgroups": [{"code": "size", "states": ["full"]}],
        "shapeByType": {
            "flywheelweb-full": {"base": "flywheelpower:item/flywheel-web-full"},
        },
        "textures": {"wood": {"base": "game:block/wood/planks/generic"}},
        "creativeinventory": {"general": ["*"], "items": ["*"], "mechanics": ["*"]},
        "maxstacksize": 8,
    }
    data.update(component_transforms(1.55))
    data.update(large_component_held_pose("flywheelweb-full"))
    return data


def rim_itemtype() -> dict:
    materials = ["wood", "stone", *METALS]
    data = {
        "code": "flywheelrim",
        "variantgroups": [
            {"code": "size", "states": ["full", "compact"]},
            {"code": "material", "states": materials},
        ],
        "skipVariants": ["flywheelrim-full-stone"],
        "shapeByType": {
            "flywheelrim-full-*": {"base": "flywheelpower:item/flywheel-rim-full"},
            "flywheelrim-compact-*": {"base": "flywheelpower:item/flywheel-rim-compact"},
        },
        "textures": {"rim": {"base": "game:block/metal/ingot/iron"}},
        "texturesByType": {
            f"*-{material}": {"rim": {"base": texture(material)}}
            for material in materials
        },
        "creativeinventory": {"general": ["*"], "items": ["*"], "mechanics": ["*"]},
        "maxstacksize": 8,
    }
    data.update(component_transforms(1.5))
    data.update(large_component_held_pose(
        "flywheelrim-full-*",
        translation=(-0.307, -0.694, -0.665),
    ))
    return data


def bearing_recipe(size: str, hub: str) -> dict:
    full = size == "full"
    return {
        "ingredientPattern": "F_F,AL_",
        "ingredients": {
            "F": {
                "type": "item",
                "code": f"flywheelpower:bearingfittings-{hub}",
                "quantity": 16 if full else 4,
            },
            "A": {"type": "block", "code": "game:woodenaxle-ud"},
            "L": {"type": "item", "code": "game:fat-rendered"},
        },
        "width": 3,
        "height": 2,
        "output": {"type": "item", "code": f"flywheelbearing-{size}-{hub}"},
    }


def smithing_recipes() -> list[dict]:
    return [{
        "ingredient": {
            "type": "item",
            "code": "game:ingot-*",
            "name": "metal",
            "allowedVariants": METALS,
        },
        "pattern": [[
            "_#####_",
            "##___##",
            "##___##",
            "##___##",
            "##___##",
            "###_###",
        ]],
        "name": "Flywheel bearing fittings",
        "code": "bearingfittings-{metal}",
        "output": {
            "type": "item",
            "code": "flywheelpower:bearingfittings-{metal}",
            "stacksize": 4,
        },
    }]


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
        for wheel, hub in valid_pairs(compact):
            ingredients = {
                    "R": {"type": "item", "code": f"flywheelpower:flywheelrim-{size}-{wheel}"},
                    "B": {"type": "item", "code": f"flywheelpower:flywheelbearing-{size}-{hub}"},
                }
            if not compact:
                ingredients["W"] = {"type": "item", "code": "flywheelpower:flywheelweb-full"}
            recipes.append({
                "ingredientPattern": "R,B" if compact else "R,W,B",
                "ingredients": ingredients,
                "width": 1,
                "height": 2 if compact else 3,
                "output": {"type": "block", "code": f"flywheelpower:{block}-{wheel}-{hub}-ud"},
            })
    return recipes


def language() -> dict:
    path = ROOT / "assets/flywheelpower/lang/en.json"
    existing = json.loads(path.read_text(encoding="utf-8"))
    generated_prefixes = (
        "item-bearingfittings-",
        "item-flywheelbearing-",
        "item-flywheelrim-",
        "block-flywheel-",
        "block-compactflywheel-",
    )
    result = {key: value for key, value in existing.items() if not key.startswith(generated_prefixes)}

    for metal in METALS:
        result[f"item-bearingfittings-{metal}"] = f"{DISPLAY[metal]} Bearing Fittings"
    for hub in FULL_HUBS:
        result[f"item-flywheelbearing-full-{hub}"] = f"Full-Size {DISPLAY[hub]} Hub and Bearing Set"
    for hub in COMPACT_HUBS:
        result[f"item-flywheelbearing-compact-{hub}"] = f"Compact {DISPLAY[hub]} Hub and Bearing Set"
    for wheel in FULL_WHEELS:
        result[f"item-flywheelrim-full-{wheel}"] = f"Full-Size {DISPLAY[wheel]} Wheel"
    for wheel in COMPACT_WHEELS:
        result[f"item-flywheelrim-compact-{wheel}"] = f"Compact {DISPLAY[wheel]} Wheel"

    for compact in (False, True):
        prefix = "block-compactflywheel" if compact else "block-flywheel"
        compact_label = "Compact " if compact else ""
        for wheel, hub in valid_pairs(compact):
            result[f"{prefix}-{wheel}-{hub}-*"] = (
                f"{compact_label}{DISPLAY[wheel]} Flywheel ({DISPLAY[hub]} Hub)"
            )
    return result


def outputs() -> dict[Path, object]:
    return {
        ROOT / "assets/flywheelpower/blocktypes/flywheel.json": blocktype(False),
        ROOT / "assets/flywheelpower/blocktypes/compactflywheel.json": blocktype(True),
        ROOT / "assets/flywheelpower/itemtypes/bearingfittings.json": bearing_fittings_itemtype(),
        ROOT / "assets/flywheelpower/itemtypes/flywheelbearing.json": bearing_itemtype(),
        ROOT / "assets/flywheelpower/itemtypes/flywheelrim.json": rim_itemtype(),
        ROOT / "assets/flywheelpower/itemtypes/flywheelweb.json": web_itemtype(),
        ROOT / "assets/flywheelpower/recipes/grid/flywheel-components.json": components_recipes(),
        ROOT / "assets/flywheelpower/recipes/grid/flywheel-assembly.json": assembly_recipes(),
        ROOT / "assets/flywheelpower/recipes/smithing/bearingfittings.json": smithing_recipes(),
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
