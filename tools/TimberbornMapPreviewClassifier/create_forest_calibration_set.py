#!/usr/bin/env python3
"""Create a reviewable forest-density calibration set from the public map index."""

from __future__ import annotations

import argparse
import gzip
import hashlib
import html
import json
import time
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path
from urllib.parse import urlparse
from urllib.request import Request, urlopen

from workshop_records import is_map_item


DEFAULT_INDEX = "https://ihsoft.github.io/TimberbornMods/search-index.jsonl.gz"
DEFAULT_OUTPUT = ".tools/map-vision/forest-calibration"
FOREST_FEATURE = "forest_density"
LEVELS = (
    ("none", "No living green trees"),
    ("sparse", "Low green-tree density"),
    ("moderate", "Moderate green-tree density"),
    ("forested", "High green-tree density"),
    ("dense", "Highest practical green forest density"),
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Select map previews across the forest-score range for quick human labeling."
    )
    parser.add_argument("--index", default=DEFAULT_INDEX, help="JSONL(.gz) path or URL")
    parser.add_argument("--output-directory", default=DEFAULT_OUTPUT)
    parser.add_argument("--count", type=int, default=50)
    parser.add_argument("--include-id", action="append", default=["3619540066"])
    parser.add_argument("--download-concurrency", type=int, default=6)
    parser.add_argument("--max-image-bytes", type=int, default=2_000_000)
    return parser.parse_args()


def open_index(source: str):
    parsed = urlparse(source)
    if parsed.scheme in {"http", "https"}:
        response = urlopen(Request(source, headers={"User-Agent": "TimberbornMapCalibration/1"}), timeout=60)
        stream = response
    else:
        stream = Path(source).open("rb")
    if source.lower().endswith(".gz"):
        return gzip.open(stream, "rt", encoding="utf-8")
    return stream if "b" not in getattr(stream, "mode", "") else __import__("io").TextIOWrapper(stream, encoding="utf-8")


def read_candidates(source: str, feature: str = FOREST_FEATURE) -> list[dict]:
    candidates = []
    with open_index(source) as lines:
        for line in lines:
            record = json.loads(line)
            scores = record.get("visual_scores", {})
            percentiles = record.get("visual_percentiles", {})
            if (
                is_map_item(record)
                and record.get("preview_url")
                and isinstance(scores.get(feature), (int, float))
                and isinstance(percentiles.get(feature), (int, float))
            ):
                candidates.append(record)
    return candidates


def stable_rank(record: dict) -> str:
    return hashlib.sha256(record["published_file_id"].encode("ascii")).hexdigest()


def select_candidates(
    candidates: list[dict], count: int, include_ids: list[str], feature: str = FOREST_FEATURE
) -> list[dict]:
    if count < 1:
        raise ValueError("--count must be positive")
    if count > len(candidates):
        raise ValueError(f"Requested {count} maps, but only {len(candidates)} candidates are available")

    by_id = {record["published_file_id"]: record for record in candidates}
    missing_ids = [published_file_id for published_file_id in include_ids if published_file_id not in by_id]
    if missing_ids:
        raise ValueError(f"Required map IDs are absent from the index: {', '.join(missing_ids)}")

    selected = {published_file_id: by_id[published_file_id] for published_file_id in include_ids}
    remaining = [record for record in candidates if record["published_file_id"] not in selected]

    # Fill equal-width percentile strata deterministically. This covers the full
    # score range without asking the reviewer to search for examples manually.
    bin_count = min(10, count)
    bins = [[] for _ in range(bin_count)]
    for record in remaining:
        percentile = min(max(float(record["visual_percentiles"][feature]), 0.0), 1.0)
        bin_index = min(int(percentile * bin_count), bin_count - 1)
        bins[bin_index].append(record)
    for records in bins:
        records.sort(key=stable_rank)

    while len(selected) < count:
        added = False
        for records in bins:
            if records and len(selected) < count:
                record = records.pop()
                selected[record["published_file_id"]] = record
                added = True
        if not added:
            raise RuntimeError("Could not fill the requested calibration set")

    return sorted(selected.values(), key=lambda item: item["visual_scores"][feature])


def image_suffix(url: str) -> str:
    suffix = Path(urlparse(url).path).suffix.lower()
    return suffix if suffix in {".jpg", ".jpeg", ".png", ".webp"} else ".jpg"


