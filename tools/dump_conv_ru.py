# -*- coding: utf-8 -*-
import re
import pathlib
import importlib.util
import sys

spec = importlib.util.spec_from_file_location("p", "tools/patch_dialogue_en.py")
p = importlib.util.module_from_spec(spec)
spec.loader.exec_module(p)

conv = int(sys.argv[1])
t = pathlib.Path("Assets/SystemDialog/DialogueDatabase.asset").read_text(encoding="utf-8")
# find conversation block: "  - id: N\n" at 2 spaces - conversations are under conversations:
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
    ru1 = re.sub(r"\s+", " ", ru)[:140]
    print(f"id={eid} | {ru1!r}")
