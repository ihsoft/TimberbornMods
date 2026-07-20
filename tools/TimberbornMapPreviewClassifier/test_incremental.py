#!/usr/bin/env python3
"""Focused standard-library tests for incremental gallery classification."""

import unittest
from datetime import datetime, timezone

import classify
import collect_gallery


class GalleryParsingTest(unittest.TestCase):
    def test_extracts_and_normalizes_gallery_urls(self) -> None:
        page = """
            var rgScreenshotURLs = {
              '1': 'https://images.steamusercontent.com/ugc/a/b/?old=1',
              '2': 'https://images.steamusercontent.com/ugc/c/d/'
            };
        """

        urls = collect_gallery.parse_gallery_urls(page)

        self.assertEqual(2, len(urls))
        self.assertTrue(all("imw=637" in url for url in urls))

    def test_retries_deferred_gallery_even_when_timestamp_is_unchanged(self) -> None:
        item = {"updated_at_utc": "2026-07-20T00:00:00Z"}
        previous = {
            "source_updated_at_utc": "2026-07-20T00:00:00Z",
            "gallery_checked_at_utc": "2026-07-20T00:00:00Z",
            "collection_state": "deferred",
        }

        self.assertTrue(
            collect_gallery.needs_refresh(
                item, previous, datetime.min.replace(tzinfo=timezone.utc)
            )
        )


class MultiImageClassificationTest(unittest.TestCase):
    def test_upgrades_legacy_primary_and_aggregates_gallery(self) -> None:
        zero_scores = {feature: 0.0 for feature in classify.FEATURE_PROMPTS}
        maps = [{
            "published_file_id": "1",
            "title": "Map",
            "preview_url": "primary",
            "images": [
                {"url": "primary", "role": "primary", "path": None},
                {"url": "gallery", "role": "gallery", "path": None},
            ],
            "gallery_collection_state": "fetched",
        }]
        previous = [{
            "published_file_id": "1",
            "preview_url": "primary",
            "visual_scores": zero_scores,
            "model": "openai/clip-vit-base-patch32",
            "classifier_version": "clip-prompts-v1",
        }]

        reusable, to_classify, previous_by_id = classify.plan_incremental(
            maps, previous, "openai/clip-vit-base-patch32"
        )

        self.assertEqual(["gallery"], [image["image_url"] for image in to_classify])
        one_scores = {feature: 1.0 for feature in classify.FEATURE_PROMPTS}
        classified = [{
            "published_file_id": "1",
            "url": "gallery",
            "role": "gallery",
            "scores": one_scores,
        }]
        result = classify.aggregate_map_results(
            maps, reusable, classified, previous_by_id
        )[0]
        classify.add_percentiles_and_labels([result])

        self.assertEqual(2, result["visual_image_count"])
        self.assertEqual(1, result["visual_gallery_image_count"])
        self.assertEqual(0.5, result["visual_scores"]["ruggedness"])
        self.assertEqual(1.0, result["visual_score_aggregates"]["ruggedness"]["spread"])
        self.assertEqual(
            0.5, result["visual_percentile_aggregates"]["ruggedness"]["max"]
        )


if __name__ == "__main__":
    unittest.main()
