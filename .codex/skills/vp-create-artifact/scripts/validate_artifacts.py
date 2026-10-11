#!/usr/bin/env python3
"""Validate the Vampire's Prey artifact catalog without modifying it."""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

REQUIRED = {"id", "saveName", "icon", "rarity", "slot", "weight", "tags", "effects"}
# Texts live in data/localization/<language>/content.json (artifact.<id>.name/.desc/.flavor and
# artifact.effect.<Type>). "saveName" is the legacy identifier persisted in saves: never displayed, never renamed.
FORBIDDEN_TEXT_FIELDS = {"name", "description", "flavorText"}
LANGUAGES = ["pt-BR", "en", "es"]
TEXT_SUFFIXES = ["name", "desc", "flavor"]
SLOTS = {"Passive", "Active"}
ID_PATTERN = re.compile(r"^[a-z0-9]+(?:_[a-z0-9]+)*$")


def project_root() -> Path:
    return Path(__file__).resolve().parents[4]


def load_content(root: Path, errors: list[str]) -> dict[str, dict[str, str]]:
    """Merge every <language>/*.json (keys are unique per language across files)."""
    tables: dict[str, dict[str, str]] = {}
    for language in LANGUAGES:
        table: dict[str, str] = {}
        folder = root / "data" / "localization" / language
        if not folder.is_dir():
            errors.append(f"missing localization folder {folder}")
        for path in sorted(folder.glob("*.json")):
            try:
                loaded = json.loads(path.read_text(encoding="utf-8-sig"))
            except (OSError, json.JSONDecodeError) as exc:
                errors.append(f"cannot read {path}: {exc}")
                continue
            if isinstance(loaded, dict):
                table.update({k: v for k, v in loaded.items() if isinstance(v, str)})
        tables[language] = table
    return tables


def has_text(texts: dict[str, dict[str, str]], language: str, key: str) -> bool:
    return bool(texts.get(language, {}).get(key, "").strip())


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
    texts = load_content(root, errors)
    for language in LANGUAGES:
        for key in ("artifact.effect_value.active", "artifact.effect_value.inactive"):
            if not has_text(texts, language, key):
                errors.append(f"missing {language} text {key}")
    save_names: set[str] = set()
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
        leftover = FORBIDDEN_TEXT_FIELDS & artifact.keys()
        if leftover:
            errors.append(f"{label}: move {', '.join(sorted(leftover))} to content.json (artifact.<id>.name/.desc/.flavor)")
        save_name = artifact.get("saveName")
        if not isinstance(save_name, str) or not save_name.strip():
            errors.append(f"{label}.saveName must be a non-empty string (legacy save identifier)")
        elif save_name in save_names:
            errors.append(f"duplicate artifact saveName: {save_name}")
        else:
            save_names.add(save_name)
        if isinstance(artifact_id, str):
            for language in LANGUAGES:
                for suffix in TEXT_SUFFIXES:
                    if not has_text(texts, language, f"artifact.{artifact_id}.{suffix}"):
                        errors.append(f"{artifact_id}: missing {language} text artifact.{artifact_id}.{suffix}")
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
            if isinstance(effect_type, str):
                for language in LANGUAGES:
                    if not has_text(texts, language, f"artifact.effect.{effect_type}"):
                        errors.append(f"{artifact_id or label}: missing {language} effect name artifact.effect.{effect_type}")
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

