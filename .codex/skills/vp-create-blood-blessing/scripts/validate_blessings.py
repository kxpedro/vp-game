#!/usr/bin/env python3
"""Validate the Vampire's Prey blood blessing catalog without modifying it."""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

REQUIRED = {"id", "name", "family", "rarity", "weight", "maxStacks", "description", "flavorText", "tags", "effects"}
FAMILIES = {"Dracul", "Bathory", "Karnstein", "Orlok"}
ID_PATTERN = re.compile(r"^[a-z0-9]+(?:_[a-z0-9]+)*$")


def project_root() -> Path:
    return Path(__file__).resolve().parents[4]


def main() -> int:
    root = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else project_root()
    catalog_path = root / "data" / "blessings" / "blood_blessings.json"
    errors: list[str] = []
    warnings: list[str] = []
    try:
        data = json.loads(catalog_path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError) as exc:
        print(f"ERROR: cannot read {catalog_path}: {exc}")
        return 1
    scaling = data.get("rarityScaling")
    expected_rarities = {"common", "uncommon", "rare", "epic", "legendary"}
    if not isinstance(scaling, dict) or not expected_rarities.issubset(scaling):
        errors.append("rarityScaling must define common, uncommon, rare, epic and legendary")
    blessings = data.get("blessings")
    if not isinstance(blessings, list):
        print("ERROR: root.blessings must be an array")
        return 1
    source_text = "\n".join(path.read_text(encoding="utf-8-sig", errors="replace") for path in (root / "scripts").rglob("*.cs"))
    seen: set[str] = set()
    for index, blessing in enumerate(blessings):
        label = f"blessings[{index}]"
        if not isinstance(blessing, dict):
            errors.append(f"{label} must be an object")
            continue
        missing = REQUIRED - blessing.keys()
        if missing:
            errors.append(f"{label} missing: {', '.join(sorted(missing))}")
        blessing_id = blessing.get("id")
        if not isinstance(blessing_id, str) or not ID_PATTERN.fullmatch(blessing_id):
            errors.append(f"{label}.id must be unique snake_case")
        elif blessing_id in seen:
            errors.append(f"duplicate blessing id: {blessing_id}")
        else:
            seen.add(blessing_id)
        if blessing.get("family") not in FAMILIES:
            errors.append(f"{label}.family must be one of {', '.join(sorted(FAMILIES))}")
        if not isinstance(blessing.get("tags"), list):
            errors.append(f"{label}.tags must be an array")
        effects = blessing.get("effects")
        if not isinstance(effects, list) or not effects:
            errors.append(f"{label}.effects must be a non-empty array")
            continue
        for effect_index, effect in enumerate(effects):
            effect_label = f"{label}.effects[{effect_index}]"
            if not isinstance(effect, dict):
                errors.append(f"{effect_label} must be an object")
                continue
            for key in ("type", "operation", "value"):
                if key not in effect:
                    errors.append(f"{effect_label} missing {key}")
            effect_type = effect.get("type")
            if isinstance(effect_type, str) and f'"{effect_type}"' not in source_text:
                warnings.append(f"{blessing_id or label}: effect {effect_type!r} has no quoted C# reference")
    for message in warnings:
        print(f"WARNING: {message}")
    for message in errors:
        print(f"ERROR: {message}")
    print(f"Validated {len(blessings)} blessings: {len(errors)} error(s), {len(warnings)} warning(s)")
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())

