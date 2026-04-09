# -*- coding: utf-8 -*-
"""All entries where EN is long vs RU (possible merge)."""
import re
import pathlib
import importlib.util

spec = importlib.util.spec_from_file_location("p", "tools/patch_dialogue_en.py")
p = importlib.util.module_from_spec(spec)
spec.loader.exec_module(p)

t = pathlib.Path("Assets/SystemDialog/DialogueDatabase.asset").read_text(encoding="utf-8")
blocks = re.findall(
    r"    - id: (\d+)\n      fields:\n(.*?)\n      conversationID: (\d+)",
    t,
    re.DOTALL,
)
rows = []
for eid, fields, conv_s in blocks:
    conv = int(conv_s)
    eid = int(eid)
    m = re.search(r'- title: en\n\s+value:\s*"([\s\S]*?)"\s*\n\s+type: 4', fields)
    if not m:
        continue
    en = p.decode_yaml_string('"' + m.group(1) + '"').strip()
    if not en:
        continue
    ru = p.get_russian_text(fields)
    ru_flat = re.sub(r"\s+", " ", ru).strip()
    if not ru_flat:
        continue
    ratio = len(en) / max(len(ru_flat), 1)
    if len(en) > 80 and ratio > 1.8:
        rows.append((ratio, conv, eid, len(ru_flat), len(en), ru_flat[:90]))

rows.sort(reverse=True, key=lambda x: x[0])
for ratio, conv, eid, rlen, elen, rprev in rows[:200]:
    print(f"conv={conv} id={eid} ratio={ratio:.1f} ru_len={rlen} en_len={elen} RU={rprev!r}")
