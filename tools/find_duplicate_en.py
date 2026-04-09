# -*- coding: utf-8 -*-
"""Find duplicate en field text in DialogueDatabase.asset (decoded)."""
import re
import pathlib
import importlib.util

spec = importlib.util.spec_from_file_location("p", "tools/patch_dialogue_en.py")
p = importlib.util.module_from_spec(spec)
spec.loader.exec_module(p)

t = pathlib.Path("Assets/SystemDialog/DialogueDatabase.asset").read_text(encoding="utf-8")
blocks = re.findall(
    r"    - id: (\d+)\n      fields:\n(.*?)\n      conversationID: (\d+)", t, re.DOTALL
)
by_text: dict[str, list[tuple[str, str]]] = {}
for eid, fields, conv in blocks:
    m = re.search(
        r'- title: en\n\s+value:\s*"([\s\S]*?)"\s*\n\s+type: 4', fields
    )
    if not m:
        continue
    s = p.decode_yaml_string('"' + m.group(1) + '"').strip()
    if not s:
        continue
    by_text.setdefault(s, []).append((conv, eid))

for txt, locs in sorted(by_text.items(), key=lambda x: -len(x[1])):
    if len(locs) <= 1:
        continue
    print("DUPLICATE x", len(locs), ":", repr(txt[:120]) + ("..." if len(txt) > 120 else ""))
    print("  ", locs[:20])
    print()
