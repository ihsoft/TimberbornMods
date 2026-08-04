#!/usr/bin/env python3
"""Experimental sky masking and perspective normalization for map previews."""

from __future__ import annotations

import argparse
from pathlib import Path


def order_quad(points):
    import numpy as np

    points = np.asarray(points, dtype=np.float32)
    sums = points.sum(axis=1)
    differences = points[:, 0] - points[:, 1]
    return np.asarray(
        [
            points[sums.argmin()],
            points[differences.argmax()],
            points[sums.argmax()],
            points[differences.argmin()],
        ],
        dtype=np.float32,
    )


def detect_map(image):
    import cv2
    import numpy as np

    rgb = np.asarray(image.convert("RGB"))
    bgr = cv2.cvtColor(rgb, cv2.COLOR_RGB2BGR)
    height, width = bgr.shape[:2]
    flood_mask = np.zeros((height + 2, width + 2), np.uint8)
    edge_barrier = cv2.dilate(
        cv2.Canny(bgr, 40, 100),
        cv2.getStructuringElement(cv2.MORPH_RECT, (3, 3)),
    )
    flood_mask[1:-1, 1:-1][edge_barrier > 0] = 1
    flood_flags = 4 | cv2.FLOODFILL_MASK_ONLY | (255 << 8)
    gray = cv2.cvtColor(bgr, cv2.COLOR_BGR2GRAY)
    gradient = cv2.magnitude(
        cv2.Sobel(gray, cv2.CV_32F, 1, 0),
        cv2.Sobel(gray, cv2.CV_32F, 0, 1),
    )
    patch_width = max(8, width // 20)
    patch_height = max(8, height // 20)
    corner_candidates = (
        ((0, 0), gradient[:patch_height, :patch_width]),
        ((width - 1, 0), gradient[:patch_height, -patch_width:]),
        ((0, height - 1), gradient[-patch_height:, :patch_width]),
        ((width - 1, height - 1), gradient[-patch_height:, -patch_width:]),
    )
    seeds = [
        seed
        for seed, _ in sorted(
            corner_candidates, key=lambda candidate: float(candidate[1].mean())
        )[:2]
    ]
    for seed in seeds:
        cv2.floodFill(
            bgr,
            flood_mask,
            seed,
            (0, 0, 0),
            (10, 10, 10),
            (10, 10, 10),
            flood_flags,
        )
    background = flood_mask[1:-1, 1:-1] == 255
    foreground = np.where(background, 0, 255).astype("uint8")
    kernel_size = max(3, min(width, height) // 80) | 1
    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (kernel_size, kernel_size))
    foreground = cv2.morphologyEx(foreground, cv2.MORPH_CLOSE, kernel)
    contours, _ = cv2.findContours(foreground, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    if not contours:
        return None
    contour = max(contours, key=cv2.contourArea)
    area_ratio = cv2.contourArea(contour) / (width * height)
    if area_ratio < 0.15:
        return None
    component = np.zeros_like(foreground)
    cv2.drawContours(component, [contour], -1, 255, thickness=cv2.FILLED)
    hull = cv2.convexHull(contour).reshape(-1, 2)
    quad = order_quad(hull)
    x, y, box_width, box_height = cv2.boundingRect(contour)
    border_pixels = np.concatenate(
        (component[0, :], component[-1, :], component[:, 0], component[:, -1])
    )
    border_leak = float((border_pixels > 0).mean())
    confidence = min(1.0, area_ratio / 0.5) * max(0.0, 1.0 - border_leak * 2)
    return {
        "mask": component,
        "quad": quad,
        "bounds": (x, y, box_width, box_height),
        "area_ratio": area_ratio,
        "border_leak": border_leak,
        "confidence": confidence,
    }


def mask_background(image, background=(96, 96, 96)):
    from PIL import Image
    import numpy as np

    detection = detect_map(image)
    if detection is None:
        return image.copy(), {"applied": False, "reason": "map_not_detected"}
    rgb = np.asarray(image.convert("RGB")).copy()
    rgb[detection["mask"] == 0] = background
    return Image.fromarray(rgb), {
        "applied": True,
        "bounds": detection["bounds"],
        "area_ratio": detection["area_ratio"],
        "border_leak": detection["border_leak"],
        "confidence": detection["confidence"],
    }


def mask_and_crop(image, background=(96, 96, 96)):
    normalized, info = mask_background(image, background)
    if not info["applied"]:
        return normalized, info
    x, y, width, height = info["bounds"]
    normalized = normalized.crop((x, y, x + width, y + height))
    return normalized, info


def perspective_rectify(image, background=(96, 96, 96)):
    from PIL import Image
    import cv2
    import numpy as np

    detection = detect_map(image)
    if detection is None or detection["confidence"] < 0.45:
        return image.copy(), {"applied": False, "reason": "low_confidence"}
    rgb = np.asarray(image.convert("RGB")).copy()
    rgb[detection["mask"] == 0] = background
    quad = detection["quad"]
    top_left, top_right, bottom_right, bottom_left = quad
    output_width = max(
        int(np.linalg.norm(top_right - top_left)),
        int(np.linalg.norm(bottom_right - bottom_left)),
    )
    output_height = max(
        int(np.linalg.norm(bottom_left - top_left)),
        int(np.linalg.norm(bottom_right - top_right)),
    )
    if output_width < 64 or output_height < 64:
        return image.copy(), {"applied": False, "reason": "small_quad"}
    target = np.asarray(
        [[0, 0], [output_width - 1, 0], [output_width - 1, output_height - 1], [0, output_height - 1]],
        dtype=np.float32,
    )
    transform = cv2.getPerspectiveTransform(quad, target)
    warped = cv2.warpPerspective(
        cv2.cvtColor(rgb, cv2.COLOR_RGB2BGR),
        transform,
        (output_width, output_height),
        borderValue=background[::-1],
    )
    return Image.fromarray(cv2.cvtColor(warped, cv2.COLOR_BGR2RGB)), {
        "applied": True,
        "area_ratio": detection["area_ratio"],
        "border_leak": detection["border_leak"],
        "confidence": detection["confidence"],
    }


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument("images", nargs="+")
    parser.add_argument("--output-directory", required=True)
    return parser.parse_args()


def main() -> None:
    from PIL import Image

    args = parse_args()
    output_directory = Path(args.output_directory)
    output_directory.mkdir(parents=True, exist_ok=True)
    for source_name in args.images:
        source = Path(source_name)
        with Image.open(source) as opened:
            image = opened.convert("RGB")
        masked, mask_info = mask_and_crop(image)
        rectified, rectify_info = perspective_rectify(image)
        masked.save(output_directory / (source.stem + "-masked.jpg"), quality=92)
        rectified.save(output_directory / (source.stem + "-rectified.jpg"), quality=92)
        print(source.name, "mask", mask_info, "rectify", rectify_info)


if __name__ == "__main__":
    main()
