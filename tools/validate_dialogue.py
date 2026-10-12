#!/usr/bin/env python3
"""Validate the Vampire's Prey dialogue data (data/dialogue/*.json) without modifying it.

Checks ids, speakers, enums, conditions, branches/choices, boss/map ids and that every
text_key / name_key exists in data/localization/<language>/dialogue.json for all languages.
Exit code 1 if there is any error (warnings do not fail).
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

LANGUAGES = ["pt-BR", "en", "es"]
ID_PATTERN = re.compile(r"^[a-z][a-z0-9_]*$")
CATEGORIES = {"hub", "bark", "boss_pre", "boss_post", "death", "return"}
TRIGGERS = {"room_enter", "room_cleared", "low_health", "boss_phase"}
CLASSES = {"essential", "specific", "evergreen"}
EMOTIONS = {"neutral", "grim", "amused", "angry", "sad", "afraid", "awe"}
NPC_CATEGORIES = {"hub", "death", "return"}
OUTCOMES = {"died", "victory", "abandoned"}
CODEX_CATEGORIES = {"artifact", "blessing", "skill", "enemy", "boss", "attack", "weapon"}
RANGE_TYPES = {"runs_started", "runs_completed", "deaths", "room", "cursed_blood"}
BOSS_TYPES = {"boss_defeated", "boss_met", "boss_in_room", "died_at_boss"}
MAP_TYPES = {"map", "last_run_map"}
ID_TYPES = {"seen", "not_seen", "flag", "no_flag", "weapon", "blessing_owned"}
BOOL_TYPES = {"boss_room", "in_run"}
MAX_LINE = 180
MAX_BARK_LINE = 90


def project_root() -> Path:
    return Path(__file__).resolve().parents[1]


def strip_jsonc(text: str) -> str:
    """Remove // and /* */ comments and trailing commas, respecting strings."""
    out: list[str] = []
    i, n = 0, len(text)
    while i < n:
        c = text[i]
        if c == '"':
            j = i + 1
            while j < n and text[j] != '"':
                j += 2 if text[j] == "\\" else 1
            out.append(text[i : j + 1])
            i = j + 1
        elif text.startswith("//", i):
            while i < n and text[i] != "\n":
                i += 1
        elif text.startswith("/*", i):
            end = text.find("*/", i + 2)
            i = n if end < 0 else end + 2
        else:
            out.append(c)
            i += 1
    return re.sub(r",(\s*[}\]])", r"\1", "".join(out))


def load_jsonc(path: Path, errors: list[str]):
    try:
        return json.loads(strip_jsonc(path.read_text(encoding="utf-8-sig")))
    except (OSError, json.JSONDecodeError) as exc:
        errors.append(f"cannot read {path.name}: {exc}")
        return None


def load_texts(root: Path, errors: list[str]) -> dict[str, dict[str, str]]:
    tables: dict[str, dict[str, str]] = {}
    for language in LANGUAGES:
        path = root / "data" / "localization" / language / "dialogue.json"
        table: dict[str, str] = {}
        try:
            loaded = json.loads(path.read_text(encoding="utf-8-sig"))
            if isinstance(loaded, dict):
                table = {k: v for k, v in loaded.items() if isinstance(v, str)}
        except (OSError, json.JSONDecodeError) as exc:
            errors.append(f"cannot read {language}/dialogue.json: {exc}")
        tables[language] = table
    return tables


def source_ids(path: Path, pattern: str) -> set[str]:
    try:
        return set(re.findall(pattern, path.read_text(encoding="utf-8-sig")))
    except OSError:
        return set()


