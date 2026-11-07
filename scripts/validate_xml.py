from pathlib import Path
import sys
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[1]
EXCLUDED_DIRS = {".git", ".vs", "bin", "obj"}


def find_xml_files():
    for path in ROOT.rglob("*.xml"):
        if not any(part in EXCLUDED_DIRS for part in path.relative_to(ROOT).parts):
            yield path


def main():
    xml_files = sorted(find_xml_files())
    if not xml_files:
        print("No XML files found.")
        return 0

    failures = []
    for path in xml_files:
        try:
            ET.parse(path)
        except ET.ParseError as error:
            failures.append((path, error))

    if failures:
        for path, error in failures:
            print(f"Invalid XML: {path.relative_to(ROOT)}: {error}", file=sys.stderr)
        print(f"XML validation failed: {len(failures)} of {len(xml_files)} files are invalid.", file=sys.stderr)
        return 1

    print(f"XML validation passed: {len(xml_files)} files checked.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
