import argparse
from pathlib import Path

from PIL import Image


ICON_SIZE = (112, 112)
STAR_SIZE = (35, 35)
STAR_POSITION = (77, 0)


def parse_arguments():
    parser = argparse.ArgumentParser(
        description="Overlay one stock district symbol on unchanged stock building icons."
    )
    parser.add_argument("--warehouse-icon", required=True, type=Path)
    parser.add_argument("--tank-icon", required=True, type=Path)
    parser.add_argument("--district-center-icon", required=True, type=Path)
    parser.add_argument("--warehouse-output", required=True, type=Path)
    parser.add_argument("--tank-output", required=True, type=Path)
    return parser.parse_args()


def load_icon(path):
    icon = Image.open(path).convert("RGBA")
    if icon.size != ICON_SIZE:
        raise RuntimeError(f"Expected {path} to be {ICON_SIZE}, found {icon.size}.")
    return icon


def create_overlay(district_center_icon):
    bounds = district_center_icon.getchannel("A").getbbox()
    if bounds is None:
        raise RuntimeError("District Center icon has no visible pixels.")

    star = district_center_icon.crop(bounds).resize(STAR_SIZE, Image.Resampling.LANCZOS)
    overlay = Image.new("RGBA", ICON_SIZE)
    overlay.alpha_composite(star, STAR_POSITION)
    return overlay


def compose(source, overlay, output):
    overlay_alpha = overlay.getchannel("A")
    result = source.copy()
    result.alpha_composite(overlay)
    output.parent.mkdir(parents=True, exist_ok=True)
    result.save(output)

    for source_pixel, result_pixel, overlay_value in zip(
        source.get_flattened_data(), result.get_flattened_data(), overlay_alpha.get_flattened_data()
    ):
        if overlay_value == 0 and source_pixel != result_pixel:
            raise RuntimeError(f"Stock pixels changed outside the overlay in {output}.")


def main():
    arguments = parse_arguments()
    warehouse = load_icon(arguments.warehouse_icon)
    tank = load_icon(arguments.tank_icon)
    district_center = load_icon(arguments.district_center_icon)
    overlay = create_overlay(district_center)
    compose(warehouse, overlay, arguments.warehouse_output)
    compose(tank, overlay, arguments.tank_output)
    print(f"Created {arguments.warehouse_output} and {arguments.tank_output} at {ICON_SIZE}.")


if __name__ == "__main__":
    main()
