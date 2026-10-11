#!/usr/bin/env python3
"""Validate the Vampire's Prey localization resources without modifying them.

Layout: data/localization/<language>/<domain>.json, flat {"key": "text"} objects.
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

LANGUAGES = ["pt-BR", "en", "es"]
REFERENCE_LANGUAGE = "pt-BR"

# Prefixes of keys built at runtime (e.g. f"blessing.{id}.name"). Keys starting with
# one of these are not reported as unreferenced. Add new dynamic prefixes here.
# "portal." keys are referenced from ScenePortal.PromptText in .tscn files, not in C#.
DYNAMIC_PREFIXES = ["blessing.", "artifact.", "skill.", "weapon.", "boss.", "map.", "enemy.", "portal."]

PLACEHOLDER = re.compile(r"(?<!\{)\{(\d+)(?:[,:][^}]*)?\}(?!\})")
# Literal keys passed to Localization.Text/TextFormat/Has (strict: missing = error).
CODE_KEY = re.compile(r'Localization\s*\.\s*(?:Text|TextFormat|Has)\s*\(\s*"([^"\\]+)"')
# Literal keys passed to UiComponents.TextOr (have a fallback: missing = warning).
FALLBACK_KEY = re.compile(r'\bTextOr\s*\(\s*"([^"\\]+)"')


def project_root() -> Path:
    return Path(__file__).resolve().parents[1]


def load_flat(path: Path, label: str, errors: list[str], warnings: list[str]) -> dict[str, str] | None:
    def no_duplicates(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                errors.append(f"{label}: duplicate key in file: {key}")
            result[key] = value
        return result

    try:
        data = json.loads(path.read_text(encoding="utf-8-sig"), object_pairs_hook=no_duplicates)
    except (OSError, json.JSONDecodeError) as exc:
        errors.append(f"cannot read {label}: {exc}")
        return None
    if not isinstance(data, dict):
        errors.append(f"{label}: root must be an object")
        return None
    table: dict[str, str] = {}
    for key, value in data.items():
        if isinstance(value, dict):
            errors.append(f"{label}: {key} must be a string (nested objects are not supported)")
            continue
        if not isinstance(value, str):
            errors.append(f"{label}: {key} must be a string")
            continue
        if not value.strip():
            warnings.append(f"{label}: {key} is empty")
        table[key] = value
    return table


def placeholders(text: str) -> list[str]:
    return sorted(set(PLACEHOLDER.findall(text)), key=int)


def main() -> int:
    args = [arg for arg in sys.argv[1:] if not arg.startswith("-")]
    verbose = "-v" in sys.argv[1:] or "--verbose" in sys.argv[1:]
    root = Path(args[0]).resolve() if args else project_root()
    loc_root = root / "data" / "localization"
    errors: list[str] = []
    warnings: list[str] = []

    for stray in sorted(loc_root.glob("*.json")):
        errors.append(f"{stray.relative_to(root).as_posix()}: texts must live in data/localization/<language>/")

    # tables[language][domain] = {key: text}
    tables: dict[str, dict[str, dict[str, str]]] = {}
    for language in LANGUAGES:
        folder = loc_root / language
        if not folder.is_dir():
            errors.append(f"missing language folder: {folder.relative_to(root).as_posix()}")
            tables[language] = {}
            continue
        tables[language] = {}
        for path in sorted(folder.glob("*.json")):
            table = load_flat(path, path.relative_to(root).as_posix(), errors, warnings)
            if table is not None:
                tables[language][path.stem] = table

    # Duplicate keys across files of the same language.
    for language, domains in tables.items():
        owner: dict[str, str] = {}
        for domain, table in domains.items():
            for key in table:
                if key in owner:
                    errors.append(f"{language}: key {key} defined in both {owner[key]}.json and {domain}.json")
                else:
                    owner[key] = domain

    # Parity and placeholders per domain file.
    all_domains = sorted({domain for domains in tables.values() for domain in domains})
    for domain in all_domains:
        for language in LANGUAGES:
            if domain not in tables[language]:
                errors.append(f"{language}: missing file {domain}.json")
        reference = tables[REFERENCE_LANGUAGE].get(domain, {})
        all_keys = sorted({key for language in LANGUAGES for key in tables[language].get(domain, {})})
        for key in all_keys:
            for language in LANGUAGES:
                if domain in tables[language] and key not in tables[language][domain]:
                    errors.append(f"{language}/{domain}.json: missing key {key}")
            if key not in reference:
                continue
            expected = placeholders(reference[key])
            for language in LANGUAGES:
                if language == REFERENCE_LANGUAGE:
                    continue
                text = tables[language].get(domain, {}).get(key)
                if text is not None and placeholders(text) != expected:
                    errors.append(
                        f"{language}/{domain}.json: {key} placeholders {placeholders(text)} differ from "
                        f"{REFERENCE_LANGUAGE} {expected}")

    # Keys referenced from C#.
    reference_keys = {key for table in tables[REFERENCE_LANGUAGE].values() for key in table}
    strict_refs: dict[str, str] = {}
    fallback_refs: dict[str, str] = {}
    for path in sorted((root / "scripts").rglob("*.cs")):
        source = path.read_text(encoding="utf-8-sig", errors="replace")
        rel = path.relative_to(root).as_posix()
        for key in CODE_KEY.findall(source):
            strict_refs.setdefault(key, rel)
        for key in FALLBACK_KEY.findall(source):
            fallback_refs.setdefault(key, rel)
    for key, rel in sorted(strict_refs.items()):
        if key not in reference_keys:
            errors.append(f"{rel}: key {key} is not defined in {REFERENCE_LANGUAGE}")
    pending_by_file: dict[str, list[str]] = {}
    for key, rel in sorted(fallback_refs.items()):
        if key not in reference_keys and key not in strict_refs:
            pending_by_file.setdefault(rel, []).append(key)
    for rel, keys in sorted(pending_by_file.items()):
        if verbose:
            for key in keys:
                warnings.append(f"{rel}: TextOr key {key} is not defined yet (fallback text is shown)")
        else:
            warnings.append(f"{rel}: {len(keys)} TextOr key(s) not defined yet (run with -v to list)")

    referenced = set(strict_refs) | set(fallback_refs)
    for key in sorted(reference_keys - referenced):
        if not any(key.startswith(prefix) for prefix in DYNAMIC_PREFIXES):
            warnings.append(f"{REFERENCE_LANGUAGE}: key {key} has no literal reference in scripts/")

    for message in warnings:
        print(f"WARNING: {message}")
    for message in errors:
        print(f"ERROR: {message}")
    counts = ", ".join(
        f"{language}={sum(len(table) for table in tables[language].values())}" for language in LANGUAGES)
    print(f"Validated {len(all_domains)} domain file(s) per language ({counts}); "
          f"{len(strict_refs)} literal key(s) in C#: {len(errors)} error(s), {len(warnings)} warning(s)")
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
