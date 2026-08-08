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
    "map-metadata.jsonl.gz",
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
            map_metadata = root / "map-metadata.jsonl"
            output = root / "public"
            write_json_lines(snapshot, [{
                "published_file_id": "1",
                "title": "Test Map",
                "tags": ["Map"],
                "primary_category": "map",
                "preview_url": "https://example.test/primary.jpg",
                "payload_size_bytes": 12345,
            }])
            write_json_lines(map_metadata, [{
                "published_file_id": "1",
                "analysis_version": 1,
                "map_width": 128,
                "map_height": 128,
                "classifications": {
                    "forest_density": {
                        "live_tree_count": 4096,
                        "coverage_ratio": 0.25,
                        "level": 2,
                    },
                    "water": {
                        "open_water_tiles": 8192,
                        "open_water_ratio": 0.5,
                        "lake_count": 1,
                        "water_form": "lakes",
                    },
                    "settlement_space": {
                        "core_count": 12,
                        "plain_share": 0.1,
                        "terrace_share": 0.7,
                        "plateau_share": 0.1,
                        "mixed_share": 0.1,
                        "space_type": "terraces",
                    },
                    "islands": [1024, 256],
                },
                "collection_state": "fetched",
            }])
            arguments = [
                "build_public_index.py",
                "--snapshot", str(snapshot),
                "--map-metadata", str(map_metadata),
                "--output-directory", str(output),
            ]

            with mock.patch.object(sys, "argv", arguments):
                self.assertEqual(0, build_public_index.main())

            manifest = json.loads((output / "manifest.json").read_text(encoding="utf-8"))
            self.assertEqual(2, manifest["schema_version"])
            self.assertIsInstance(manifest["schema_version"], int)
            self.assertEqual(CANONICAL_DATA_FILES, set(manifest["files"]))
            self.assertEqual(1, manifest["map_settlement_space_known"])
            self.assertEqual(1, manifest["map_islands_known"])

            with gzip.open(output / "workshop-items.jsonl.gz", "rt", encoding="utf-8") as stream:
                public_workshop_records = [json.loads(line) for line in stream]
            self.assertNotIn("payload_size_bytes", public_workshop_records[0])

            with gzip.open(output / "search-index.jsonl.gz", "rt", encoding="utf-8") as stream:
                consumer_records = [json.loads(line) for line in stream]

            self.assertEqual(1, len(consumer_records))
            self.assertEqual("1", consumer_records[0]["published_file_id"])
            self.assertNotIn("payload_size_bytes", consumer_records[0])
            self.assertEqual(128, consumer_records[0]["map_width"])
            self.assertEqual(128, consumer_records[0]["map_height"])
            self.assertEqual(1, consumer_records[0]["map_analysis_version"])
            self.assertEqual(
                2,
                consumer_records[0]["map_classifications"]["forest_density"]["level"],
            )
            self.assertEqual(
                "lakes",
                consumer_records[0]["map_classifications"]["water"]["water_form"],
            )
            self.assertEqual(
                "terraces",
                consumer_records[0]["map_classifications"]["settlement_space"]["space_type"],
            )
            self.assertEqual(
                [1024, 256],
                consumer_records[0]["map_classifications"]["islands"],
            )

    def test_publishes_unsupported_state_without_fake_dimensions(self) -> None:
        with TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            snapshot = root / "snapshot.jsonl"
            map_metadata = root / "map-metadata.jsonl"
            output = root / "public"
            write_json_lines(snapshot, [{"published_file_id": "2", "tags": ["Map"]}])
            write_json_lines(map_metadata, [{
                "published_file_id": "2",
                "analysis_version": 2,
                "map_width": 0,
                "map_height": 0,
                "classifications": None,
                "collection_state": "unsupported",
                "analysis_error": "unknown terrain format",
            }])

            with mock.patch.object(sys, "argv", [
                "build_public_index.py",
                "--snapshot", str(snapshot),
                "--map-metadata", str(map_metadata),
                "--output-directory", str(output),
            ]):
                self.assertEqual(0, build_public_index.main())

            manifest = json.loads((output / "manifest.json").read_text(encoding="utf-8"))
            self.assertEqual(0, manifest["map_dimensions_known"])
            self.assertEqual(1, manifest["map_metadata_unsupported"])
            with gzip.open(output / "search-index.jsonl.gz", "rt", encoding="utf-8") as stream:
                record = json.loads(next(stream))
            self.assertEqual("unsupported", record["map_metadata_collection_state"])
            self.assertEqual("unknown terrain format", record["map_analysis_error"])
            self.assertNotIn("map_width", record)
            self.assertNotIn("map_classifications", record)


if __name__ == "__main__":
    unittest.main()
