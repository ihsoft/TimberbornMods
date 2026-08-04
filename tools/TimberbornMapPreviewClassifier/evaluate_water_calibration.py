#!/usr/bin/env python3
"""Evaluate water and moist-soil CLIP strategies against human labels."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from statistics import fmean

import evaluate_forest_calibration as evaluation
import normalize_map_preview as normalization


LEVEL_VALUES = {"very_low": 0, "low": 1, "moderate": 2, "high": 3, "very_high": 4}

CURRENT_WATER_PROMPTS = {
    "positive": (
        "a Timberborn map dominated by large rivers lakes and water",
        "an isometric strategy game map with most of its area covered by water",
        "a wet landscape with extensive waterways",
    ),
    "negative": (
        "a dry Timberborn map with very little visible water",
        "an isometric strategy game map dominated by dry land",
        "an arid landscape with few waterways",
    ),
}

FREE_WATER_PROMPTS = {
    "positive": (
        "a large proportion of this Timberborn map is covered by visible blue free water",
        "an isometric map with extensive blue rivers lakes and open water relative to its total area",
        "a map landscape containing a high density of blue free-water tiles",
    ),
    "negative": (
        "a Timberborn map with almost no visible blue free water",
        "an isometric map dominated by land with very few blue rivers or lakes",
        "a map landscape containing a very low density of blue free-water tiles",
    ),
}

MOIST_SOIL_PROMPTS = {
    "positive": (
        "a large proportion of this Timberborn map is green irrigated moist soil",
        "an isometric map with extensive bright green watered ground relative to its total area",
        "a map landscape containing a high density of green fertile soil supplied with water",
    ),
    "negative": (
        "a Timberborn map dominated by dry brown barren soil",
        "an isometric map with almost no green irrigated or moist ground",
        "a dry map landscape containing a very low density of green watered soil",
    ),
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--labels", required=True)
    parser.add_argument(
        "--calibration-set",
        default=".tools/map-vision/water-calibration/calibration-set.json",
    )
    parser.add_argument("--model", default="openai/clip-vit-base-patch32")
    parser.add_argument("--batch-size", type=int, default=16)
    parser.add_argument(
        "--normalization", choices=("original", "mask_only", "mask_crop"), default="original"
    )
    parser.add_argument(
        "--output", default=".tools/map-vision/water-calibration/evaluation.json"
    )
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    import torch
    from PIL import Image
    from transformers import CLIPModel, CLIPProcessor

    labels_document = json.loads(Path(args.labels).read_text(encoding="utf-8"))
    labels_by_id = {item["published_file_id"]: item for item in labels_document["labels"]}
    calibration_document = json.loads(Path(args.calibration_set).read_text(encoding="utf-8"))
    entries = [entry for entry in calibration_document["maps"] if entry["published_file_id"] in labels_by_id]
    if len(entries) != len(labels_by_id):
        raise ValueError("Calibration set does not contain every labeled map")

    model = CLIPModel.from_pretrained(args.model)
    processor = CLIPProcessor.from_pretrained(args.model, use_fast=False)
    model.eval()

    image_directory = Path(args.calibration_set).parent
    image_records = []
    normalized_count = 0
    for entry in entries:
        with Image.open(image_directory / entry["image_path"]) as source:
            image = source.convert("RGB")
        if args.normalization == "mask_only":
            image, normalization_info = normalization.mask_background(image)
            normalized_count += int(normalization_info["applied"])
        elif args.normalization == "mask_crop":
            image, normalization_info = normalization.mask_and_crop(image)
            normalized_count += int(normalization_info["applied"])
        crops = evaluation.make_crops(image)
        crops.update(evaluation.make_grid_crops(image, 3))
        for crop_name, crop in crops.items():
            image_records.append((entry["published_file_id"], crop_name, crop))

    embeddings = {}
    for offset in range(0, len(image_records), args.batch_size):
        batch = image_records[offset : offset + args.batch_size]
        inputs = processor(images=[record[2] for record in batch], return_tensors="pt")
        with torch.inference_mode():
            batch_embeddings = evaluation.normalized(model.get_image_features(**inputs))
        for record, embedding in zip(batch, batch_embeddings):
            embeddings[(record[0], record[1])] = embedding
        print(f"Embedded {min(offset + len(batch), len(image_records))} / {len(image_records)} crops", flush=True)

    prototypes = {
        name: (
            evaluation.build_prototype(model, processor, prompts["positive"]),
            evaluation.build_prototype(model, processor, prompts["negative"]),
        )
        for name, prompts in (
            ("current", CURRENT_WATER_PROMPTS),
            ("free_water", FREE_WATER_PROMPTS),
            ("moist_soil", MOIST_SOIL_PROMPTS),
        )
    }

    component_scores = {}
    for component, (positive, negative) in prototypes.items():
        whole_scores = []
        quadrant_scores = []
        for entry in entries:
            published_file_id = entry["published_file_id"]
            whole = embeddings[(published_file_id, "whole")]
            whole_scores.append(float(whole @ positive - whole @ negative))
            quadrant_scores.append(
                fmean(
                    float(embeddings[(published_file_id, crop)] @ positive - embeddings[(published_file_id, crop)] @ negative)
                    for crop in ("top_left", "top_right", "bottom_left", "bottom_right")
                )
            )
        component_scores[component + "_whole"] = whole_scores
        component_scores[component + "_quadrant_mean"] = quadrant_scores

        grid_scores = []
        for entry in entries:
            published_file_id = entry["published_file_id"]
            grid_scores.append(
                fmean(
                    float(embeddings[(published_file_id, f"grid_3_{row}_{column}")] @ positive - embeddings[(published_file_id, f"grid_3_{row}_{column}")] @ negative)
                    for row in range(3)
                    for column in range(3)
                )
            )
        component_scores[component + "_grid_3_mean"] = grid_scores

    strategies = dict(component_scores)
    free_scores = component_scores["free_water_quadrant_mean"]
    moist_scores = component_scores["moist_soil_quadrant_mean"]
    for weight in (0.25, 0.5, 0.75, 1.0):
        strategies[f"free_water_plus_{weight:g}_moist_soil_quadrant_mean"] = [
            free + weight * moist for free, moist in zip(free_scores, moist_scores)
        ]
    current_scores = component_scores["current_quadrant_mean"]
    for weight in (0.25, 0.5, 0.75, 1.0):
        strategies[f"current_water_plus_{weight:g}_moist_soil_quadrant_mean"] = [
            current + weight * moist for current, moist in zip(current_scores, moist_scores)
        ]
    current_grid_scores = component_scores["current_grid_3_mean"]
    moist_grid_scores = component_scores["moist_soil_grid_3_mean"]
    for weight in (0.25, 0.5, 0.75, 1.0):
        strategies[f"current_water_plus_{weight:g}_moist_soil_grid_3_mean"] = [
            current + weight * moist for current, moist in zip(current_grid_scores, moist_grid_scores)
        ]

    label_values = [LEVEL_VALUES[labels_by_id[entry["published_file_id"]]["level"]] for entry in entries]
    report = {
        "schema_version": 1,
        "model": args.model,
        "labeled_maps": len(entries),
        "normalization": args.normalization,
        "normalized_maps": normalized_count,
        "strategies": {},
    }
    for name, scores in strategies.items():
        thresholds = evaluation.fit_thresholds(label_values, scores)
        report["strategies"][name] = {
            "spearman": evaluation.spearman(label_values, scores),
            "ordered_pair_accuracy": evaluation.ordered_pair_accuracy(label_values, scores),
            "fitted_thresholds": thresholds,
            "fitted_metrics": evaluation.prediction_metrics(
                label_values, evaluation.predict_levels(scores, thresholds)
            ),
            "leave_one_out_metrics": evaluation.leave_one_out_metrics(label_values, scores),
        }
    report["maps"] = [
        {
            "published_file_id": entry["published_file_id"],
            "title": entry["title"],
            "level": labels_by_id[entry["published_file_id"]]["level"],
            "scores": {name: scores[index] for name, scores in strategies.items()},
        }
        for index, entry in enumerate(entries)
    ]
    output = Path(args.output)
    output.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
    print(json.dumps(report["strategies"], indent=2))


if __name__ == "__main__":
    main()
