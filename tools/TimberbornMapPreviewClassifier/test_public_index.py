#!/usr/bin/env python3
"""Focused standard-library tests for the published Workshop data contract."""

import gzip
import json
from pathlib import Path
import sys
from tempfile import TemporaryDirectory
import unittest
from unittest import mock

import build_public_index


CANONICAL_DATA_FILES = {
    "workshop-items.jsonl.gz",
    "map-gallery.jsonl.gz",
    "map-visual-features.jsonl.gz",
    "search-index.jsonl.gz",
}


def write_json_lines(path: Path, records: list[dict]) -> None:
    path.write_text(
        "".join(json.dumps(record) + "\n" for record in records),
        encoding="utf-8",
    )


class PublicIndexContractTest(unittest.TestCase):
    def test_generates_versioned_manifest_and_canonical_consumer_index(self) -> None:
        with TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            snapshot = root / "snapshot.jsonl"
            visual_features = root / "visual-features.jsonl"
            gallery_results = root / "gallery-results.jsonl"
            output = root / "public"
            write_json_lines(snapshot, [{
                "published_file_id": "1",
                "title": "Test Map",
                "primary_category": "map",
                "preview_url": "https://example.test/primary.jpg",
            }])
            write_json_lines(visual_features, [{
                "published_file_id": "1",
                "visual_scores": {"ruggedness": 0.75},
                "visual_percentiles": {"ruggedness": 0.9},
                "visual_labels": ["predominantly_mountainous"],
                "visual_image_count": 2,
                "model": "test-model",
                "classifier_version": "test-classifier-v1",
            }])
            write_json_lines(gallery_results, [{
                "published_file_id": "1",
                "gallery_urls": ["https://example.test/gallery.jpg"],
                "collection_state": "reused",
            }])
            arguments = [
                "build_public_index.py",
                "--snapshot", str(snapshot),
                "--visual-features", str(visual_features),
                "--gallery-results", str(gallery_results),
                "--output-directory", str(output),
            ]

            with mock.patch.object(sys, "argv", arguments):
                self.assertEqual(0, build_public_index.main())

            manifest = json.loads((output / "manifest.json").read_text(encoding="utf-8"))
            self.assertEqual(1, manifest["schema_version"])
            self.assertIsInstance(manifest["schema_version"], int)
            self.assertEqual("test-classifier-v1", manifest["visual_classifier_version"])
            self.assertEqual(CANONICAL_DATA_FILES, set(manifest["files"]))

            with gzip.open(output / "search-index.jsonl.gz", "rt", encoding="utf-8") as stream:
                consumer_records = [json.loads(line) for line in stream]

            self.assertEqual(1, len(consumer_records))
            self.assertEqual("1", consumer_records[0]["published_file_id"])
            self.assertEqual(
                ["https://example.test/gallery.jpg"],
                consumer_records[0]["gallery_urls"],
            )
            self.assertEqual(
                "test-classifier-v1",
                consumer_records[0]["visual_classifier_version"],
            )


if __name__ == "__main__":
    unittest.main()
