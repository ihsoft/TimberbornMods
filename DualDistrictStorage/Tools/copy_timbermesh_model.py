import argparse
import hashlib
import sys
import zlib
from pathlib import Path


def parse_arguments():
    parser = argparse.ArgumentParser(
        description="Copy a generated Timbermesh under a distinct model identity."
    )
    parser.add_argument("--timbermesh-plugin-dir", required=True, type=Path)
    parser.add_argument("--source", required=True, type=Path)
    parser.add_argument("--source-model", required=True)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--output-model", required=True)
    parser.add_argument(
        "--material", action="append", default=[], metavar="SOURCE=OUTPUT",
        help="Map one source material identity to its output identity. Every source material must be mapped.",
    )
    return parser.parse_args()


def parse_material_map(material_arguments):
    material_map = {}
    for material_argument in material_arguments:
        source, separator, output = material_argument.partition("=")
        if not separator or not source or not output:
            raise RuntimeError(f"Invalid material mapping: {material_argument}")
        if source in material_map:
            raise RuntimeError(f"Duplicate source material mapping: {source}")
        material_map[source] = output
    return material_map


def main():
    arguments = parse_arguments()
    sys.path.insert(0, str(arguments.timbermesh_plugin_dir.resolve()))

    import model_pb2

    source_bytes = arguments.source.read_bytes()
    model = model_pb2.Model()
    model.ParseFromString(zlib.decompress(source_bytes))
    if len(model.nodes) != 1:
        raise RuntimeError(f"Expected one model node, found {len(model.nodes)}.")
    if model.nodes[0].name != arguments.source_model:
        raise RuntimeError(
            f"Expected source model {arguments.source_model}, found {model.nodes[0].name}."
        )

    material_map = parse_material_map(arguments.material)
    source_materials = {mesh.material for mesh in model.nodes[0].meshes}
    missing_materials = source_materials - material_map.keys()
    unknown_materials = material_map.keys() - source_materials
    if missing_materials:
        raise RuntimeError(f"Missing material mappings: {', '.join(sorted(missing_materials))}")
    if unknown_materials:
        raise RuntimeError(f"Unknown source materials: {', '.join(sorted(unknown_materials))}")

    model.nodes[0].name = arguments.output_model
    for mesh in model.nodes[0].meshes:
        mesh.material = material_map[mesh.material]
    output_bytes = zlib.compress(model.SerializeToString())
    validation_model = model_pb2.Model()
    validation_model.ParseFromString(zlib.decompress(output_bytes))
    if len(validation_model.nodes) != 1 or validation_model.nodes[0].name != arguments.output_model:
        raise RuntimeError("Copied model failed serialization validation.")
    output_materials = {mesh.material for mesh in validation_model.nodes[0].meshes}
    if output_materials != set(material_map.values()):
        raise RuntimeError("Copied model failed material mapping validation.")

    arguments.output.parent.mkdir(parents=True, exist_ok=True)
    arguments.output.write_bytes(output_bytes)
    print(
        f"Created {arguments.output} from {arguments.source}: "
        f"{len(source_bytes)}->{len(output_bytes)} bytes, "
        f"SHA-256 {hashlib.sha256(output_bytes).hexdigest()}."
    )


if __name__ == "__main__":
    main()
