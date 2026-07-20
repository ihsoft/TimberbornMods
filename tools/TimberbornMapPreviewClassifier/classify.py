#!/usr/bin/env python3
"""Classify public Timberborn map previews with account-independent CLIP features."""

from __future__ import annotations

import argparse
import bisect
from concurrent.futures import ThreadPoolExecutor
from io import BytesIO
import json
from pathlib import Path
from statistics import fmean, median
import time
from typing import Iterable
from urllib.request import Request, urlopen

CLASSIFIER_VERSION = "clip-prompts-v2-multi-image"
LEGACY_CLASSIFIER_VERSIONS = {"clip-prompts-v1"}
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
    parser.add_argument("--max-image-bytes", type=int, default=2_000_000)
    parser.add_argument("--previous-results")
    parser.add_argument("--gallery-results")
    parser.add_argument("--plan-only", action="store_true")
    parser.add_argument("--plan-output")
    return parser.parse_args()


def load_maps(
    snapshot: Path, preview_directory: Path, max_items: int,
    download_previews: bool, gallery_results: list[dict]
) -> list[dict]:
    gallery_by_id = {record["published_file_id"]: record for record in gallery_results}
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
            gallery = gallery_by_id.get(record["published_file_id"], {})
            images = [{"url": preview_url, "role": "primary", "path": preview_path}]
            images.extend(
                {"url": url, "role": "gallery", "path": None}
                for url in gallery.get("gallery_urls", [])
                if url and url != preview_url
            )
            maps.append({
                "published_file_id": record["published_file_id"],
                "title": record["title"],
                "preview_url": preview_url,
                "images": images,
                "gallery_collection_state": gallery.get("collection_state"),
            })
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
        and classifier_version in {CLASSIFIER_VERSION, *LEGACY_CLASSIFIER_VERSIONS}
        and all(feature in scores for feature in FEATURE_PROMPTS)
    )


def classified_preview_url(result: dict) -> str | None:
    return result.get("classified_preview_url", result.get("preview_url"))


def previous_image_scores(previous: dict) -> list[dict]:
    if previous.get("visual_image_scores"):
        return previous["visual_image_scores"]
    url = classified_preview_url(previous)
    if not url:
        return []
    return [{"url": url, "role": "primary", "scores": previous["visual_scores"]}]


def plan_incremental(
    maps: list[dict], previous_results: list[dict], model_name: str
) -> tuple[dict[str, dict[str, dict]], list[dict], dict[str, dict]]:
    previous_by_id = {
        result["published_file_id"]: result
        for result in previous_results
        if has_compatible_scores(result, model_name)
    }
    reusable_by_map = {}
    to_classify = []
    for item in maps:
        previous = previous_by_id.get(item["published_file_id"])
        previous_images = {
            image["url"]: image for image in previous_image_scores(previous)
        } if previous else {}
        reusable_by_map[item["published_file_id"]] = {
            image["url"]: previous_images[image["url"]]
            for image in item["images"]
            if image["url"] in previous_images
        }
        to_classify.extend(
            {
                "published_file_id": item["published_file_id"],
                "title": item["title"],
                "image_url": image["url"],
                "image_role": image["role"],
                "image_path": image["path"],
            }
            for image in item["images"]
            if image["url"] not in previous_images
        )
    return reusable_by_map, to_classify, previous_by_id


def write_plan(
    path: Path | None, maps: list[dict], reusable_by_map: dict[str, dict[str, dict]],
    to_classify: list[dict]
) -> None:
    images_total = sum(len(item["images"]) for item in maps)
    images_reused = sum(len(images) for images in reusable_by_map.values())
    plan = {
        "maps_total": len(maps),
        "images_total": images_total,
        "images_reused": images_reused,
        "images_to_classify": len(to_classify),
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


def load_image(item: dict, download_previews: bool, max_image_bytes: int) -> Image.Image:
    if download_previews:
        request = Request(
            item["image_url"],
            headers={"User-Agent": "TimberbornMods-PublicWorkshopIndexer/1.0"},
        )
        image_bytes = None
        last_exception = None
        for attempt in range(3):
            try:
                with urlopen(request, timeout=60) as response:
                    content_length = response.headers.get("Content-Length")
                    if content_length and int(content_length) > max_image_bytes:
                        raise RuntimeError(
                            f"Image exceeds {max_image_bytes} byte limit: {content_length}"
                        )
                    image_bytes = response.read(max_image_bytes + 1)
                    if len(image_bytes) > max_image_bytes:
                        raise RuntimeError(f"Image exceeds {max_image_bytes} byte limit")
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
    with Image.open(item["image_path"]) as image:
        return image.convert("RGB")


def try_load_image(
    item: dict, download_previews: bool, max_image_bytes: int
) -> tuple[Image.Image | None, Exception | None]:
    try:
        return load_image(item, download_previews, max_image_bytes), None
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


def score_images(
    images_to_classify: list[dict],
    model: CLIPModel,
    processor: CLIPProcessor,
    batch_size: int,
    download_previews: bool,
    download_concurrency: int,
    max_image_bytes: int,
) -> list[dict]:
    prototypes = build_text_prototypes(model, processor)
    results = []
    for batch_number, batch in enumerate(chunks(images_to_classify, batch_size), start=1):
        images: list[Image.Image] = []
        valid = []
        with ThreadPoolExecutor(max_workers=download_concurrency) as executor:
            loaded_images = list(
                executor.map(
                    lambda item: try_load_image(item, download_previews, max_image_bytes), batch
                )
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
                    "url": item["image_url"],
                    "role": item["image_role"],
                    "scores": scores,
                }
            )
        print(
            f"Batch {batch_number}: classified {len(results)} / {len(images_to_classify)} images",
            flush=True,
        )
    return results


