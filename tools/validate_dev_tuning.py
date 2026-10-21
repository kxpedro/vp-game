#!/usr/bin/env python3
"""Valida o registro de parametros de dev (DevParameterRegistry / CompendiumTuning).

Checagens (parse por regex, sem compilar o C#):
  (a) ERRO   chave lida no codigo (CompendiumTuning.GetFloat/GetInt/GetNumber e G() do EnemyBasic)
             que nao existe nos Register* de scripts/systems/DevParameters.*.cs
  (b) ERRO   chave literal registrada duas vezes
  (c) AVISO  chave registrada que nenhum codigo le
  (d) ERRO   Min <= Default <= Max quando os valores sao literais numericos simples
  (e) data/tuning/tuning_overrides.json (valores promovidos): ERRO se o JSON/formato for invalido ou o valor
      sair da faixa registrada (quando literal); AVISO se a chave nao casar com nenhum registro

Chaves interpoladas ($"...{x}...") viram padroes: cada {...} casa qualquer segmento [\\w.-]+.
Limites conhecidos (falsos positivos/negativos possiveis):
  - padroes sao frouxos (ex.: {key}.damage casa qualquer coisa terminada em .damage);
  - chaves lidas por helper com argumento nao literal (GetFloat(key, ...)) nao sao verificadas;
  - "lida" em (c) inclui qualquer aparicao do literal entre aspas em .cs fora dos DevParameter*.cs.
Saida 1 se houver erro; aviso nao conta.
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SCRIPTS = ROOT / "scripts"
SHIPPED = ROOT / "data" / "tuning" / "tuning_overrides.json"
WILD = r"[\w.\-]+"

# Excecoes: chaves lidas pelo codigo que NAO precisam estar registradas. Mantenha curta e comentada.
# Cada item e uma regex de fullmatch sobre a chave (ou padrao com {} ja normalizado para "{}").
READ_EXCEPTIONS: list[tuple[str, str]] = [
    # (regex, motivo)
]

# Chaves registradas sem leitura via CompendiumTuning (armazenamento externo): nao entram em (c).
UNREAD_OK_PREFIXES = (
    "game.",  # ajustes rapidos: GameTuning (user://game_tuning.cfg), via Getter/Setter
)


def line_of(text: str, pos: int) -> int:
    return text.count("\n", 0, pos) + 1


def split_args(text: str, start: int) -> tuple[list[str], int]:
    """Quebra os argumentos da chamada cujo '(' esta em text[start-1]. Respeita strings e aninhamento."""
    args: list[str] = []
    depth = 0
    i = start
    cur = start
    n = len(text)
    while i < n:
        c = text[i]
        if c == '"':
            i += 1
            while i < n and text[i] != '"':
                i += 2 if text[i] == "\\" else 1
        elif c in "([{":
            depth += 1
        elif c in ")]}":
            if depth == 0:
                args.append(text[cur:i].strip())
                return args, i
            depth -= 1
        elif c == "," and depth == 0:
            args.append(text[cur:i].strip())
            cur = i + 1
        i += 1
    return args, n


def parse_string_arg(arg: str) -> tuple[bool, str] | None:
    m = re.fullmatch(r'(\$?)"((?:[^"\\]|\\.)*)"', arg)
    if not m:
        return None
    return bool(m.group(1)), m.group(2)


def to_regex(key: str, interpolated: bool) -> re.Pattern[str]:
    if not interpolated:
        return re.compile(re.escape(key))
    parts = re.split(r"\{[^}]*\}", key)
    return re.compile(WILD.join(re.escape(p) for p in parts))


def normalize(key: str, interpolated: bool) -> str:
    return re.sub(r"\{[^}]*\}", "{}", key) if interpolated else key


NUM = re.compile(r"-?\d+(?:\.\d+)?[dDfF]?")


def num(arg: str) -> float | None:
    a = arg.strip()
    return float(a.rstrip("dDfF")) if NUM.fullmatch(a) else None


def strip_comments(text: str) -> str:
    # remove // ate o fim da linha fora de strings (aproximado: ignora // dentro de "...")
    out = []
    for line in text.splitlines():
        res, i, in_str = [], 0, False
        while i < len(line):
            c = line[i]
            if c == '"' and (i == 0 or line[i - 1] != "\\"):
                in_str = not in_str
            if not in_str and line.startswith("//", i):
                break
            res.append(c)
            i += 1
        out.append("".join(res))
    return "\n".join(out)


class Reg:
    def __init__(self, key, interp, file, line, kind):
        self.key, self.interp, self.file, self.line, self.kind = key, interp, file, line, kind
        self.regex = to_regex(key, interp or "{m}" in key)
        self.is_pattern = interp or "{m}" in key
        self.min = self.max = None


def main() -> int:
    errors: list[str] = []
    warnings: list[str] = []

    dev_files = sorted((SCRIPTS / "systems").glob("DevParameters.*.cs"))
    if not dev_files:
        print("ERRO: nenhum scripts/systems/DevParameters.*.cs encontrado")
        return 1

    registered: list[Reg] = []
    range_checked = 0
    q_re = re.compile(r"\b(P|Q|RegisterPerMap|RegisterEffectValue)\(")
    full_re = re.compile(r"\bRegister\(\s*new DevParameter\(")

    for f in dev_files:
        raw = f.read_text(encoding="utf-8")
        text = strip_comments(raw)
        rel = f.relative_to(ROOT).as_posix()
        calls = []
        # variaveis locais/const de chave (const string key = "weapon.longsword"; var key = $"a.{id}") sao substituidas em {nome}
        consts = {m.group(1): m.group(2) for m in re.finditer(r'(?:const string|var)\s+(\w+)\s*=\s*\$?"([^"]*)"\s*;', text)}
        for m in q_re.finditer(text):
            # ignora declaracoes/locais: 'static void P(' etc. -> precedidos por tipo
            before = text[max(0, m.start() - 12): m.start()]
            if re.search(r"(void|bool|double|string)\s+$", before):
                continue
            args, _ = split_args(text, m.end())
            calls.append((m.group(1), args, m.start()))
        for m in full_re.finditer(text):
            args, _ = split_args(text, m.end())
            calls.append(("Register", args, m.start()))

        for kind, args, pos in calls:
            if not args:
                continue
            s = parse_string_arg(args[0])
            if s is None:
                continue  # chave nao literal (ex.: parametro do helper)
            interp, key = s
            for _ in range(3):
                key = re.sub(r"\{(\w+)\}", lambda mm: consts.get(mm.group(1), mm.group(0)), key) if interp else key
            ln = line_of(text, pos)
            reg = Reg(key, interp, rel, ln, kind)
            registered.append(reg)
            if kind == "Q":
                reg.min, reg.max = 1.0, 250.0  # GameTuning: 1..250 %
                continue
            # (d) faixa Min <= Default <= Max
            if kind == "RegisterPerMap":
                # (keyPattern, label, group, defaults[], min, max, step, isInt, ...)
                if len(args) >= 6:
                    arr = re.search(r"\{([^}]*)\}", args[3])
                    mn, mx = num(args[4]), num(args[5])
                    reg.min, reg.max = mn, mx
                    if arr and mn is not None and mx is not None:
                        vals = [num(v) for v in arr.group(1).split(",") if v.strip()]
                        for i, v in enumerate(vals):
                            if v is None:
                                continue
                            range_checked += 1
                            if not (mn <= v <= mx):
                                errors.append(f"{rel}:{ln}: {key} (mapa {i}): default {v} fora de [{mn}, {mx}]")
                        if mn > mx:
                            errors.append(f"{rel}:{ln}: {key}: Min {mn} > Max {mx}")
            else:
                # P(key,label,group,def,min,max,...) ; Register(new DevParameter(key,label,group,def,min,max,...)
                if len(args) >= 6:
                    d, mn, mx = num(args[3]), num(args[4]), num(args[5])
                    reg.min, reg.max = mn, mx
                    if None not in (d, mn, mx):
                        range_checked += 1
                        if not (mn <= d <= mx):
                            errors.append(f"{rel}:{ln}: {key}: esperado Min <= Default <= Max, obtido {mn} / {d} / {mx}")

    # (b) duplicatas literais
    seen: dict[str, Reg] = {}
    for r in registered:
        if r.is_pattern:
            continue
        if r.key in seen:
            o = seen[r.key]
            errors.append(f"{r.file}:{r.line}: chave duplicada '{r.key}' (ja em {o.file}:{o.line})")
        else:
            seen[r.key] = r
    # duplicatas de padrao identico
    pseen: dict[str, Reg] = {}
    for r in registered:
        if not r.is_pattern:
            continue
        nk = normalize(r.key, True)
        if nk in pseen:
            o = pseen[nk]
            warnings.append(f"{r.file}:{r.line}: padrao '{r.key}' repete o de {o.file}:{o.line} (confira se e o mesmo laco)")
        else:
            pseen[nk] = r

    # Amostras para casar padrao-usado contra padrao-registrado: {x} -> "x"
    def sample(reg: Reg) -> str:
        return re.sub(r"\{[^}]*\}", "0", reg.key)

    # Usos
    use_re = re.compile(r'(?:CompendiumTuning\.(?:GetFloat|GetInt|GetNumber)|(?<![\w.])G)\(\s*(?=\$?")')
    used: list[tuple[str, bool, str, int]] = []  # key, interp, file, line
    non_dev_text = []
    for f in sorted(SCRIPTS.rglob("*.cs")):
        if f.name.startswith("DevParameter"):
            continue
        raw = f.read_text(encoding="utf-8")
        text = strip_comments(raw)
        rel = f.relative_to(ROOT).as_posix()
        non_dev_text.append(text)
        for m in use_re.finditer(text):
            args, _ = split_args(text, m.end())
            if not args:
                continue
            s = parse_string_arg(args[0])
            if s is None:
                continue
            interp, key = s
            used.append((key, interp, rel, line_of(text, m.start())))

    all_text = "\n".join(non_dev_text)
    exc = [(re.compile(rx), why) for rx, why in READ_EXCEPTIONS]

    # (a) cada chave lida precisa estar registrada
    unresolved = 0
    for key, interp, rel, ln in used:
        nk = normalize(key, interp)
        if any(rx.fullmatch(nk) for rx, _ in exc):
            continue
        urx = to_regex(key, interp)
        ok = False
        for r in registered:
            if not interp and not r.is_pattern:
                ok = r.key == key
            elif not interp:
                ok = bool(r.regex.fullmatch(key))
            elif not r.is_pattern:
                ok = bool(urx.fullmatch(r.key))
            else:
                ok = bool(urx.fullmatch(sample(r))) or bool(r.regex.fullmatch(re.sub(r"\{[^}]*\}", "0", key)))
            if ok:
                break
        if not ok:
            unresolved += 1
            errors.append(f"{rel}:{ln}: chave lida nao registrada: {'$' if interp else ''}\"{key}\"")

    # (c) registradas que ninguem le (aviso)
    unread = 0
    used_lit = {k for k, i, _, _ in used if not i}
    used_pat = [(to_regex(k, True), k) for k, i, _, _ in used if i]
    for r in registered:
        if r.is_pattern or r.kind == "Q" or r.key.startswith(UNREAD_OK_PREFIXES):
            continue
        if r.key in used_lit:
            continue
        if any(rx.fullmatch(r.key) for rx, _ in used_pat):
            continue
        if f'"{r.key}"' in all_text:
            continue  # lida por outro helper
        unread += 1
        warnings.append(f"{r.file}:{r.line}: chave registrada que nenhum codigo le: {r.key}")

    # (e) valores promovidos para o jogo
    shipped_count = 0
    if SHIPPED.exists():
        rel = SHIPPED.relative_to(ROOT).as_posix()
        try:
            data = json.loads(SHIPPED.read_text(encoding="utf-8"))
        except (ValueError, UnicodeDecodeError) as exc:
            data = None
            errors.append(f"{rel}: JSON invalido: {exc}")
        if data is not None:
            values = data.get("values") if isinstance(data, dict) else None
            if not isinstance(data, dict) or not isinstance(data.get("version"), int) or not isinstance(values, dict):
                errors.append(f"{rel}: esperado {{\"version\": int, \"values\": {{chave: numero}}}}")
            else:
                for key, value in values.items():
                    shipped_count += 1
                    if isinstance(value, bool) or not isinstance(value, (int, float)):
                        errors.append(f"{rel}: {key}: valor nao numerico ({value!r})")
                        continue
                    match = next((r for r in registered if not r.is_pattern and r.key == key), None)
                    match = match or next((r for r in registered if r.is_pattern and r.regex.fullmatch(key)), None)
                    if match is None:
                        warnings.append(f"{rel}: {key}: chave sem registro em DevParameters.*.cs (o jogo le, mas o F1 nao mostra)")
                    elif match.min is not None and match.max is not None and not (match.min <= value <= match.max):
                        errors.append(f"{rel}: {key} = {value} fora de [{match.min}, {match.max}] ({match.file}:{match.line})")

    n_lit = sum(1 for r in registered if not r.is_pattern)
    n_pat = len(registered) - n_lit
    print(f"validate_dev_tuning: {len(registered)} chamadas de registro ({n_lit} literais, {n_pat} padroes/por-mapa/laco), "
          f"{len(used)} leituras com chave literal ou interpolada, {range_checked} faixas conferidas, "
          f"{shipped_count} valor(es) promovido(s) em data/tuning.")
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
