import unittest

from workshop_records import is_map_item


class WorkshopRecordTest(unittest.TestCase):
    def test_map_tag_is_the_only_map_criterion(self) -> None:
        self.assertTrue(is_map_item({
            "tags": ["Mod", "Map", "Update 1.0"],
            "primary_category": "other",
        }))
        self.assertTrue(is_map_item({"tags": ["map"]}))
        self.assertFalse(is_map_item({"tags": ["Maps"], "primary_category": "map"}))
        self.assertFalse(is_map_item({
            "title": "Map with terrain",
            "description_plain": "A challenge map with a starting location.",
            "primary_category": "map",
        }))


if __name__ == "__main__":
    unittest.main()