def aggregate_map_results(
    maps: list[dict], reusable_by_map: dict[str, dict[str, dict]],
    classified: list[dict], previous_by_id: dict[str, dict]
) -> list[dict]:
    classified_by_map = {}
    for image in classified:
        classified_by_map.setdefault(image["published_file_id"], {})[image["url"]] = image
    results = []
    for item in maps:
        published_file_id = item["published_file_id"]
        desired_urls = {image["url"] for image in item["images"]}
        images = {
            **reusable_by_map.get(published_file_id, {}),
            **classified_by_map.get(published_file_id, {}),
        }
        missing_urls = desired_urls - images.keys()
        previous = previous_by_id.get(published_file_id)
        if not images and previous:
            images = {image["url"]: image for image in previous_image_scores(previous)}
        if not images:
            continue
        image_scores = list(images.values())
        aggregates = {}
        for feature in FEATURE_PROMPTS:
            values = [image["scores"][feature] for image in image_scores]
            aggregates[feature] = {
                "median": median(values),
                "mean": fmean(values),
                "min": min(values),
                "max": max(values),
                "spread": max(values) - min(values),
            }
        newly_classified = len(classified_by_map.get(published_file_id, {}))
        stale = bool(missing_urls) or item.get("gallery_collection_state") in {
            "stale", "deferred"
        }
        state = "stale" if stale else "classified" if newly_classified else "reused"
        primary = next(
            (image for image in image_scores if image["url"] == item["preview_url"]), None
        )
        results.append({
            "published_file_id": published_file_id,
            "title": item["title"],
            "preview_url": item["preview_url"],
            "classified_preview_url": (
                primary["url"] if primary else classified_preview_url(previous or {})
            ),
            "visual_scores": {
                feature: values["median"] for feature, values in aggregates.items()
            },
            "visual_score_aggregates": aggregates,
            "visual_image_scores": image_scores,
            "visual_image_count": len(image_scores),
            "visual_gallery_image_count": sum(
                image.get("role") == "gallery" for image in image_scores
            ),
            "visual_missing_image_count": len(missing_urls),
            "visual_stale": stale,
            "classification_state": state,
            "images_classified_this_run": newly_classified,
        })
    return results


def add_percentiles_and_labels(results: list[dict]) -> None:
    aggregate_names = ("median", "mean", "min", "max", "spread")
    distributions = {
        feature: {
            aggregate: sorted(
                result["visual_score_aggregates"][feature][aggregate]
                for result in results
            )
            for aggregate in aggregate_names
        }
        for feature in FEATURE_PROMPTS
    }
    count = len(results)
    for result in results:
        aggregate_percentiles = {}
        for feature, feature_distributions in distributions.items():
            aggregate_percentiles[feature] = {}
            for aggregate, distribution in feature_distributions.items():
                score = result["visual_score_aggregates"][feature][aggregate]
                aggregate_percentiles[feature][aggregate] = (
                    bisect.bisect_right(distribution, score) - 0.5
                ) / count
        result["visual_percentile_aggregates"] = aggregate_percentiles
        percentiles = {
            feature: aggregate_percentiles[feature]["median"]
            for feature in FEATURE_PROMPTS
        }
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
    if args.max_image_bytes < 1:
        raise ValueError("--max-image-bytes must be positive")
    maps = load_maps(
        Path(args.snapshot),
        Path(args.preview_directory),
        args.max_items,
        args.download_previews,
        read_json_lines(Path(args.gallery_results)) if args.gallery_results else [],
    )
    if not maps:
        raise RuntimeError("No map previews were available")
    previous_results = read_json_lines(Path(args.previous_results)) if args.previous_results else []
    reusable_by_map, to_classify, previous_by_id = plan_incremental(
        maps, previous_results, args.model
    )
    write_plan(
        Path(args.plan_output) if args.plan_output else None,
        maps,
        reusable_by_map,
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
        classified = score_images(
            to_classify,
            model,
            processor,
            args.batch_size,
            args.download_previews,
            args.download_concurrency,
            args.max_image_bytes,
        )

    results = aggregate_map_results(maps, reusable_by_map, classified, previous_by_id)
    if not results:
        raise RuntimeError("No map previews could be classified or reused")
    add_percentiles_and_labels(results)
    write_results(Path(args.output), results, args.model)
    stale_count = sum(result["visual_stale"] for result in results)
    reused_images = sum(len(images) for images in reusable_by_map.values())
    print(
        f"Wrote {len(results)} records to {Path(args.output).resolve()}; "
        f"reused {reused_images} images, classified {len(classified)} images, "
        f"stale {stale_count} maps",
        flush=True,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
