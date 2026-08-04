#!/usr/bin/env python3
"""Focused standard-library tests for incremental gallery classification."""

import unittest
from datetime import datetime, timezone
import gzip
import json
from pathlib import Path
import tempfile
from unittest import mock
from urllib.error import HTTPError
import time

import classify
import build_public_index
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

    def test_does_not_retry_after_steam_rate_limit(self) -> None:
        error = HTTPError("https://example", 429, "rate limited", {}, None)
        with mock.patch.object(collect_gallery, "urlopen", side_effect=error) as request:
            with self.assertRaises(collect_gallery.SteamThrottleError):
                collect_gallery.fetch_gallery_urls("1", 0, time.monotonic() + 5)

        self.assertEqual(1, request.call_count)

    def test_doubles_cooldown_then_stops_on_third_rate_limit(self) -> None:
        error = collect_gallery.SteamThrottleError(429, None)

        first = collect_gallery.throttle_policy(error, 1, 20)
        second = collect_gallery.throttle_policy(error, 2, first[2])
        third = collect_gallery.throttle_policy(error, 3, second[2])

        self.assertEqual((False, 60, 40), first)
        self.assertEqual((False, 120, 80), second)
        self.assertEqual((True, 0, 80), third)


class MultiImageClassificationTest(unittest.TestCase):
    def test_forest_density_uses_four_equal_area_quadrants(self) -> None:
        self.assertEqual(
            (
                (0, 0, 50, 30),
                (50, 0, 101, 30),
                (0, 30, 50, 61),
                (50, 30, 101, 61),
            ),
            classify.quadrant_boxes(101, 61),
        )

    def test_water_density_uses_nine_equal_area_regions(self) -> None:
        boxes = classify.grid_boxes(101, 61, 3)

        self.assertEqual(9, len(boxes))
        self.assertEqual((67, 40, 101, 61), boxes[-1])

    def test_moist_soil_has_less_weight_than_free_water(self) -> None:
        self.assertGreater(classify.MOIST_SOIL_WEIGHT, 0)
        self.assertLess(classify.MOIST_SOIL_WEIGHT, 1)

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
            "classifier_version": classify.CLASSIFIER_VERSION,
        }]

        reusable, to_classify, previous_by_id, _ = classify.plan_incremental(
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
        classify.add_levels_and_labels([result])

        self.assertEqual(2, result["visual_image_count"])
        self.assertEqual(1, result["visual_gallery_image_count"])
        self.assertEqual(0.5, result["visual_scores"]["ruggedness"])
        self.assertEqual(1.0, result["visual_score_aggregates"]["ruggedness"]["spread"])
        self.assertEqual(4, result["visual_levels"]["ruggedness"])

    def test_absolute_levels_do_not_depend_on_other_maps(self) -> None:
        def result_with_score(score: float) -> dict:
            return {
                "visual_scores": {
                    feature: score for feature in classify.FEATURE_PROMPTS
                }
            }

        target = result_with_score(-0.029)
        classify.add_levels_and_labels([target])
        target_levels = dict(target["visual_levels"])

        corpus = [result_with_score(value) for value in (-1.0, -0.5, 0.5, 1.0)]
        corpus.append(target)
        classify.add_levels_and_labels(corpus)

        self.assertEqual(target_levels, target["visual_levels"])
        self.assertEqual(3, target["visual_levels"]["water_dominance"])

    def test_reclassifies_older_scores_after_spatial_feature_change(self) -> None:
        zero_scores = {feature: 0.0 for feature in classify.FEATURE_PROMPTS}
        maps = [{
            "published_file_id": "1",
            "title": "Map",
            "preview_url": "primary",
            "images": [{"url": "primary", "role": "primary", "path": None}],
            "gallery_collection_state": "fetched",
        }]
        previous = [{
            "published_file_id": "1",
            "preview_url": "primary",
            "visual_scores": zero_scores,
            "model": "openai/clip-vit-base-patch32",
            "classifier_version": "clip-prompts-v3-green-tree-quadrants",
        }]

        reusable, to_classify, _, _ = classify.plan_incremental(
            maps, previous, "openai/clip-vit-base-patch32"
        )

        self.assertEqual({}, reusable["1"])
        self.assertEqual(["primary"], [image["image_url"] for image in to_classify])

    def test_partial_migration_carries_previous_record_without_changing_version(self) -> None:
        zero_scores = {feature: 0.0 for feature in classify.FEATURE_PROMPTS}
        previous = {
            "published_file_id": "1",
            "title": "Map",
            "preview_url": "primary",
            "visual_scores": zero_scores,
            "visual_percentiles": zero_scores,
            "visual_labels": [],
            "model": "openai/clip-vit-base-patch32",
            "classifier_version": "clip-prompts-v2-multi-image",
        }
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

        results = classify.aggregate_map_results(
            maps, {"1": {}}, [], {}, {"1": previous}
        )

        self.assertEqual("clip-prompts-v2-multi-image", results[0]["classifier_version"])
        self.assertEqual("migration_pending", results[0]["classification_state"])


class ResumableMigrationIndexTest(unittest.TestCase):
    def test_mixed_snapshot_is_published_with_explicit_migration_progress(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            snapshot = root / "items.jsonl"
            visual = root / "visual.jsonl"
            gallery = root / "gallery.jsonl"
            map_metadata = root / "map-metadata.jsonl"
            output = root / "public"
            items = [
                {"published_file_id": "1", "title": "One", "tags": ["Map"], "primary_category": "map"},
                {"published_file_id": "2", "title": "Two", "tags": ["Map"], "primary_category": "map"},
            ]
            scores = {feature: 0.0 for feature in classify.FEATURE_PROMPTS}
            records = []
            for published_file_id, version in (
                ("1", classify.CLASSIFIER_VERSION),
                ("2", "clip-prompts-v2-multi-image"),
            ):
                records.append({
                    "published_file_id": published_file_id,
                    "visual_scores": scores,
                    "visual_labels": [],
                    "visual_image_count": 1,
                    "visual_missing_image_count": 0,
                    "model": "openai/clip-vit-base-patch32",
                    "classifier_version": version,
                })
                if version == classify.CLASSIFIER_VERSION:
                    records[-1]["visual_levels"] = {
                        feature: 2 for feature in classify.FEATURE_PROMPTS
                    }
                else:
                    records[-1]["visual_percentiles"] = scores
            dimensions = [{
                "published_file_id": "1",
                "map_width": 128,
                "map_height": 64,
                "collection_state": "fetched",
            }]
            for path, values in (
                (snapshot, items), (visual, records), (gallery, []), (map_metadata, dimensions)
            ):
                path.write_text("".join(json.dumps(value) + "\n" for value in values), encoding="utf-8")

            with mock.patch(
                "sys.argv",
                [
                    "build_public_index.py",
                    "--snapshot", str(snapshot),
                    "--visual-features", str(visual),
                    "--gallery-results", str(gallery),
                    "--map-metadata", str(map_metadata),
                    "--target-classifier-version", classify.CLASSIFIER_VERSION,
                    "--output-directory", str(output),
                ],
            ):
                self.assertEqual(0, build_public_index.main())

            manifest = json.loads((output / "manifest.json").read_text(encoding="utf-8"))
            self.assertEqual("mixed", manifest["visual_classifier_version"])
            self.assertFalse(manifest["visual_migration_complete"])
            self.assertEqual(1, manifest["visual_migration_maps_completed"])
            self.assertEqual(1, manifest["visual_migration_maps_remaining"])
            self.assertEqual(1, manifest["map_dimensions_known"])
            with gzip.open(output / "search-index.jsonl.gz", "rt", encoding="utf-8") as stream:
                search_records = [json.loads(line) for line in stream]
            self.assertEqual((128, 64), (search_records[0]["map_width"], search_records[0]["map_height"]))
            with gzip.open(output / "map-visual-features.jsonl.gz", "rt", encoding="utf-8") as stream:
                published = [json.loads(line) for line in stream]
            self.assertEqual(
                [classify.CLASSIFIER_VERSION, "clip-prompts-v2-multi-image"],
                [record["classifier_version"] for record in published],
            )


class ImageDownloadSafetyTest(unittest.TestCase):
    def test_does_not_retry_after_image_stop_response(self) -> None:
        for status_code in (403, 429, 503):
            with self.subTest(status_code=status_code):
                error = HTTPError("https://example", status_code, "stop", {}, None)
                item = {"image_url": "https://example"}
                with mock.patch.object(classify, "urlopen", side_effect=error) as request:
                    with self.assertRaises(classify.SteamImageRequestStopError):
                        classify.load_image(item, True, 2_000_000)

                self.assertEqual(1, request.call_count)

    def test_suppresses_queued_downloads_after_image_rate_limit(self) -> None:
        error = HTTPError("https://example", 429, "rate limited", {}, None)
        item = {"image_url": "https://example"}
        stop_downloads = classify.Event()
        with mock.patch.object(classify, "urlopen", side_effect=error) as request:
            _, exception = classify.try_load_image(
                item, True, 2_000_000, stop_downloads)
            skipped = classify.try_load_image(
                item, True, 2_000_000, stop_downloads)

        self.assertIsInstance(exception, classify.SteamImageRequestStopError)
        self.assertEqual((None, None), skipped)
        self.assertEqual(1, request.call_count)


if __name__ == "__main__":
    unittest.main()