def download_image(record: dict, image_directory: Path, max_image_bytes: int) -> str:
    filename = record["published_file_id"] + image_suffix(record["preview_url"])
    destination = image_directory / filename
    if destination.is_file() and destination.stat().st_size > 0:
        return filename

    last_error = None
    for attempt in range(3):
        try:
            request = Request(record["preview_url"], headers={"User-Agent": "TimberbornMapCalibration/1"})
            with urlopen(request, timeout=30) as response:
                length = response.headers.get("Content-Length")
                if length and int(length) > max_image_bytes:
                    raise ValueError(f"image exceeds {max_image_bytes} bytes")
                data = response.read(max_image_bytes + 1)
            if len(data) > max_image_bytes:
                raise ValueError(f"image exceeds {max_image_bytes} bytes")
            temporary = destination.with_suffix(destination.suffix + ".tmp")
            temporary.write_bytes(data)
            temporary.replace(destination)
            return filename
        except Exception as exception:  # Network errors differ across platforms.
            last_error = exception
            if attempt < 2:
                time.sleep(attempt + 1)
    raise RuntimeError(f"Could not download {record['published_file_id']}: {last_error}")


def prepare_entries(
    records: list[dict], output_directory: Path, concurrency: int, max_image_bytes: int,
    feature: str = FOREST_FEATURE, field_prefix: str = "forest",
) -> list[dict]:
    image_directory = output_directory / "images"
    image_directory.mkdir(parents=True, exist_ok=True)
    entries_by_id = {}
    with ThreadPoolExecutor(max_workers=concurrency) as executor:
        pending = {
            executor.submit(download_image, record, image_directory, max_image_bytes): record
            for record in records
        }
        for future in as_completed(pending):
            record = pending[future]
            filename = future.result()
            entries_by_id[record["published_file_id"]] = {
                "published_file_id": record["published_file_id"],
                "title": record["title"],
                "preview_url": record["preview_url"],
                "image_path": "images/" + filename,
                field_prefix + "_score": record["visual_scores"][feature],
                field_prefix + "_percentile": record["visual_percentiles"][feature],
                "visual_image_count": record.get("visual_image_count", 1),
            }
    return [entries_by_id[record["published_file_id"]] for record in records]