class Validator:
    def __init__(self, root: Path) -> None:
        self.root = root
        self.errors: list[str] = []
        self.warnings: list[str] = []
        self.texts = load_texts(root, self.errors)
        self.used_keys: set[str] = set()
        self.bosses = source_ids(root / "scripts" / "bosses" / "BossCatalog.cs", r'new\("([a-z_]+)",\s*\d')
        self.maps = source_ids(root / "scripts" / "systems" / "CampaignCatalog.cs", r'new\("([a-z_]+)",\s*\d')
        self.speakers: dict[str, dict] = {}
        self.conversation_ids: set[str] = set()
        self.conversations: dict[str, dict] = {}

    def err(self, where: str, message: str) -> None:
        self.errors.append(f"{where}: {message}")

    def warn(self, where: str, message: str) -> None:
        self.warnings.append(f"{where}: {message}")

    def check_key(self, where: str, key, *, bark: bool = False) -> None:
        if not isinstance(key, str) or not key:
            self.err(where, "text_key missing")
            return
        self.used_keys.add(key)
        for language in LANGUAGES:
            if not self.texts[language].get(key, "").strip():
                self.err(where, f"key {key} missing in {language}/dialogue.json")
        limit = MAX_BARK_LINE if bark else MAX_LINE
        for language in LANGUAGES:
            text = self.texts[language].get(key, "")
            if ".choice." not in key and len(text) > limit:
                self.warn(where, f"{key} has {len(text)} chars in {language} (limit {limit})")

    # ---- speakers ----
    def check_speakers(self, data) -> None:
        for speaker in (data or {}).get("speakers", []):
            sid = speaker.get("id", "")
            where = f"speaker {sid}"
            if not ID_PATTERN.match(sid):
                self.err(where, "invalid id")
                continue
            if sid in self.speakers:
                self.err(where, "duplicate id")
                continue
            self.speakers[sid] = speaker
            self.check_key(where, speaker.get("name_key"))
            if speaker.get("kind") not in ("npc", "player"):
                self.err(where, "kind must be npc or player")
            portraits = speaker.get("portraits", {})
            if "default" not in portraits:
                self.err(where, "portraits.default is required")
            for name, spec in portraits.items():
                for color_field in ("color", "accent"):
                    value = spec.get(color_field)
                    if value is not None and not re.fullmatch(r"#[0-9A-Fa-f]{6}", value):
                        self.err(where, f"portrait {name}.{color_field} must be #RRGGBB")

    # ---- conditions ----
    def check_condition(self, where: str, cond) -> None:
        if not isinstance(cond, dict) or not cond.get("type"):
            self.err(where, "condition without type")
            return
        ctype = cond["type"]
        if ctype in RANGE_TYPES:
            if "min" not in cond and "max" not in cond:
                self.err(where, f"{ctype} needs min and/or max")
        elif ctype in BOSS_TYPES:
            if cond.get("id") not in self.bosses:
                self.err(where, f"{ctype}: unknown boss id {cond.get('id')!r}")
        elif ctype in MAP_TYPES:
            if cond.get("id") not in self.maps:
                self.err(where, f"{ctype}: unknown map id {cond.get('id')!r}")
        elif ctype in ID_TYPES:
            if not cond.get("id"):
                self.err(where, f"{ctype} needs id")
            elif ctype in ("seen", "not_seen"):
                cond["_ref"] = True
        elif ctype in BOOL_TYPES:
            if not isinstance(cond.get("value"), bool):
                self.err(where, f"{ctype} needs boolean value")
        elif ctype == "health_below":
            if not isinstance(cond.get("ratio"), (int, float)):
                self.err(where, "health_below needs ratio")
        elif ctype == "last_run_outcome":
            if cond.get("value") not in OUTCOMES:
                self.err(where, "last_run_outcome value must be died|victory|abandoned")
        elif ctype == "skill":
            if not cond.get("id"):
                self.err(where, "skill needs id")
        elif ctype == "discovered":
            if cond.get("category") not in CODEX_CATEGORIES or not cond.get("id"):
                self.err(where, "discovered needs a valid category and id")
        elif ctype == "not":
            self.check_condition(where, cond.get("condition"))
        elif ctype == "any":
            children = cond.get("conditions")
            if not isinstance(children, list) or not children:
                self.err(where, "any needs a non-empty conditions list")
            else:
                for child in children:
                    self.check_condition(where, child)
        else:
            self.err(where, f"unknown condition type {ctype!r}")

    # ---- lines ----
    def check_lines(self, where: str, lines, speaker_id: str, bark: bool) -> None:
        if not isinstance(lines, list) or not lines:
            self.err(where, "lines must be a non-empty list")
            return
        for index, line in enumerate(lines, start=1):
            lw = f"{where} line {index}"
            self.check_key(lw, line.get("text_key"), bark=bark)
            line_speaker = line.get("speaker", speaker_id)
            if line_speaker not in self.speakers:
                self.err(lw, f"unknown speaker {line_speaker!r}")
            emotion = line.get("emotion")
            if emotion is not None and emotion not in EMOTIONS:
                self.err(lw, f"invalid emotion {emotion!r}")
            portrait = line.get("portrait")
            if portrait is not None and portrait not in self.speakers.get(line_speaker, {}).get("portraits", {}):
                self.err(lw, f"portrait {portrait!r} not defined for {line_speaker}")

    # ---- conversations ----
    def check_conversation(self, conv: dict) -> None:
        cid = conv.get("id", "")
        where = f"conversation {cid}"
        if not ID_PATTERN.match(cid) or cid in ("speaker", "choice"):
            self.err(where, "invalid id")
            return
        if cid in self.conversation_ids:
            self.err(where, "duplicate id")
            return
        self.conversation_ids.add(cid)
        self.conversations[cid] = conv
        speaker = conv.get("speaker")
        if speaker not in self.speakers:
            self.err(where, f"unknown speaker {speaker!r}")
        category = conv.get("category")
        if category not in CATEGORIES:
            self.err(where, f"invalid category {category!r}")
        trigger = conv.get("trigger")
        if category == "bark":
            if trigger not in TRIGGERS:
                self.err(where, "bark needs a valid trigger")
        elif trigger is not None:
            self.err(where, "trigger is only allowed in bark")
        pclass = conv.get("priority_class")
        if pclass not in CLASSES:
            self.err(where, f"invalid priority_class {pclass!r}")
        priority = conv.get("priority", 0)
        if not isinstance(priority, int) or not 0 <= priority <= 99:
            self.err(where, "priority must be an int 0..99")
        chance = conv.get("chance", 1.0)
        if not isinstance(chance, (int, float)) or not 0 <= chance <= 1:
            self.err(where, "chance must be 0..1")
        if pclass == "essential" and conv.get("repeatable", False):
            self.warn(where, "essential conversation should have repeatable false")
        if conv.get("auto_start") and (pclass != "essential" or category not in NPC_CATEGORIES):
            self.warn(where, "auto_start only applies to essential hub/death/return conversations")
        bark = category == "bark"
        lines = conv.get("lines")
        self.check_lines(where, lines, speaker, bark)
        if bark and isinstance(lines, list) and len(lines) > 2:
            self.err(where, "bark has more than 2 lines")
        for req in conv.get("requirements", []):
            self.check_condition(where, req)

        branches = conv.get("branches", {})
        used_branches: set[str] = set()
        choices = conv.get("choices", [])
        if len(choices) > 3:
            self.err(where, "more than 3 choices")
        choice_ids: set[str] = set()
        for index, choice in enumerate(choices, start=1):
            cw = f"{where} choice {index}"
            chid = choice.get("id", "")
            if not ID_PATTERN.match(chid) or chid in choice_ids:
                self.err(cw, "invalid or duplicate choice id")
            choice_ids.add(chid)
            self.check_key(cw, choice.get("text_key"))
            expected = f"dlg.choice.{cid}.{index}"
            if choice.get("text_key") != expected:
                self.warn(cw, f"text_key differs from convention {expected}")
            nxt = choice.get("next")
            if nxt:
                if nxt not in branches:
                    self.err(cw, f"next {nxt!r} has no branch")
                used_branches.add(nxt)
        for bid, branch in branches.items():
            bw = f"{where} branch {bid}"
            if not ID_PATTERN.match(bid):
                self.err(bw, "invalid branch id")
            blines = branch.get("lines", [])
            if not 1 <= len(blines) <= 3:
                self.err(bw, "branch needs 1 to 3 lines")
            self.check_lines(bw, blines, speaker, False)
            if bid not in used_branches:
                self.warn(bw, "branch is not referenced by any choice")

        # key convention (warning)
        for index, line in enumerate(lines or [], start=1):
            expected = f"dlg.{cid}.{index}"
            if line.get("text_key") != expected:
                self.warn(f"{where} line {index}", f"text_key differs from convention {expected}")

    def check_cooldowns(self) -> None:
        """Warn when cooldown_visits >= number of repeatable conversations of the same pool (can mute them)."""
        groups: dict[str, list[dict]] = {}
        for conv in self.conversations.values():
            if not conv.get("repeatable", False):
                continue
            category = conv.get("category")
            if category == "bark":
                key = "bark"
            elif category in NPC_CATEGORIES:
                key = f"npc:{conv.get('speaker')}"
            else:
                key = category
            groups.setdefault(key, []).append(conv)
        for key, members in groups.items():
            for conv in members:
                default = 6 if conv.get("category") == "bark" else 4
                cooldown = conv.get("cooldown_visits", default)
                if cooldown >= len(members):
                    self.warn(f"conversation {conv.get('id')}",
                              f"cooldown_visits {cooldown} >= {len(members)} repeatable conversation(s) in pool {key}")

    def check_references(self) -> None:
        for cid, conv in self.conversations.items():
            for req in conv.get("requirements", []):
                self._check_seen_refs(f"conversation {cid}", req)

    def _check_seen_refs(self, where: str, cond) -> None:
        if not isinstance(cond, dict):
            return
        if cond.get("type") in ("seen", "not_seen") and cond.get("id") not in self.conversation_ids:
            self.err(where, f"{cond['type']} references unknown conversation {cond.get('id')!r}")
        if cond.get("type") == "not":
            self._check_seen_refs(where, cond.get("condition"))
        for child in cond.get("conditions", []) if cond.get("type") == "any" else []:
            self._check_seen_refs(where, child)

    def check_unused_and_parity(self) -> None:
        reference = set(self.texts["pt-BR"])
        for language in LANGUAGES[1:]:
            for key in sorted(reference - set(self.texts[language])):
                self.err("dialogue.json", f"{key} missing in {language}")
            for key in sorted(set(self.texts[language]) - reference):
                self.err("dialogue.json", f"{key} exists in {language} but not in pt-BR")
        # dlg.npc.* are read by C# (DialogueNpc), not by data/dialogue.
        for key in sorted(k for k in reference - self.used_keys if not k.startswith("dlg.npc.")):
            self.warn("dialogue.json", f"key {key} is not used by data/dialogue/*.json")


def main() -> int:
    root = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else project_root()
    folder = root / "data" / "dialogue"
    validator = Validator(root)
    if not folder.is_dir():
        print(f"ERROR: missing folder {folder}")
        return 1
    files = sorted(folder.glob("*.json"))
    documents = {path.name: load_jsonc(path, validator.errors) for path in files}
    validator.check_speakers(documents.get("speakers.json"))
    for name, data in documents.items():
        if name == "speakers.json" or data is None:
            continue
        for conv in data.get("conversations", []):
            validator.check_conversation(conv)
    validator.check_references()
    validator.check_cooldowns()
    for language in LANGUAGES:
        if not validator.texts[language].get("dlg.npc.prompt", "").strip():
            validator.err("dialogue.json", f"dlg.npc.prompt missing in {language}")
    validator.check_unused_and_parity()
    for message in validator.errors:
        print(f"ERROR: {message}")
    for message in validator.warnings:
        print(f"WARNING: {message}")
    print(f"Dialogue: {len(validator.conversation_ids)} conversations, {len(validator.speakers)} speakers, "
          f"{len(validator.errors)} errors, {len(validator.warnings)} warnings")
    return 1 if validator.errors else 0


if __name__ == "__main__":
    sys.exit(main())
