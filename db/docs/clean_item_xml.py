#!/usr/bin/env python3
import os
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


def load_valid_ids(path: Path):
    ids = set()
    with path.open("r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            try:
                ids.add(int(line))
            except ValueError:
                continue
    return ids


def clean_xml_file(path: Path, valid_ids: set[int], report: dict[str, list[int]]):
    parser = ET.XMLParser(target=ET.TreeBuilder(insert_comments=True))
    tree = ET.parse(path, parser=parser)
    root = tree.getroot()

    removed = []
    # Only remove direct Item children to avoid breaking nested structures
    for child in list(root):
        if child.tag != "Item":
            continue
        item_id = child.get("ItemId")
        if item_id is None:
            item_id = child.get("Id")
        if item_id is None:
            continue
        try:
            iid = int(item_id)
        except ValueError:
            continue
        if iid not in valid_ids:
            root.remove(child)
            removed.append(iid)

    if removed:
        ET.indent(tree, space="\t")
        tree.write(path, encoding="utf-8", xml_declaration=True)
    report[str(path)] = removed


def main():
    if len(sys.argv) < 3:
        print("Usage: clean_item_xml.py <valid_item_ids.txt> <xml_dir> [<xml_dir> ...]")
        return 1

    valid_ids_path = Path(sys.argv[1])
    valid_ids = load_valid_ids(valid_ids_path)
    if not valid_ids:
        print("No valid item ids loaded; aborting.")
        return 1

    report = {}
    for xml_dir in sys.argv[2:]:
        base = Path(xml_dir)
        if not base.exists():
            continue
        for path in sorted(base.glob("*.xml")):
            clean_xml_file(path, valid_ids, report)

    report_path = Path("/Users/airtan/Documents/GitHub/jp/db/docs/clean_item_xml_report.md")
    with report_path.open("w", encoding="utf-8") as f:
        f.write("# Clean Item XML Report\n\n")
        for path, removed in report.items():
            if not removed:
                continue
            removed_sorted = ", ".join(str(i) for i in sorted(set(removed)))
            f.write(f"- `{path}`\n")
            f.write(f"  - removed item ids: {removed_sorted}\n")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
