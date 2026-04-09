# -*- coding: utf-8 -*-
import re, pathlib, importlib.util, sys

spec = importlib.util.spec_from_file_location("p", "tools/patch_dialogue_en.py")
p = importlib.util.module_from_spec(spec)
spec.loader.exec_module(p)

conv = int(sys.argv[1])
t = pathlib.Path("Assets/SystemDialog/DialogueDatabase.asset").read_text(encoding="utf-8")
pat = rf"(  - id: {conv}\n    fields:.*?\n    dialogueEntries:)(.*?)(\n  - id: {conv + 1}\n|\n  locations:|\Z)"
m = re.search(pat, t, re.DOTALL)
if not m:
    print("conv not found", conv)
    sys.exit(1)
body = m.group(2)
entries = re.findall(
    r"    - id: (\d+)\n      fields:\n(.*?)(?=\n    - id:|\n    entryGroups:)",
    body,
    re.DOTALL,
)
for eid, fld in entries:
    ru = p.get_russian_text(fld)
    en_m = re.search(r"- title: en\n\s+value:\s*\"([\s\S]*?)\"\s*\n\s+type: 4", fld)
    en = p.decode_yaml_string('"' + en_m.group(1) + '"') if en_m else ""
    if not ru.strip() and not en.strip():
        continue
    ru1 = re.sub(r"\s+", " ", ru)[:100]
    print(f"id={eid}")
    print(f"  RU: {ru1!r}")
    prec = en[:200] + ("…" if len(en) > 200 else "")
    print(f"  EN: {prec!r} (len={len(en)})")
