#!/usr/bin/env python3
"""Evaluate forest prompt and crop strategies against human calibration labels."""

from __future__ import annotations

import argparse
import bisect
import json
from pathlib import Path
from statistics import fmean


LEVEL_VALUES = {"none": 0, "sparse": 1, "moderate": 2, "forested": 3, "dense": 4}

CURRENT_PROMPTS = {
    "positive": (
        "a Timberborn map densely covered by forests and trees",
        "a heavily forested isometric strategy game landscape",
        "a lush terrain map with extensive tree cover",
    ),
    "negative": (
        "a barren Timberborn map with very few trees",
        "an open isometric landscape without forests",
        "a sparsely vegetated terrain map",
    ),
}

GREEN_TREE_PROMPTS = {
    "positive": (
        "a Timberborn map with a high density of living green trees relative to its total area",
        "an isometric strategy game map with many healthy green trees across the map",
        "a map landscape containing dense groups of living green trees",
    ),
    "negative": (
        "a Timberborn map with no living green trees",
        "an isometric strategy game map containing only dead dry trees and no green forest",
        "a map landscape with very low green tree density relative to its total area",
    ),
}

ORDINAL_GREEN_TREE_PROMPTS = (
    (
        "a Timberborn map with no living green trees",
        "a map landscape containing only bare terrain water and dead dry trees",
    ),
    (
        "a Timberborn map with a low density of scattered living green trees relative to its total area",
        "an isometric map with only a few small groups of healthy green trees",
    ),
    (
        "a Timberborn map with moderate living green tree density relative to its total area",
        "an isometric map with noticeable but moderate green tree cover",
    ),
    (
        "a Timberborn map with high living green tree density across several areas",
        "an isometric map with many groups of healthy green trees relative to its total area",
    ),
    (
        "a Timberborn map with the highest practical density of living green forest",
        "an isometric map densely populated by healthy green trees across available areas",
    ),
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--labels", required=True)
    parser.add_argument(
        "--calibration-set",
        default=".tools/map-vision/forest-calibration/calibration-set.json",
    )
    parser.add_argument("--model", default="openai/clip-vit-base-patch32")
    parser.add_argument("--batch-size", type=int, default=16)
    parser.add_argument(
        "--output", default=".tools/map-vision/forest-calibration/evaluation.json"
    )
    return parser.parse_args()


def normalized(tensor):
    return tensor / tensor.norm(dim=-1, keepdim=True)


def make_crops(image):
    """Return the whole image and four equal-area quadrants."""
    width, height = image.size
    middle_x, middle_y = width // 2, height // 2
    return {
        "whole": image,
        "top_left": image.crop((0, 0, middle_x, middle_y)),
        "top_right": image.crop((middle_x, 0, width, middle_y)),
        "bottom_left": image.crop((0, middle_y, middle_x, height)),
        "bottom_right": image.crop((middle_x, middle_y, width, height)),
    }


def make_grid_crops(image, grid_size: int):
    if grid_size < 1:
        raise ValueError("grid_size must be positive")
    width, height = image.size
    crops = {}
    for row in range(grid_size):
        top = height * row // grid_size
        bottom = height * (row + 1) // grid_size
        for column in range(grid_size):
            left = width * column // grid_size
            right = width * (column + 1) // grid_size
            crops[f"grid_{grid_size}_{row}_{column}"] = image.crop((left, top, right, bottom))
    return crops


def rank(values: list[float]) -> list[float]:
    ordered = sorted(range(len(values)), key=values.__getitem__)
    ranks = [0.0] * len(values)
    position = 0
    while position < len(ordered):
        end = position + 1
        while end < len(ordered) and values[ordered[end]] == values[ordered[position]]:
            end += 1
        average_rank = (position + end - 1) / 2
        for ordered_index in ordered[position:end]:
            ranks[ordered_index] = average_rank
        position = end
    return ranks


def pearson(left: list[float], right: list[float]) -> float:
    left_mean, right_mean = fmean(left), fmean(right)
    numerator = sum((a - left_mean) * (b - right_mean) for a, b in zip(left, right))
    left_scale = sum((value - left_mean) ** 2 for value in left) ** 0.5
    right_scale = sum((value - right_mean) ** 2 for value in right) ** 0.5
    return numerator / (left_scale * right_scale) if left_scale and right_scale else 0.0


def spearman(labels: list[int], scores: list[float]) -> float:
    return pearson(rank(labels), rank(scores))


def ordered_pair_accuracy(labels: list[int], scores: list[float]) -> float:
    correct = 0.0
    pairs = 0
    for left in range(len(labels)):
        for right in range(left + 1, len(labels)):
            if labels[left] == labels[right]:
                continue
            pairs += 1
            expected = labels[left] < labels[right]
            observed = scores[left] < scores[right]
            if scores[left] == scores[right]:
                correct += 0.5
            elif expected == observed:
                correct += 1
    return correct / pairs if pairs else 0.0


def fit_thresholds(labels: list[int], scores: list[float]) -> list[float]:
    """Fit four monotonic score thresholds with minimum absolute class error."""
    ordered = sorted(zip(scores, labels))
    count = len(ordered)
    level_count = len(LEVEL_VALUES)
    if count < level_count:
        raise ValueError("At least one calibration item per level is required")
    # segment_cost[level][start][end] assigns sorted items [start:end] to level.
    segment_cost = [
        [
            [sum(abs(ordered[index][1] - level) for index in range(start, end)) for end in range(count + 1)]
            for start in range(count + 1)
        ]
        for level in range(level_count)
    ]
    infinity = float("inf")
    dp = [[infinity] * (count + 1) for _ in range(level_count)]
    previous = [[-1] * (count + 1) for _ in range(level_count)]
    for end in range(1, count + 1):
        dp[0][end] = segment_cost[0][0][end]
    for level in range(1, level_count):
        for end in range(level + 1, count + 1):
            for split in range(level, end):
                cost = dp[level - 1][split] + segment_cost[level][split][end]
                if cost < dp[level][end]:
                    dp[level][end] = cost
                    previous[level][end] = split
    splits = []
    end = count
    for level in range(level_count - 1, 0, -1):
        split = previous[level][end]
        splits.append(split)
        end = split
    splits.reverse()
    return [
        (ordered[split - 1][0] + ordered[split][0]) / 2
        for split in splits
    ]


def predict_levels(scores: list[float], thresholds: list[float]) -> list[int]:
    return [bisect.bisect_right(thresholds, score) for score in scores]


def prediction_metrics(expected: list[int], predicted: list[int]) -> dict:
    errors = [abs(left - right) for left, right in zip(expected, predicted)]
    return {
        "exact_accuracy": sum(error == 0 for error in errors) / len(errors),
        "within_one_accuracy": sum(error <= 1 for error in errors) / len(errors),
        "mean_absolute_error": fmean(errors),
    }


def leave_one_out_metrics(labels: list[int], scores: list[float]) -> dict:
    predicted = []
    for excluded in range(len(labels)):
        training_labels = labels[:excluded] + labels[excluded + 1 :]
        training_scores = scores[:excluded] + scores[excluded + 1 :]
        thresholds = fit_thresholds(training_labels, training_scores)
        predicted.append(predict_levels([scores[excluded]], thresholds)[0])
    return prediction_metrics(labels, predicted)


def build_prototype(model, processor, prompts):
    import torch

    inputs = processor(text=prompts, return_tensors="pt", padding=True)
    with torch.inference_mode():
        embeddings = normalized(model.get_text_features(**inputs))
    prototype = embeddings.mean(dim=0)
    return prototype / prototype.norm()


def main() -> None:
    args = parse_args()
    import torch
    from PIL import Image
    from transformers import CLIPModel, CLIPProcessor

    labels_document = json.loads(Path(args.labels).read_text(encoding="utf-8"))
    labels_by_id = {
        item["published_file_id"]: item for item in labels_document["labels"]
    }
    calibration_document = json.loads(
        Path(args.calibration_set).read_text(encoding="utf-8")
    )
    entries = [
        entry
        for entry in calibration_document["maps"]
        if entry["published_file_id"] in labels_by_id
    ]
    if len(entries) != len(labels_by_id):
        raise ValueError("Calibration set does not contain every labeled map")

    model = CLIPModel.from_pretrained(args.model)
    processor = CLIPProcessor.from_pretrained(args.model)
    model.eval()

    image_directory = Path(args.calibration_set).parent
    image_records = []
    for entry in entries:
        with Image.open(image_directory / entry["image_path"]) as source:
            image = source.convert("RGB")
        for crop_name, crop in make_crops(image).items():
            image_records.append((entry["published_file_id"], crop_name, crop))

    embeddings = {}
    for offset in range(0, len(image_records), args.batch_size):
        batch = image_records[offset : offset + args.batch_size]
        inputs = processor(images=[record[2] for record in batch], return_tensors="pt")
        with torch.inference_mode():
            batch_embeddings = normalized(model.get_image_features(**inputs))
        for record, embedding in zip(batch, batch_embeddings):
            embeddings[(record[0], record[1])] = embedding
        print(f"Embedded {min(offset + len(batch), len(image_records))} / {len(image_records)} crops", flush=True)

    strategies = {}
    for prompt_name, prompts in (
        ("current_prompts", CURRENT_PROMPTS),
        ("living_green_tree_prompts", GREEN_TREE_PROMPTS),
    ):
        positive = build_prototype(model, processor, prompts["positive"])
        negative = build_prototype(model, processor, prompts["negative"])
        whole_scores = []
        quadrant_scores = []
        for entry in entries:
            published_file_id = entry["published_file_id"]
            whole = embeddings[(published_file_id, "whole")]
            whole_scores.append(float(whole @ positive - whole @ negative))
            tile_values = [
                float(embeddings[(published_file_id, crop)] @ positive - embeddings[(published_file_id, crop)] @ negative)
                for crop in ("top_left", "top_right", "bottom_left", "bottom_right")
            ]
            quadrant_scores.append(fmean(tile_values))
        strategies[prompt_name + "_whole"] = whole_scores
        strategies[prompt_name + "_quadrant_mean"] = quadrant_scores

    ordinal_prototypes = torch.stack(
        [build_prototype(model, processor, prompts) for prompts in ORDINAL_GREEN_TREE_PROMPTS]
    )
    ordinal_whole_scores = []
    ordinal_quadrant_scores = []
    logit_scale = float(model.logit_scale.exp())
    for entry in entries:
        published_file_id = entry["published_file_id"]

        def ordinal_score(crop_name: str) -> float:
            similarities = embeddings[(published_file_id, crop_name)] @ ordinal_prototypes.T
            probabilities = torch.softmax(similarities * logit_scale, dim=0)
            return float(probabilities @ torch.arange(5, dtype=probabilities.dtype))

        ordinal_whole_scores.append(ordinal_score("whole"))
        ordinal_quadrant_scores.append(
            fmean(
                ordinal_score(crop)
                for crop in ("top_left", "top_right", "bottom_left", "bottom_right")
            )
        )
    strategies["ordinal_green_tree_prompts_whole"] = ordinal_whole_scores
    strategies["ordinal_green_tree_prompts_quadrant_mean"] = ordinal_quadrant_scores

    label_values = [LEVEL_VALUES[labels_by_id[entry["published_file_id"]]["level"]] for entry in entries]
    report = {
        "schema_version": 1,
        "model": args.model,
        "labeled_maps": len(entries),
        "strategies": {},
    }
    for name, scores in strategies.items():
        thresholds = fit_thresholds(label_values, scores)
        fitted_predictions = predict_levels(scores, thresholds)
        report["strategies"][name] = {
            "spearman": spearman(label_values, scores),
            "ordered_pair_accuracy": ordered_pair_accuracy(label_values, scores),
            "fitted_thresholds": thresholds,
            "fitted_metrics": prediction_metrics(label_values, fitted_predictions),
            "leave_one_out_metrics": leave_one_out_metrics(label_values, scores),
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
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
    print(json.dumps(report["strategies"], indent=2))


if __name__ == "__main__":
    main()
