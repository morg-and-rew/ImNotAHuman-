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
n = 0
for eid, fields, conv in blocks:
    m = re.search(
        r'- title: en\n\s+value:\s*"([\s\S]*?)"\s*\n\s+type: 4', fields
    )
    if not m:
        continue
    s = p.decode_yaml_string('"' + m.group(1) + '"')
    if re.search(r"[\u4e00-\u9fff]", s):
        n += 1
print("blocks with CJK chars in en:", n)
