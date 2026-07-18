#!/usr/bin/env python3
"""Classify public Timberborn map previews with account-independent CLIP features."""

from __future__ import annotations

import argparse
import bisect
from concurrent.futures import ThreadPoolExecutor
from io import BytesIO
import json
from pathlib import Path
import time
from typing import Iterable
from urllib.request import Request, urlopen

CLASSIFIER_VERSION = "clip-prompts-v1"
FEATURE_PROMPTS = {
    "ruggedness": {
        "positive": [
            "a Timberborn custom map dominated by steep mountains and high elevation terrain",
            "an isometric strategy game map with rugged mountains cliffs and many terraces",
            "a mountainous landscape with many dramatic elevation changes",
        ],
        "negative": [
            "a Timberborn custom map dominated by broad flat terrain with almost no elevation changes",
            "an isometric strategy game map made of large level plains",
            "a very flat landscape with few cliffs or terraces",
        ],
    },
    "canyonness": {
        "positive": [
            "a Timberborn custom map dominated by deep canyons and narrow valleys",
            "an isometric strategy game map with steep canyon walls and a narrow valley floor",
            "a landscape carved into deep gorges and canyons",
        ],
        "negative": [
            "an open landscape without canyons or narrow valleys",
            "a broad unobstructed strategy game map",
            "a landscape made of wide open terrain",
        ],
    },
    "water_dominance": {
        "positive": [
            "a Timberborn map dominated by large rivers lakes and water",
            "an isometric strategy game map with most of its area covered by water",
            "a wet landscape with extensive waterways",
        ],
        "negative": [
            "a dry Timberborn map with very little visible water",
            "an isometric strategy game map dominated by dry land",
            "an arid landscape with few waterways",
        ],
    },
    "islandness": {
        "positive": [
            "a Timberborn map made of islands surrounded by water",
            "an archipelago strategy game map",
            "a landscape containing multiple distinct islands",
        ],
        "negative": [
            "a continuous mainland landscape without islands",
            "a strategy game map made of one connected landmass",
            "an inland terrain map",
        ],
    },
    "forest_density": {
        "positive": [
            "a Timberborn map densely covered by forests and trees",
            "a heavily forested isometric strategy game landscape",
            "a lush terrain map with extensive tree cover",
        ],
        "negative": [
            "a barren Timberborn map with very few trees",
            "an open isometric landscape without forests",
            "a sparsely vegetated terrain map",
        ],
    },
    "artificial_layout": {
        "positive": [
            "an artificial Timberborn map with geometric shapes straight channels and regular patterns",
            "a deliberately engineered strategy game map with a grid layout",
            "a highly symmetrical man-made terrain layout",
        ],
        "negative": [
            "a natural-looking Timberborn landscape with irregular terrain",
            "an organic winding river landscape",
            "a realistic natural terrain map without geometric patterns",
        ],
    },
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--snapshot",
        default=".tools/workshop-index/timberborn-workshop-bootstrap.jsonl",
    )
    parser.add_argument("--preview-directory", default=".tools/workshop-index/previews")
    parser.add_argument(
        "--output",
        default=".tools/workshop-index/timberborn-map-visual-features.jsonl",
    )
    parser.add_argument("--model", default="openai/clip-vit-base-patch32")
    parser.add_argument("--batch-size", type=int, default=24)
    parser.add_argument("--max-items", type=int, default=0)
    parser.add_argument("--download-previews", action="store_true")
    parser.add_argument("--download-concurrency", type=int, default=6)
    parser.add_argument("--previous-results")
    parser.add_argument("--plan-only", action="store_true")
    parser.add_argument("--plan-output")
    return parser.parse_args()


def load_maps(
    snapshot: Path, preview_directory: Path, max_items: int, download_previews: bool
) -> list[dict]:
    maps = []
    with snapshot.open("r", encoding="utf-8") as stream:
        for line in stream:
            record = json.loads(line)
            if record.get("primary_category") != "map":
                continue
            preview_url = record.get("preview_url")
            cache_path = record.get("preview_cache_path")
            preview_path = preview_directory / cache_path if cache_path else None
            if not download_previews and (preview_path is None or not preview_path.is_file()):
                continue
            if download_previews and not preview_url:
                continue
            maps.append(
                {
                    "published_file_id": record["published_file_id"],
                    "title": record["title"],
                    "preview_url": preview_url,
                    "preview_path": preview_path,
                }
            )
            if max_items and len(maps) >= max_items:
                break
    return maps


def chunks(items: list[dict], size: int) -> Iterable[list[dict]]:
    for index in range(0, len(items), size):
        yield items[index : index + size]


