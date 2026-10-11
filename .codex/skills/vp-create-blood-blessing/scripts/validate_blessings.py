#!/usr/bin/env python3
"""Validate the Vampire's Prey blood blessing catalog without modifying it."""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

REQUIRED = {"id", "family", "rarity", "weight", "maxStacks", "tags", "effects"}
# Texts live in data/localization/<language>/content.json, never in the catalog.
FORBIDDEN_TEXT_FIELDS = {"name", "description", "flavorText"}
LANGUAGES = ["pt-BR", "en", "es"]
TEXT_SUFFIXES = ["name", "desc", "flavor"]
PLACEHOLDER = re.compile(r"(?<!\{)\{(\d+)(?:[,:][^}]*)?\}(?!\})")
KEYWORD_OPEN = re.compile(r"\[kw=([a-z_]+)\]")
KEYWORD_IDS = {"basic_attack", "quick_attack", "special_attack", "fury", "bleed", "pestilence", "barrier",
               "fascination", "host", "interrupt", "wound", "charm"}
ID_PATTERN = re.compile(r"^[a-z0-9]+(?:_[a-z0-9]+)*$")
DIRECT_MOVESET_TAGS = {"basic_attack", "quick_attack", "special_attack", "bestial_fury"}
DIRECT_MOVESET_GROUPS = {
    "basic_attack": "basic_attack_status",
    "quick_attack": "quick_attack_status",
    "special_attack": "special_attack_status",
    "bestial_fury": "bestial_fury_mode",
}


def project_root() -> Path:
    return Path(__file__).resolve().parents[4]


def strip_json_comments(text: str) -> str:
    """Remove // and /* */ comments while preserving quoted JSON strings."""
    result: list[str] = []
    index = 0
    in_string = False
    escaped = False
    while index < len(text):
        char = text[index]
        next_char = text[index + 1] if index + 1 < len(text) else ""
        if in_string:
            result.append(char)
            if escaped:
                escaped = False
            elif char == "\\":
                escaped = True
            elif char == '"':
                in_string = False
            index += 1
            continue
        if char == '"':
            in_string = True
            result.append(char)
            index += 1
            continue
        if char == "/" and next_char == "/":
            index += 2
            while index < len(text) and text[index] not in "\r\n":
                index += 1
            continue
        if char == "/" and next_char == "*":
            index += 2
            while index + 1 < len(text) and text[index : index + 2] != "*/":
                index += 1
            index += 2
            continue
        result.append(char)
        index += 1
    return re.sub(r",\s*([}\]])", r"\1", "".join(result))


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


def check_texts(blessing_id: str, effects: list, texts: dict[str, dict[str, str]], errors: list[str]) -> None:
    for language in LANGUAGES:
        table = texts.get(language, {})
        for suffix in TEXT_SUFFIXES:
            key = f"blessing.{blessing_id}.{suffix}"
            if not table.get(key, "").strip():
                errors.append(f"{blessing_id}: missing {language} text {key}")
        description = table.get(f"blessing.{blessing_id}.desc", "")
        for index in PLACEHOLDER.findall(description):
            index = int(index)
            if index >= len(effects):
                errors.append(f"{blessing_id}: {language} desc uses {{{index}}} but there are {len(effects)} effect(s)")
            elif isinstance(effects[index], dict) and (
                isinstance(effects[index].get("value"), bool) or effects[index].get("operation") in ("Set", "SetBool")
            ):
                errors.append(f"{blessing_id}: {language} desc uses {{{index}}} for a boolean/structural effect")
        for keyword in KEYWORD_OPEN.findall(description):
            if keyword not in KEYWORD_IDS:
                errors.append(f"{blessing_id}: {language} desc uses unknown keyword [kw={keyword}]")
        if description.count("[kw=") != description.count("[/kw]"):
            errors.append(f"{blessing_id}: {language} desc has unbalanced [kw]...[/kw] markup")


