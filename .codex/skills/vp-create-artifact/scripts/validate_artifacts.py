#!/usr/bin/env python3
"""Validate the Vampire's Prey artifact catalog without modifying it."""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

REQUIRED = {"id", "name", "icon", "rarity", "slot", "weight", "tags", "description", "flavorText", "effects"}
SLOTS = {"Passive", "Active"}
ID_PATTERN = re.compile(r"^[a-z0-9]+(?:_[a-z0-9]+)*$")


def project_root() -> Path:
    return Path(__file__).resolve().parents[4]


def main() -> int:
    root = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else project_root()
    catalog_path = root / "data" / "artifacts" / "artifacts.json"
    errors: list[str] = []
    warnings: list[str] = []
    try:
        data = json.loads(catalog_path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError) as exc:
        print(f"ERROR: cannot read {catalog_path}: {exc}")
        return 1
    artifacts = data.get("artifacts")
    if not isinstance(artifacts, list):
        print("ERROR: root.artifacts must be an array")
        return 1
    source_text = "\n".join(path.read_text(encoding="utf-8-sig", errors="replace") for path in (root / "scripts").rglob("*.cs"))
    seen: set[str] = set()
    for index, artifact in enumerate(artifacts):
        label = f"artifacts[{index}]"
        if not isinstance(artifact, dict):
            errors.append(f"{label} must be an object")
            continue
        missing = REQUIRED - artifact.keys()
        if missing:
            errors.append(f"{label} missing: {', '.join(sorted(missing))}")
        artifact_id = artifact.get("id")
        if not isinstance(artifact_id, str) or not ID_PATTERN.fullmatch(artifact_id):
            errors.append(f"{label}.id must be unique snake_case")
        elif artifact_id in seen:
            errors.append(f"duplicate artifact id: {artifact_id}")
        else:
            seen.add(artifact_id)
        if artifact.get("slot") not in SLOTS:
            errors.append(f"{label}.slot must be Passive or Active")
        if not isinstance(artifact.get("tags"), list):
            errors.append(f"{label}.tags must be an array")
        effects = artifact.get("effects")
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
                warnings.append(f"{artifact_id or label}: effect {effect_type!r} has no quoted C# reference")
    for message in warnings:
        print(f"WARNING: {message}")
    for message in errors:
        print(f"ERROR: {message}")
    print(f"Validated {len(artifacts)} artifacts: {len(errors)} error(s), {len(warnings)} warning(s)")
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())

