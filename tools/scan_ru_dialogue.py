# -*- coding: utf-8 -*-
"""
Scan DialogueDatabase.asset for Russian text snippets and print conv/id + EN.

Usage:
  python tools/scan_ru_dialogue.py "Добрый день" "Сразу говорю"
"""

from __future__ import annotations

import re
import sys
import importlib.util
from pathlib import Path


def _load_patch_module():
    spec = importlib.util.spec_from_file_location("p", "tools/patch_dialogue_en.py")
    mod = importlib.util.module_from_spec(spec)
    assert spec and spec.loader
    spec.loader.exec_module(mod)
    return mod


def main(argv: list[str]) -> int:
    needles = [a.strip() for a in argv[1:] if a.strip()]
    if not needles:
        print("Provide at least one snippet to search for.")
        return 2

    p = _load_patch_module()
    t = Path("Assets/SystemDialog/DialogueDatabase.asset").read_text(encoding="utf-8")
    blocks = re.findall(
        r"    - id: (\d+)\n      fields:\n(.*?)\n      conversationID: (\d+)",
        t,
        re.DOTALL,
    )

    hits: list[tuple[int, int, str, str]] = []
    for eid_s, fields, conv_s in blocks:
        ru = p.get_russian_text(fields)
        if not ru:
            continue
        ru_flat = re.sub(r"\s+", " ", ru).strip()
        if not any(n in ru_flat for n in needles):
            continue
        m = re.search(r'- title: en\n\s+value:\s*"([\s\S]*?)"\s*\n\s+type: 4', fields)
        en = p.decode_yaml_string('"' + m.group(1) + '"').strip() if m else ""
        hits.append((int(conv_s), int(eid_s), ru_flat, en))

    hits.sort()
    for conv, eid, ru_flat, en in hits:
        print(f"conv={conv} id={eid}")
        print(f"  RU: {ru_flat}")
        print(f"  EN: {en!r}")
        print()

    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))

