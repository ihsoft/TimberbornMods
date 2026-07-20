#!/usr/bin/env python3
"""Build compact public Workshop search artifacts."""

from __future__ import annotations

import argparse
from datetime import datetime, timezone
import gzip
import json
from pathlib import Path
import shutil


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--snapshot", required=True)
    parser.add_argument("--visual-features", required=True)
    parser.add_argument("--gallery-results", required=True)
    parser.add_argument("--output-directory", required=True)
    return parser.parse_args()


def read_json_lines(path: Path) -> list[dict]:
    with path.open("r", encoding="utf-8") as stream:
        return [json.loads(line) for line in stream if line.strip()]


def write_gzip_json_lines(path: Path, records: list[dict]) -> None:
    with gzip.open(path, "wt", encoding="utf-8", newline="\n") as stream:
        for record in records:
            stream.write(json.dumps(record, ensure_ascii=False, separators=(",", ":")) + "\n")


def main() -> int:
    args = parse_args()
    snapshot_path = Path(args.snapshot)
    visual_path = Path(args.visual_features)
    gallery_path = Path(args.gallery_results)
    output_directory = Path(args.output_directory)
    output_directory.mkdir(parents=True, exist_ok=True)

    workshop_items = read_json_lines(snapshot_path)
    visual_features = read_json_lines(visual_path)
    gallery_results = read_json_lines(gallery_path)
    visual_by_id = {record["published_file_id"]: record for record in visual_features}
    gallery_by_id = {record["published_file_id"]: record for record in gallery_results}
    search_index = []
    for item in workshop_items:
        record = dict(item)
        visual = visual_by_id.get(item["published_file_id"])
        gallery = gallery_by_id.get(item["published_file_id"])
        if gallery:
            record["gallery_urls"] = gallery.get("gallery_urls", [])
            record["gallery_checked_at_utc"] = gallery.get("gallery_checked_at_utc")
            record["gallery_collection_state"] = gallery.get("collection_state")
        if visual:
            record["visual_scores"] = visual["visual_scores"]
            record["visual_score_aggregates"] = visual.get("visual_score_aggregates")
            record["visual_percentiles"] = visual["visual_percentiles"]
            record["visual_percentile_aggregates"] = visual.get(
                "visual_percentile_aggregates"
            )
            record["visual_labels"] = visual["visual_labels"]
            record["visual_image_count"] = visual.get("visual_image_count", 1)
            record["visual_gallery_image_count"] = visual.get("visual_gallery_image_count", 0)
            record["visual_missing_image_count"] = visual.get("visual_missing_image_count", 0)
            record["visual_model"] = visual["model"]
            record["visual_classifier_version"] = visual.get("classifier_version")
            record["visual_stale"] = visual.get("visual_stale", False)
            record["visual_classified_preview_url"] = visual.get(
                "classified_preview_url", visual.get("preview_url")
            )
        search_index.append(record)

    write_gzip_json_lines(output_directory / "workshop-items.jsonl.gz", workshop_items)
    write_gzip_json_lines(output_directory / "map-gallery.jsonl.gz", gallery_results)
    write_gzip_json_lines(output_directory / "map-visual-features.jsonl.gz", visual_features)
    write_gzip_json_lines(output_directory / "search-index.jsonl.gz", search_index)

    missing_visual_maps = sum(
        item.get("primary_category") == "map"
        and item["published_file_id"] not in visual_by_id
        for item in workshop_items
    )
    manifest = {
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "source": "public-steam-workshop-http",
        "workshop_items": len(workshop_items),
        "classified_maps": len(visual_features),
        "maps_missing_visual_features": missing_visual_maps,
        "maps_with_stale_visual_features": sum(
            record.get("visual_stale", False) for record in visual_features
        ),
        "maps_classified_this_run": sum(
            record.get("classification_state") == "classified"
            for record in visual_features
        ),
        "maps_reused_this_run": sum(
            record.get("classification_state") == "reused"
            for record in visual_features
        ),
        "gallery_maps_known": len(gallery_results),
        "gallery_maps_fetched_this_run": sum(
            record.get("collection_state") == "fetched" for record in gallery_results
        ),
        "gallery_maps_stale": sum(
            record.get("collection_state") in {"stale", "deferred"}
            for record in gallery_results
        ),
        "gallery_images_known": sum(
            len(record.get("gallery_urls", [])) for record in gallery_results
        ),
        "visual_images_classified": sum(
            record.get("visual_image_count", 1) for record in visual_features
        ),
        "visual_images_classified_this_run": sum(
            record.get("images_classified_this_run", 0) for record in visual_features
        ),
        "visual_model": visual_features[0]["model"] if visual_features else None,
        "visual_classifier_version": (
            visual_features[0].get("classifier_version") if visual_features else None
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
