from argparse import ArgumentParser
from pathlib import Path

from PIL import Image


OUTPUT_SIZE = (112, 112)
BASELINE = 100
HORIZONTAL_OFFSET = 1
PLATFORM_WIDTH = 92
TERRAIN_BLOCK_WIDTH = 88
DEFAULT_OUTPUT = (
    Path(__file__).parent.parent
    / "Mod"
    / "Buildings"
    / "Landscaping"
    / "FillTheGap"
    / "FillTheGapIcon.png"
)


def main() -> None:
    parser = ArgumentParser(description="Generate the Fill the Gap building icon.")
    parser.add_argument(
        "terrain_block_icon",
        type=Path,
        help="Extracted Buildings/Landscaping/TerrainBlock/TerrainBlockIcon PNG.",
    )
    parser.add_argument(
        "platform_icon",
        type=Path,
        help="Extracted Buildings/Paths/Platform/PlatformIcon PNG.",
    )
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    args = parser.parse_args()

    terrain_block = load_main_component(args.terrain_block_icon)
    platform = load_main_component(args.platform_icon)

    output = Image.new("RGBA", OUTPUT_SIZE)
    composite_at_baseline(output, terrain_block, TERRAIN_BLOCK_WIDTH)
    composite_at_baseline(output, platform, PLATFORM_WIDTH)

    args.output.parent.mkdir(parents=True, exist_ok=True)
    output.save(args.output)


def load_main_component(path: Path) -> Image.Image:
    image = Image.open(path).convert("RGBA")
    if image.size != OUTPUT_SIZE:
        raise ValueError(f"Expected a 112x112 source icon, got {image.size}: {path}")

    alpha = image.getchannel("A")
    components = find_components(alpha)
    if not components:
        raise ValueError(f"Source icon has no visible pixels: {path}")

    main_component = max(components, key=len)
    isolated = Image.new("RGBA", image.size)
    source_pixels = image.load()
    isolated_pixels = isolated.load()
    for coordinates in main_component:
        isolated_pixels[coordinates] = source_pixels[coordinates]

    return isolated.crop(isolated.getchannel("A").getbbox())


def find_components(alpha: Image.Image) -> list[list[tuple[int, int]]]:
    alpha_pixels = alpha.load()
    visited = set()
    components = []

    for y in range(alpha.height):
        for x in range(alpha.width):
            if alpha_pixels[x, y] == 0 or (x, y) in visited:
                continue

            component = []
            pending = [(x, y)]
            visited.add((x, y))
            while pending:
                coordinates = pending.pop()
                component.append(coordinates)
                for neighbor in neighbors(coordinates, alpha.size):
                    if alpha_pixels[neighbor] > 0 and neighbor not in visited:
                        visited.add(neighbor)
                        pending.append(neighbor)
            components.append(component)

    return components


def neighbors(coordinates: tuple[int, int], size: tuple[int, int]):
    x, y = coordinates
    width, height = size
    for neighbor_x, neighbor_y in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
        if 0 <= neighbor_x < width and 0 <= neighbor_y < height:
            yield neighbor_x, neighbor_y


def composite_at_baseline(output: Image.Image, source: Image.Image, width: int) -> None:
    height = round(width * source.height / source.width)
    resized = source.resize((width, height), Image.Resampling.LANCZOS)
    position = ((output.width - width) // 2 + HORIZONTAL_OFFSET, BASELINE - height)
    output.alpha_composite(resized, position)


if __name__ == "__main__":
    main()