def render_html(
    entries: list[dict], feature: str = FOREST_FEATURE, levels=LEVELS,
    rubric: tuple[str, ...] = (
        "<strong>What counts as forest:</strong> only living green trees. Dead or dry trees do not count.",
        "<strong>Density, not tree count:</strong> judge living green trees relative to the map's total area. The same approximate number of trees must receive a lower rating on a larger map and a higher rating on a smaller map.",
        "<strong>Scale:</strong> judge realistic Timberborn map density, not percentage of total map area. Level 5 is the highest practical forest density seen on a map preview; it does not mean that trees cover the whole map.",
    ),
    page_title: str = "Forest density calibration",
    storage_key: str = "timberborn-forest-calibration-v1",
    export_filename: str = "forest-calibration-labels.json",
    field_prefix: str = "forest",
) -> str:
    entries_json = json.dumps(entries, ensure_ascii=False).replace("</", "<\\/")
    levels_json = json.dumps(levels)
    rubric_html = "\n".join(f"  <p>{line}</p>" for line in rubric)
    score_field_json = json.dumps(field_prefix + "_score")
    percentile_field_json = json.dumps(field_prefix + "_percentile")
    return f"""<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{html.escape(page_title)}</title>
<style>
body {{ margin: 0; font: 16px system-ui, sans-serif; color: #e8eee8; background: #172019; }}
main {{ max-width: 1120px; margin: auto; padding: 18px; }}
.top {{ display: flex; gap: 14px; align-items: center; flex-wrap: wrap; }}
.progress {{ flex: 1; min-width: 220px; height: 10px; background: #344136; border-radius: 6px; overflow: hidden; }}
.progress div {{ height: 100%; background: #8dbb72; }}
.card {{ margin-top: 16px; padding: 18px; background: #222d24; border-radius: 12px; }}
.rubric {{ margin: 12px 0 16px; padding: 12px 14px; color: #dce8d9; background: #303d32; border-left: 4px solid #8dbb72; }}
.rubric p {{ margin: 5px 0; }}
img {{ display: block; width: 100%; max-height: 65vh; object-fit: contain; background: #0d110e; }}
.meta {{ color: #aeb9af; margin: 8px 0 14px; }}
.choices {{ display: grid; grid-template-columns: repeat(5, 1fr); gap: 8px; }}
button {{ padding: 12px 8px; color: inherit; background: #3a493d; border: 1px solid #657568; border-radius: 7px; cursor: pointer; }}
button:hover, button.selected {{ background: #607c58; border-color: #a9d28f; }}
.nav {{ display: flex; gap: 8px; margin-top: 12px; }}
.nav button:last-child {{ margin-left: auto; }}
@media (max-width: 760px) {{ .choices {{ grid-template-columns: 1fr; }} }}
</style>
</head>
<body><main>
<div class="top"><strong id="counter"></strong><div class="progress"><div id="bar"></div></div><span id="done"></span></div>
<aside class="rubric">
{rubric_html}
</aside>
<section class="card">
  <h1 id="title"></h1>
  <div class="meta" id="meta"></div>
  <img id="preview" alt="Map preview">
  <div class="choices" id="choices"></div>
  <div class="nav"><button id="previous">Previous</button><button id="next">Next</button><button id="export">Export labels.json</button></div>
</section>
<script>
const entries = {entries_json};
const levels = {levels_json};
const feature = {json.dumps(feature)};
const scoreField = {score_field_json};
const percentileField = {percentile_field_json};
const storageKey = {json.dumps(storage_key)};
let labels = JSON.parse(localStorage.getItem(storageKey) || '{{}}');
let index = Math.max(0, entries.findIndex(entry => !labels[entry.published_file_id]));
const byId = id => document.getElementById(id);
function save() {{ localStorage.setItem(storageKey, JSON.stringify(labels)); }}
function show() {{
  const entry = entries[index];
  byId('counter').textContent = `${{index + 1}} / ${{entries.length}}`;
  byId('bar').style.width = `${{100 * (index + 1) / entries.length}}%`;
  byId('done').textContent = `${{Object.keys(labels).length}} labeled`;
  byId('title').textContent = entry.title;
  byId('meta').textContent = `Workshop ${{entry.published_file_id}} · current score ${{entry[scoreField].toFixed(4)}} · percentile ${{(100 * entry[percentileField]).toFixed(1)}}%`;
  byId('preview').src = entry.image_path;
  byId('choices').innerHTML = '';
  levels.forEach(([value, caption], levelIndex) => {{
    const button = document.createElement('button');
    button.textContent = `${{levelIndex + 1}} · ${{caption}}`;
    if (labels[entry.published_file_id]?.level === value) button.classList.add('selected');
    button.onclick = () => label(value);
    byId('choices').appendChild(button);
  }});
  byId('previous').disabled = index === 0;
  byId('next').disabled = index === entries.length - 1;
}}
function label(level) {{
  const entry = entries[index];
  labels[entry.published_file_id] = {{ level, title: entry.title, [scoreField]: entry[scoreField], [percentileField]: entry[percentileField] }};
  save();
  if (index < entries.length - 1) index++;
  show();
}}
byId('previous').onclick = () => {{ index = Math.max(0, index - 1); show(); }};
byId('next').onclick = () => {{ index = Math.min(entries.length - 1, index + 1); show(); }};
byId('export').onclick = () => {{
  const payload = {{ schema_version: 1, feature, labels: entries.filter(entry => labels[entry.published_file_id]).map(entry => ({{ ...entry, level: labels[entry.published_file_id].level }})) }};
  const link = document.createElement('a');
  link.href = URL.createObjectURL(new Blob([JSON.stringify(payload, null, 2)], {{type: 'application/json'}}));
  link.download = {json.dumps(export_filename)};
  link.click();
  URL.revokeObjectURL(link.href);
}};
document.addEventListener('keydown', event => {{
  if (/^[1-5]$/.test(event.key)) label(levels[Number(event.key) - 1][0]);
  else if (event.key === 'ArrowLeft') byId('previous').click();
  else if (event.key === 'ArrowRight') byId('next').click();
}});
show();
</script>
</main></body></html>
"""


def main() -> None:
    args = parse_args()
    if args.download_concurrency < 1:
        raise ValueError("--download-concurrency must be positive")
    if args.max_image_bytes < 1:
        raise ValueError("--max-image-bytes must be positive")
    output_directory = Path(args.output_directory)
    output_directory.mkdir(parents=True, exist_ok=True)
    candidates = read_candidates(args.index)
    selected = select_candidates(candidates, args.count, args.include_id)
    entries = prepare_entries(selected, output_directory, args.download_concurrency, args.max_image_bytes)
    (output_directory / "calibration-set.json").write_text(
        json.dumps({"schema_version": 1, "feature": FOREST_FEATURE, "maps": entries}, indent=2, ensure_ascii=False),
        encoding="utf-8",
    )
    (output_directory / "index.html").write_text(render_html(entries), encoding="utf-8")
    print(f"Wrote {len(entries)} maps to {output_directory / 'index.html'}")


if __name__ == "__main__":
    main()