def read_json_lines(path: Path) -> list[dict]:
    if not path.is_file():
        return []
    if path.suffix == ".gz":
        import gzip

        stream = gzip.open(path, "rt", encoding="utf-8")
    else:
        stream = path.open("r", encoding="utf-8")
    with stream:
        return [json.loads(line) for line in stream if line.strip()]


def has_compatible_scores(result: dict, model_name: str) -> bool:
    classifier_version = result.get("classifier_version", CLASSIFIER_VERSION)
    scores = result.get("visual_scores", {})
    return (
        result.get("model") == model_name
        and classifier_version == CLASSIFIER_VERSION
        and all(feature in scores for feature in FEATURE_PROMPTS)
    )


def classified_preview_url(result: dict) -> str | None:
    return result.get("classified_preview_url", result.get("preview_url"))


def build_reused_result(item: dict, previous: dict, stale: bool = False) -> dict:
    return {
        "published_file_id": item["published_file_id"],
        "title": item["title"],
        "preview_url": item["preview_url"],
        "classified_preview_url": classified_preview_url(previous),
        "visual_scores": previous["visual_scores"],
        "visual_stale": stale,
        "classification_state": "stale" if stale else "reused",
    }


def plan_incremental(
    maps: list[dict], previous_results: list[dict], model_name: str
) -> tuple[list[dict], list[dict], dict[str, dict]]:
    previous_by_id = {
        result["published_file_id"]: result
        for result in previous_results
        if has_compatible_scores(result, model_name)
    }
    reused = []
    to_classify = []
    for item in maps:
        previous = previous_by_id.get(item["published_file_id"])
        if previous and classified_preview_url(previous) == item["preview_url"]:
            reused.append(build_reused_result(item, previous))
        else:
            to_classify.append(item)
    return reused, to_classify, previous_by_id


def write_plan(path: Path | None, maps: list[dict], reused: list[dict], to_classify: list[dict]) -> None:
    plan = {
        "maps_total": len(maps),
        "maps_reused": len(reused),
        "maps_to_classify": len(to_classify),
        "requires_model": bool(to_classify),
        "classifier_version": CLASSIFIER_VERSION,
    }
    serialized = json.dumps(plan, indent=2)
    print(serialized, flush=True)
    if path:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(serialized + "\n", encoding="utf-8")


def load_ml_dependencies() -> None:
    global torch, Image, CLIPModel, CLIPProcessor

    import torch
    from PIL import Image
    from transformers import CLIPModel, CLIPProcessor


def normalized(features: torch.Tensor) -> torch.Tensor:
    return features / features.norm(dim=-1, keepdim=True)


def load_image(item: dict, download_previews: bool) -> Image.Image:
    if download_previews:
        request = Request(
            item["preview_url"],
            headers={"User-Agent": "TimberbornMods-PublicWorkshopIndexer/1.0"},
        )
        image_bytes = None
        last_exception = None
        for attempt in range(3):
            try:
                with urlopen(request, timeout=60) as response:
                    image_bytes = response.read()
                break
            except Exception as exception:
                last_exception = exception
                if attempt < 2:
                    time.sleep(2**attempt)
        if image_bytes is None:
            raise RuntimeError(
                f"Could not download preview after 3 attempts: {last_exception}"
            )
        with Image.open(BytesIO(image_bytes)) as image:
            return image.convert("RGB")
    with Image.open(item["preview_path"]) as image:
        return image.convert("RGB")


def try_load_image(item: dict, download_previews: bool) -> tuple[Image.Image | None, Exception | None]:
    try:
        return load_image(item, download_previews), None
    except Exception as exception:
        return None, exception


def build_text_prototypes(
    model: CLIPModel, processor: CLIPProcessor
) -> dict[str, tuple[torch.Tensor, torch.Tensor]]:
    prototypes = {}
    with torch.inference_mode():
        for feature, prompts in FEATURE_PROMPTS.items():
            feature_prototypes = []
            for polarity in ("positive", "negative"):
                inputs = processor(text=prompts[polarity], return_tensors="pt", padding=True)
                embeddings = normalized(model.get_text_features(**inputs))
                prototype = embeddings.mean(dim=0)
                feature_prototypes.append(prototype / prototype.norm())
            prototypes[feature] = (feature_prototypes[0], feature_prototypes[1])
    return prototypes


