# -*- coding: utf-8 -*-
"""List dialogue entries where the same English string is reused in one conversation (likely bad merge)."""
import re
import pathlib
import importlib.util
from collections import defaultdict

spec = importlib.util.spec_from_file_location("p", "tools/patch_dialogue_en.py")
p = importlib.util.module_from_spec(spec)
spec.loader.exec_module(p)

t = pathlib.Path("Assets/SystemDialog/DialogueDatabase.asset").read_text(encoding="utf-8")
blocks = re.findall(
    r"    - id: (\d+)\n      fields:\n(.*?)\n      conversationID: (\d+)",
    t,
    re.DOTALL,
)
by_conv_en: dict[tuple[int, str], list[str]] = defaultdict(list)
long_clump: list[tuple[int, str, str, int, int]] = []

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
    by_conv_en[(conv, en)].append(str(eid))
    if ru_flat and len(en) > 120 and len(en) > len(ru_flat) * 2:
        long_clump.append((conv, str(eid), ru_flat[:70], len(ru_flat), len(en)))

print("=== Same EN used for multiple ids in one conversation (count >= 2) ===")
for (conv, en), ids in sorted(by_conv_en.items(), key=lambda x: (-len(x[1]), x[0][0])):
    if len(ids) < 2:
        continue
    uniq = sorted(set(ids), key=int)
    if len(en) > 200:
        prev = en[:200] + "..."
    else:
        prev = en
    print(f"conv={conv} x{len(uniq)} ids={uniq[:25]}{'...' if len(uniq)>25 else ''}")
    print(f"  EN: {prev!r}")
    print()

print("\n=== Possible merged EN (long EN vs short RU), sample 40 ===")
for row in sorted(long_clump, key=lambda r: -r[4])[:40]:
    print(f"conv={row[0]} id={row[1]} ru_len={row[3]} en_len={row[4]} RU={row[2]!r}")
