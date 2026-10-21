#!/usr/bin/env python3
"""Valida data/weapons/weapon_mods.json (Bigorna) sem modificar nada. Somente stdlib.

Checagens (erro = saida 1):
  - JSON valido, array "mods", ids unicos e snake_case
  - weaponId existe em scripts/systems/weapons/WeaponCatalog.cs
  - efeitos: type conhecido, field em {value, value2} sem repeticao no mesmo mod, value numerico finito
  - weaponmod.<id>.name/.desc/.flavor em content.json dos 3 idiomas, com placeholders {0}/{1} iguais entre idiomas
  - cada weapon.mod.<id>.<field> registrada em DevParameters.*.cs com Default igual ao JSON
Aviso: placeholders da descricao que excedem o numero de efeitos; chaves weapon.mod.<id>.* registradas sem mod.
"""

from __future__ import annotations

import json
import math
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
LANGUAGES = ["pt-BR", "en", "es"]
KNOWN_TYPES = {
    "AttackRange.Multiplier",
    "BasicQuickDamage.Multiplier",
    "AttackTiming.Reduction",
    "Knockback.Multiplier",
    "Stagger.Multiplier",
    "SpecialRadius.Multiplier",
    "SpecialCooldown.Reduction",
}
KNOWN_FIELDS = {"value", "value2"}
SNAKE = re.compile(r"^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$")
PLACEHOLDER = re.compile(r"(?<!\{)\{(\d+)(?:[,:][^}]*)?\}(?!\})")


def weapon_ids(errors: list[str]) -> set[str]:
    path = ROOT / "scripts/systems/weapons/WeaponCatalog.cs"
    try:
        text = path.read_text(encoding="utf-8-sig")
    except OSError as exc:
        errors.append(f"cannot read {path}: {exc}")
        return set()
    ids = set(re.findall(r'Placeholder\(\s*"([a-z0-9_]+)"', text))
    # arma completa: "new(" sozinho na linha, id na linha seguinte
    ids |= set(re.findall(r'^\s*new\(\s*\r?\n\s*"([a-z0-9_]+)"\s*,', text, re.M))
    return ids


def registered_defaults() -> dict[str, float]:
    result: dict[str, float] = {}
    for f in sorted((ROOT / "scripts/systems").glob("DevParameters.*.cs")):
        text = f.read_text(encoding="utf-8-sig")
        for m in re.finditer(r'\bP\(\s*"(weapon\.mod\.[\w.]+)"\s*,\s*"[^"]*"\s*,\s*\w+\s*,\s*(-?\d+(?:\.\d+)?)', text):
            result[m.group(1)] = float(m.group(2))
    return result


