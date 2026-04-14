# -*- coding: utf-8 -*-
"""
One-off migration: copy Russian from Menu Text -> Dialogue Text when Dialogue Text is empty.
Targets Pixel Crushers Dialogue System YAML in DialogueDatabase.asset.
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

import yaml

ROOT = Path(__file__).resolve().parents[1]
ASSET = ROOT / "Assets" / "SystemDialog" / "DialogueDatabase.asset"


def get_value_block(lines: list[str], value_line_idx: int) -> tuple[list[str] | None, int]:
    """First line at value_line_idx must be '        value:'. Collect until '        type:'."""
    if value_line_idx >= len(lines):
        return None, value_line_idx
    if not lines[value_line_idx].startswith("        value:"):
        return None, value_line_idx
    block = [lines[value_line_idx]]
    j = value_line_idx + 1
    while j < len(lines) and not lines[j].startswith("        type:"):
        block.append(lines[j])
        j += 1
    return block, j


def skip_field_tail(lines: list[str], idx_at_type_line: int) -> int:
    """Skip '        type:' and '        typeString:' lines after a field value."""
    k = idx_at_type_line
    while k < len(lines):
        line = lines[k]
        if line.startswith("        type:") or line.startswith("        typeString:"):
            k += 1
            continue
        break
    return k


def block_to_string(value_lines: list[str]) -> str:
    if not value_lines:
        return ""
    text = "".join(value_lines)
    m = re.match(r"^[ \t]*value:\s*(.*)$", text, re.DOTALL)
    if not m:
        return ""
    payload = m.group(1)
    if not payload.strip():
        return ""
    doc = yaml.safe_load("k: " + payload)
    if doc is None or not isinstance(doc, dict):
        return ""
    v = doc.get("k")
    if v is None:
        return ""
    return v if isinstance(v, str) else str(v)


def is_effectively_empty(s: str) -> bool:
    return s is None or len(s.strip()) == 0


def migrate_file(path: Path, dry_run: bool) -> tuple[int, int, list[str]]:
    lines = path.read_text(encoding="utf-8").splitlines(keepends=True)

    i = 0
    fills = 0
    pairs = 0
    report: list[str] = []

    while i < len(lines):
        if lines[i].rstrip("\n") != "      - title: Menu Text":
            i += 1
            continue

        menu_val_block, menu_type_idx = get_value_block(lines, i + 1)
        if menu_val_block is None:
            i += 1
            continue

        after_menu = skip_field_tail(lines, menu_type_idx)
        if after_menu >= len(lines):
            break
        if lines[after_menu].rstrip("\n") != "      - title: Dialogue Text":
            i += 1
            continue

        pairs += 1
        dlg_val_block, dlg_type_idx = get_value_block(lines, after_menu + 1)
        if dlg_val_block is None:
            i = after_menu + 1
            continue

        ms = block_to_string(menu_val_block)
        ds = block_to_string(dlg_val_block)

        next_i = skip_field_tail(lines, dlg_type_idx)

        if is_effectively_empty(ds) and not is_effectively_empty(ms):
            fills += 1
            report.append(f"  line {i + 1}: copied Menu ({len(ms)} chars) -> Dialogue Text")
            if not dry_run:
                insert_at = after_menu + 1
                old_len = len(dlg_val_block)
                new_block = list(menu_val_block)
                lines[insert_at : insert_at + old_len] = new_block
                delta = len(new_block) - old_len
                next_i += delta
            i = next_i
            continue

        i = next_i

    if not dry_run and fills:
        path.write_text("".join(lines), encoding="utf-8")

    return pairs, fills, report


def main() -> None:
    dry = "--dry-run" in sys.argv
    if not ASSET.is_file():
        print("Missing:", ASSET)
        sys.exit(1)
    pairs, fills, rep = migrate_file(ASSET, dry_run=dry)
    print(f"Menu/Dialogue pairs found: {pairs}")
    print(f"Entries filled (empty Dialogue Text, non-empty Menu Text): {fills}")
    if rep:
        print("Log (first 40):")
        for line in rep[:40]:
            print(line)
        if len(rep) > 40:
            print(f"  ... and {len(rep) - 40} more")
    if dry:
        print("(dry-run: file not modified)")
    else:
        print("Wrote:", ASSET)


if __name__ == "__main__":
    main()