def main() -> int:
    root = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else project_root()
    catalog_path = root / "data" / "blessings" / "blood_blessings.json"
    errors: list[str] = []
    warnings: list[str] = []
    try:
        data = json.loads(strip_json_comments(catalog_path.read_text(encoding="utf-8-sig")))
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
    family_entries = data.get("families")
    if not isinstance(family_entries, list) or not family_entries:
        print("ERROR: root.families must be a non-empty array")
        return 1
    families = {
        family.get("name")
        for family in family_entries
        if isinstance(family, dict) and isinstance(family.get("name"), str)
    }
    texts = load_content(root, errors)
    for language in LANGUAGES:
        for keyword in sorted(KEYWORD_IDS):
            for suffix in ("term", "hint"):
                if not texts.get(language, {}).get(f"blessing.keyword.{keyword}.{suffix}", "").strip():
                    errors.append(f"missing {language} text blessing.keyword.{keyword}.{suffix}")
        for family in family_entries:
            if isinstance(family, dict) and family.get("id") and not texts.get(language, {}).get(
                    f"blessing.family.{family['id']}.theme", "").strip():
                errors.append(f"missing {language} text blessing.family.{family['id']}.theme")
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
        leftover = FORBIDDEN_TEXT_FIELDS & blessing.keys()
        if leftover:
            errors.append(f"{label}: move {', '.join(sorted(leftover))} to content.json (blessing.<id>.name/.desc/.flavor)")
        blessing_id = blessing.get("id")
        if not isinstance(blessing_id, str) or not ID_PATTERN.fullmatch(blessing_id):
            errors.append(f"{label}.id must be unique snake_case")
        elif blessing_id in seen:
            errors.append(f"duplicate blessing id: {blessing_id}")
        else:
            seen.add(blessing_id)
        if blessing.get("family") not in families:
            errors.append(f"{label}.family must be declared in root.families ({', '.join(sorted(families))})")
        tags = blessing.get("tags")
        if not isinstance(tags, list):
            errors.append(f"{label}.tags must be an array")
        effects = blessing.get("effects")
        if not isinstance(effects, list) or not effects:
            errors.append(f"{label}.effects must be a non-empty array")
            continue
        if isinstance(blessing_id, str):
            check_texts(blessing_id, effects, texts, errors)
        direct_by_tag = isinstance(tags, list) and bool(
            DIRECT_MOVESET_TAGS.intersection(tag for tag in tags if isinstance(tag, str))
        )
        direct_by_effect = any(
            isinstance(effect, dict)
            and isinstance(effect.get("type"), str)
            and (
                effect["type"].startswith(("BasicAttack", "QuickAttack", "SpecialAttack", "Fury"))
                or effect["type"] == "UnlockBestialFury"
            )
            for effect in effects
        )
        if (
            blessing.get("family") != "Stirps"
            and (direct_by_tag or direct_by_effect)
            and str(blessing.get("rarity", "")).lower() != "common"
        ):
            errors.append(f"{blessing_id or label}: direct moveset blessings must use Common as their base rarity")
        if blessing.get("family") != "Stirps" and isinstance(tags, list):
            direct_tags = [tag for tag in tags if tag in DIRECT_MOVESET_GROUPS]
            if len(direct_tags) > 1:
                errors.append(f"{blessing_id or label}: direct moveset blessing must target only one attack type")
            elif len(direct_tags) == 1:
                required_group = DIRECT_MOVESET_GROUPS[direct_tags[0]]
                if blessing.get("exclusiveGroup") != required_group:
                    errors.append(
                        f"{blessing_id or label}: {direct_tags[0]} must use exclusiveGroup {required_group!r}"
                    )
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
                    if not texts.get(language, {}).get(f"artifact.effect.{effect_type}", "").strip():
                        errors.append(f"{blessing_id or label}: missing {language} effect name artifact.effect.{effect_type}")
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