def main() -> int:
    errors: list[str] = []
    warnings: list[str] = []
    path = ROOT / "data/weapons/weapon_mods.json"
    try:
        data = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError) as exc:
        print(f"ERRO: cannot read {path}: {exc}")
        return 1
    mods = data.get("mods") if isinstance(data, dict) else None
    if not isinstance(mods, list):
        print("ERRO: weapon_mods.json precisa de um array 'mods'")
        return 1

    weapons = weapon_ids(errors)
    if not weapons:
        errors.append("nenhum id de arma encontrado em WeaponCatalog.cs")
    registered = registered_defaults()

    content: dict[str, dict[str, str]] = {}
    for lang in LANGUAGES:
        p = ROOT / "data/localization" / lang / "content.json"
        try:
            content[lang] = json.loads(p.read_text(encoding="utf-8-sig"))
        except (OSError, json.JSONDecodeError) as exc:
            errors.append(f"cannot read {p}: {exc}")
            content[lang] = {}

    seen: set[str] = set()
    expected_keys: set[str] = set()
    for index, mod in enumerate(mods):
        where = f"mods[{index}]"
        if not isinstance(mod, dict):
            errors.append(f"{where}: precisa ser objeto")
            continue
        mid = mod.get("id")
        if not isinstance(mid, str) or not SNAKE.match(mid):
            errors.append(f"{where}: id invalido (snake_case): {mid!r}")
            continue
        where = f"mod '{mid}'"
        if mid in seen:
            errors.append(f"{where}: id duplicado")
        seen.add(mid)

        wid = mod.get("weaponId")
        if wid not in weapons:
            errors.append(f"{where}: weaponId '{wid}' nao existe em WeaponCatalog.cs ({', '.join(sorted(weapons))})")

        effects = mod.get("effects")
        if not isinstance(effects, list) or not effects:
            errors.append(f"{where}: 'effects' precisa ser lista nao vazia")
            effects = []
        fields: set[str] = set()
        for ei, eff in enumerate(effects):
            ew = f"{where} effects[{ei}]"
            if not isinstance(eff, dict):
                errors.append(f"{ew}: precisa ser objeto")
                continue
            if eff.get("type") not in KNOWN_TYPES:
                errors.append(f"{ew}: type desconhecido: {eff.get('type')!r}")
            field = eff.get("field", "value")
            if field not in KNOWN_FIELDS:
                errors.append(f"{ew}: field desconhecido: {field!r}")
            elif field in fields:
                errors.append(f"{ew}: field '{field}' repetido no mesmo mod")
            fields.add(field)
            value = eff.get("value")
            if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value):
                errors.append(f"{ew}: value precisa ser numero finito: {value!r}")
                continue
            key = f"weapon.mod.{mid}.{field}"
            if key not in registered:
                errors.append(f"{key}: nao registrada em scripts/systems/DevParameters.*.cs")
            elif not math.isclose(registered[key], float(value), rel_tol=1e-9, abs_tol=1e-9):
                errors.append(f"{key}: Default registrado {registered[key]} != JSON {value}")

        placeholders: dict[str, set[str]] = {}
        for lang in LANGUAGES:
            for suffix in ("name", "desc", "flavor"):
                key = f"weaponmod.{mid}.{suffix}"
                expected_keys.add(key)
                text = content[lang].get(key)
                if not isinstance(text, str) or not text.strip():
                    errors.append(f"{lang}/content.json: falta {key}")
                elif suffix == "desc":
                    placeholders[lang] = set(PLACEHOLDER.findall(text))
        if len({frozenset(v) for v in placeholders.values()}) > 1:
            errors.append(f"{where}: placeholders da descricao diferem entre idiomas: "
                          + ", ".join(f"{l}={sorted(v)}" for l, v in placeholders.items()))
        for lang, ph in placeholders.items():
            if any(int(n) >= max(len(effects), 1) for n in ph):
                warnings.append(f"{where}: {lang} usa placeholder acima do numero de efeitos ({sorted(ph)})")

    # sobras
    for key in registered:
        m = re.match(r"weapon\.mod\.(\w+?)\.(value2?)$", key)
        if m and m.group(1) not in seen:
            warnings.append(f"{key}: registrada sem mod correspondente no JSON")
    for lang in LANGUAGES:
        for key in content[lang]:
            m = re.match(r"weaponmod\.(\w+?)\.(name|desc|flavor)$", key)
            if m and m.group(1) not in seen:
                warnings.append(f"{lang}/content.json: {key} sem mod correspondente no JSON")

    print(f"validate_weapon_mods: {len(seen)} mods, {len(weapons)} armas conhecidas, "
          f"{len(registered)} chaves weapon.mod.* registradas, {len(expected_keys)} chaves de texto por idioma.")
    for w in warnings:
        print("AVISO:", w)
    for e in errors:
        print("ERRO:", e)
    print(f"{len(errors)} erro(s), {len(warnings)} aviso(s).")
    return 1 if errors else 0


if __name__ == "__main__":
    try:
        sys.stdout.reconfigure(encoding="utf-8")  # type: ignore[attr-defined]
    except Exception:
        pass
    raise SystemExit(main())
