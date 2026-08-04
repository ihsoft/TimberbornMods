#!/usr/bin/env python3
"""Create a reviewable water-availability calibration set from the public map index."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

import create_forest_calibration_set as common


WATER_FEATURE = "water_dominance"
DEFAULT_OUTPUT = ".tools/map-vision/water-calibration"
LEVELS = (
    ("very_low", "Very little water"),
    ("low", "Low water availability"),
    ("moderate", "Moderate water availability"),
    ("high", "High water availability"),
    ("very_high", "Very high water availability"),
)
RUBRIC = (
    "<strong>Free water:</strong> blue water areas are the primary and strongest contribution to water availability.",
    "<strong>Moist soil:</strong> green irrigated or moist ground contributes less than blue free water, but still indicates water availability. Dry green decoration or vegetation alone should not be counted as moist soil.",
    "<strong>Relative density:</strong> judge both contributions relative to the map's total area, not by absolute tile count. The same approximate number of wet tiles must receive a lower rating on a larger map and a higher rating on a smaller map.",
    "<strong>Scale:</strong> level 5 is the highest practical water availability for a Timberborn map; it does not require the entire map to be blue or green.",
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Select map previews across the water-score range for quick human labeling."
    )
    parser.add_argument("--index", default=common.DEFAULT_INDEX)
    parser.add_argument("--output-directory", default=DEFAULT_OUTPUT)
    parser.add_argument("--count", type=int, default=50)
    parser.add_argument(
        "--include-id", action="append", default=["3672607632", "3652824726"]
    )
    parser.add_argument("--download-concurrency", type=int, default=6)
    parser.add_argument("--max-image-bytes", type=int, default=2_000_000)
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    output_directory = Path(args.output_directory)
    output_directory.mkdir(parents=True, exist_ok=True)
    candidates = common.read_candidates(args.index, WATER_FEATURE)
    selected = common.select_candidates(candidates, args.count, args.include_id, WATER_FEATURE)
    entries = common.prepare_entries(
        selected, output_directory, args.download_concurrency, args.max_image_bytes, WATER_FEATURE, "water"
    )
    (output_directory / "calibration-set.json").write_text(
        json.dumps({"schema_version": 1, "feature": WATER_FEATURE, "maps": entries}, indent=2, ensure_ascii=False),
        encoding="utf-8",
    )
    (output_directory / "index.html").write_text(
        common.render_html(
            entries,
            feature=WATER_FEATURE,
            levels=LEVELS,
            rubric=RUBRIC,
            page_title="Water availability calibration",
            storage_key="timberborn-water-calibration-v1",
            export_filename="water-calibration-labels.json",
            field_prefix="water",
        ),
        encoding="utf-8",
    )
    print(f"Wrote {len(entries)} maps to {output_directory / 'index.html'}")


if __name__ == "__main__":
    main()
