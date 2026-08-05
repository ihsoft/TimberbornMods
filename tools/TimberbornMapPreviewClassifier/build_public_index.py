#!/usr/bin/env python3
"""Build compact public Workshop search artifacts."""

from __future__ import annotations

import argparse
from datetime import datetime, timezone
import gzip
import json
from pathlib import Path
import shutil

PUBLIC_SCHEMA_VERSION = 2


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--snapshot", required=True)
    parser.add_argument("--map-metadata", required=True)
    parser.add_argument("--output-directory", required=True)
    return parser.parse_args()


def read_json_lines(path: Path) -> list[dict]:
    if not path.is_file():
        return []
    if path.suffix == ".gz":
        stream = gzip.open(path, "rt", encoding="utf-8")
    else:
        stream = path.open("r", encoding="utf-8")
    with stream:
        return [json.loads(line) for line in stream if line.strip()]


def write_gzip_json_lines(path: Path, records: list[dict]) -> None:
    with gzip.open(path, "wt", encoding="utf-8", newline="\n") as stream:
        for record in records:
            stream.write(json.dumps(record, ensure_ascii=False, separators=(",", ":")) + "\n")


def main() -> int:
    args = parse_args()
    snapshot_path = Path(args.snapshot)
    map_metadata_path = Path(args.map_metadata)
    output_directory = Path(args.output_directory)
    output_directory.mkdir(parents=True, exist_ok=True)

    workshop_items = read_json_lines(snapshot_path)
    map_metadata_results = read_json_lines(map_metadata_path)
    map_metadata_by_id = {
        record["published_file_id"]: record for record in map_metadata_results
    }
    search_index = []
    for item in workshop_items:
        record = dict(item)
        map_metadata = map_metadata_by_id.get(item["published_file_id"])
        if map_metadata:
            record["map_width"] = map_metadata["map_width"]
            record["map_height"] = map_metadata["map_height"]
            record["map_metadata_collection_state"] = map_metadata.get("collection_state")
            record["map_analysis_version"] = map_metadata.get("analysis_version")
            if "classifications" in map_metadata:
                record["map_classifications"] = map_metadata["classifications"]
        search_index.append(record)

    write_gzip_json_lines(output_directory / "workshop-items.jsonl.gz", workshop_items)
    write_gzip_json_lines(output_directory / "map-metadata.jsonl.gz", map_metadata_results)
    write_gzip_json_lines(output_directory / "search-index.jsonl.gz", search_index)
    manifest = {
        "schema_version": PUBLIC_SCHEMA_VERSION,
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "source": "public-steam-workshop-http-and-anonymous-map-payloads",
        "workshop_items": len(workshop_items),
        "map_dimensions_known": len(map_metadata_results),
        "map_dimensions_stale": sum(
            record.get("collection_state") == "stale" for record in map_metadata_results
        ),
        "map_forest_density_known": sum(
            "forest_density" in record.get("classifications", {})
            for record in map_metadata_results
        ),
        "map_water_known": sum(
            "water" in record.get("classifications", {})
            for record in map_metadata_results
        ),
        "files": {
            path.name: path.stat().st_size
            for path in sorted(output_directory.glob("*.jsonl.gz"))
        },
    }
    (output_directory / "manifest.json").write_text(
        json.dumps(manifest, indent=2) + "\n", encoding="utf-8"
    )
    shutil.copyfile(
        Path(__file__).with_name("public-index.html"), output_directory / "index.html"
    )
    print(json.dumps(manifest, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
