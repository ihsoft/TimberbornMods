import unittest

import create_forest_calibration_set as calibration
import evaluate_forest_calibration as evaluation
import create_water_calibration_set as water_calibration
import evaluate_water_calibration as water_evaluation
import normalize_map_preview as normalization


def candidate(published_file_id: int, percentile: float) -> dict:
    return {
        "published_file_id": str(published_file_id),
        "title": f"Map {published_file_id}",
        "preview_url": f"https://example/{published_file_id}",
        "primary_category": "map",
        "visual_scores": {"forest_density": percentile - 0.5},
        "visual_percentiles": {"forest_density": percentile},
    }


class CalibrationSelectionTest(unittest.TestCase):
    def test_forest_scale_excludes_dead_trees_and_uses_practical_maximum(self) -> None:
        captions = dict(calibration.LEVELS)

        self.assertEqual("No living green trees", captions["none"])
        self.assertIn("density", captions["sparse"])
        self.assertIn("practical", captions["dense"])

    def test_selects_requested_count_across_score_range_and_keeps_required_map(self) -> None:
        candidates = [candidate(index, index / 100) for index in range(100)]

        selected = calibration.select_candidates(candidates, 50, ["4"])

        self.assertEqual(50, len(selected))
        self.assertIn("4", {record["published_file_id"] for record in selected})
        represented_bins = {
            min(int(record["visual_percentiles"]["forest_density"] * 10), 9)
            for record in selected
        }
        self.assertEqual(set(range(10)), represented_bins)

    def test_selection_is_deterministic(self) -> None:
        candidates = [candidate(index, index / 100) for index in range(100)]

        first = calibration.select_candidates(candidates, 20, ["4"])
        second = calibration.select_candidates(list(reversed(candidates)), 20, ["4"])

        self.assertEqual(
            [record["published_file_id"] for record in first],
            [record["published_file_id"] for record in second],
        )

    def test_missing_required_map_is_reported(self) -> None:
        with self.assertRaisesRegex(ValueError, "999"):
            calibration.select_candidates([candidate(1, 0.1)], 1, ["999"])

    def test_water_calibration_uses_an_independent_feature_and_storage_key(self) -> None:
        entries = [{
            "published_file_id": "1",
            "title": "Map",
            "image_path": "images/1.jpg",
            "water_dominance_score": 0.1,
            "water_dominance_percentile": 0.2,
        }]

        page = calibration.render_html(
            entries,
            feature=water_calibration.WATER_FEATURE,
            levels=water_calibration.LEVELS,
            rubric=water_calibration.RUBRIC,
            storage_key="timberborn-water-calibration-v1",
            export_filename="water-calibration-labels.json",
            field_prefix="water",
        )

        self.assertIn("timberborn-water-calibration-v1", page)
        self.assertIn("water-calibration-labels.json", page)
        self.assertIn("blue water areas", page)


class CalibrationEvaluationTest(unittest.TestCase):
    def test_quad_points_are_ordered_clockwise_from_top_left(self) -> None:
        points = [(90, 80), (10, 20), (80, 10), (20, 90)]

        ordered = normalization.order_quad(points).tolist()

        self.assertEqual([[10.0, 20.0], [80.0, 10.0], [90.0, 80.0], [20.0, 90.0]], ordered)

    def test_water_levels_are_ordered_from_very_low_to_very_high(self) -> None:
        self.assertEqual(
            [0, 1, 2, 3, 4],
            [water_evaluation.LEVEL_VALUES[level] for level in ("very_low", "low", "moderate", "high", "very_high")],
        )

    def test_rank_uses_average_for_ties(self) -> None:
        self.assertEqual([0.0, 1.5, 1.5, 3.0], evaluation.rank([1, 2, 2, 3]))

    def test_ordered_pair_accuracy_detects_forward_and_reverse_scores(self) -> None:
        labels = [0, 1, 2, 3, 4]

        self.assertEqual(1.0, evaluation.ordered_pair_accuracy(labels, [0, 1, 2, 3, 4]))
        self.assertEqual(0.0, evaluation.ordered_pair_accuracy(labels, [4, 3, 2, 1, 0]))

    def test_fitted_thresholds_separate_ordered_levels(self) -> None:
        labels = [0, 0, 1, 1, 2, 2, 3, 3, 4, 4]
        scores = [value / 10 for value in range(10)]

        thresholds = evaluation.fit_thresholds(labels, scores)

        self.assertEqual(labels, evaluation.predict_levels(scores, thresholds))

    def test_grid_crops_cover_odd_image_without_gaps(self) -> None:
        class FakeImage:
            size = (10, 7)

            @staticmethod
            def crop(box):
                return box

        self.assertEqual(
            (6, 4, 10, 7),
            evaluation.make_grid_crops(FakeImage(), 3)["grid_3_2_2"],
        )


if __name__ == "__main__":
    unittest.main()