def score_maps(
    maps: list[dict],
    model: CLIPModel,
    processor: CLIPProcessor,
    batch_size: int,
    download_previews: bool,
    download_concurrency: int,
) -> list[dict]:
    prototypes = build_text_prototypes(model, processor)
    results = []
    for batch_number, batch in enumerate(chunks(maps, batch_size), start=1):
        images: list[Image.Image] = []
        valid = []
        with ThreadPoolExecutor(max_workers=download_concurrency) as executor:
            loaded_images = list(
                executor.map(lambda item: try_load_image(item, download_previews), batch)
            )
        for item, (image, exception) in zip(batch, loaded_images):
            if image is not None:
                images.append(image)
                valid.append(item)
            else:
                print(f"Skipping {item['published_file_id']}: {exception}", flush=True)
        if not images:
            continue
        inputs = processor(images=images, return_tensors="pt")
        with torch.inference_mode():
            image_embeddings = normalized(model.get_image_features(**inputs))
        for item, embedding in zip(valid, image_embeddings):
            scores = {}
            for feature, (positive, negative) in prototypes.items():
                scores[feature] = float(embedding @ positive - embedding @ negative)
            results.append(
                {
                    "published_file_id": item["published_file_id"],
                    "title": item["title"],
                    "preview_url": item["preview_url"],
                    "classified_preview_url": item["preview_url"],
                    "visual_scores": scores,
                    "visual_stale": False,
                    "classification_state": "classified",
                }
            )
        print(
            f"Batch {batch_number}: classified {len(results)} / {len(maps)} maps",
            flush=True,
        )
    return results


def add_percentiles_and_labels(results: list[dict]) -> None:
    distributions = {
        feature: sorted(result["visual_scores"][feature] for result in results)
        for feature in FEATURE_PROMPTS
    }
    count = len(results)
    for result in results:
        percentiles = {}
        for feature, distribution in distributions.items():
            score = result["visual_scores"][feature]
            percentiles[feature] = (bisect.bisect_right(distribution, score) - 0.5) / count
        result["visual_percentiles"] = percentiles
        labels = []
        if percentiles["ruggedness"] >= 0.80:
            labels.append("predominantly_mountainous")
        if percentiles["ruggedness"] <= 0.20:
            labels.append("predominantly_flat")
        if percentiles["canyonness"] >= 0.85:
            labels.append("canyon_or_narrow_valley")
        if percentiles["water_dominance"] >= 0.85:
            labels.append("water_dominated")
        if percentiles["islandness"] >= 0.85:
            labels.append("islands")
        if percentiles["forest_density"] >= 0.85:
            labels.append("densely_forested")
        if percentiles["artificial_layout"] >= 0.90:
            labels.append("artificial_layout")
        result["visual_labels"] = labels


def write_results(output: Path, results: list[dict], model_name: str) -> None:
    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open("w", encoding="utf-8", newline="\n") as stream:
        for result in results:
            result["model"] = model_name
            result["classifier_version"] = CLASSIFIER_VERSION
            stream.write(json.dumps(result, ensure_ascii=False, separators=(",", ":")) + "\n")


def main() -> int:
    args = parse_args()
    if args.batch_size < 1:
        raise ValueError("--batch-size must be positive")
    if args.download_concurrency < 1 or args.download_concurrency > 16:
        raise ValueError("--download-concurrency must be between 1 and 16")
    maps = load_maps(
        Path(args.snapshot),
        Path(args.preview_directory),
        args.max_items,
        args.download_previews,
    )
    if not maps:
        raise RuntimeError("No map previews were available")
    previous_results = read_json_lines(Path(args.previous_results)) if args.previous_results else []
    reused, to_classify, previous_by_id = plan_incremental(
        maps, previous_results, args.model
    )
    write_plan(
        Path(args.plan_output) if args.plan_output else None,
        maps,
        reused,
        to_classify,
    )
    if args.plan_only:
        return 0

    classified = []
    if to_classify:
        load_ml_dependencies()
        print(f"Loading {args.model} for {len(to_classify)} map previews", flush=True)
        processor = CLIPProcessor.from_pretrained(args.model, use_fast=False)
        model = CLIPModel.from_pretrained(args.model)
        model.eval()
        classified = score_maps(
            to_classify,
            model,
            processor,
            args.batch_size,
            args.download_previews,
            args.download_concurrency,
        )

    results_by_id = {result["published_file_id"]: result for result in reused + classified}
    for item in to_classify:
        if item["published_file_id"] in results_by_id:
            continue
        previous = previous_by_id.get(item["published_file_id"])
        if previous:
            results_by_id[item["published_file_id"]] = build_reused_result(
                item, previous, stale=True
            )
    results = [
        results_by_id[item["published_file_id"]]
        for item in maps
        if item["published_file_id"] in results_by_id
    ]
    if not results:
        raise RuntimeError("No map previews could be classified or reused")
    add_percentiles_and_labels(results)
    write_results(Path(args.output), results, args.model)
    stale_count = sum(result["visual_stale"] for result in results)
    print(
        f"Wrote {len(results)} records to {Path(args.output).resolve()}; "
        f"reused {len(reused)}, classified {len(classified)}, stale {stale_count}",
        flush=True,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
