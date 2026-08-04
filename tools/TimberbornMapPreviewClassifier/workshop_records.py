"""Shared interpretation of trustworthy Steam Workshop record fields."""


def is_map_item(record: dict) -> bool:
    """Return whether the item's Steam tag list contains Map."""
    return any(
        isinstance(tag, str) and tag.casefold() == "map"
        for tag in record.get("tags", [])
    )
