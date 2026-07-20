import argparse
import sys
import zlib
from pathlib import Path

import create_half_timbermesh as timbermesh


CLIP_COORDINATE = 1.5
MODEL_SPECS = (
    (
        "LargePile.Folktails.Model",
        "DualDistrictPile.Folktails.Model",
        "Buildings/Storage/DualDistrictPile/DualDistrictPile.Folktails.Model.timbermesh",
    ),
    (
        "LargePile.Folktails.ConstructionStage0.Model",
        "DualDistrictPile.Folktails.ConstructionStage0.Model",
        "Buildings/Storage/DualDistrictPile/DualDistrictPile.Folktails.ConstructionStage0.Model.timbermesh",
    ),
    (
        "LargeIndustrialPile.IronTeeth.Model",
        "DualDistrictPile.IronTeeth.Model",
        "Buildings/Storage/DualDistrictPile/DualDistrictPile.IronTeeth.Model.timbermesh",
    ),
    (
        "LargeIndustrialPile.IronTeeth.ConstructionStage0.Model",
        "DualDistrictPile.IronTeeth.ConstructionStage0.Model",
        "Buildings/Storage/DualDistrictPile/DualDistrictPile.IronTeeth.ConstructionStage0.Model.timbermesh",
    ),
    (
        "ConstructionBase3x3.Model",
        "DualDistrictPile.ConstructionBase.Model",
        "Buildings/Storage/DualDistrictPile/DualDistrictPile.ConstructionBase.Model.timbermesh",
    ),
)


def parse_arguments():
    parser = argparse.ArgumentParser(
        description="Generate the complete 1.5-tile model set for the asymmetric dual-district pile."
    )
    parser.add_argument("--resources-assets", required=True, type=Path)
    parser.add_argument("--unitypy-dir", required=True, type=Path)
    parser.add_argument("--timbermesh-plugin-dir", required=True, type=Path)
    parser.add_argument("--output-root", required=True, type=Path)
    return parser.parse_args()


def generate_model(environment, model_pb2, source_name, output_name, output_path):
    serialized_model = timbermesh.find_source_blob(environment, source_name)
    model = model_pb2.Model()
    model.ParseFromString(serialized_model)
    if len(model.nodes) != 1:
        raise RuntimeError(f"Expected one source node in {source_name}, found {len(model.nodes)}.")

    source_vertex_count = model.nodes[0].vertexCount
    source_triangle_count = sum(len(mesh.indices) // 3 for mesh in model.nodes[0].meshes)
    model.nodes[0].name = output_name
    _, output_vertices = timbermesh.clip_node(model.nodes[0], CLIP_COORDINATE)
    timbermesh.validate_model(model, CLIP_COORDINATE)

    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_bytes(zlib.compress(model.SerializeToString()))
    round_trip = model_pb2.Model()
    round_trip.ParseFromString(zlib.decompress(output_path.read_bytes()))
    timbermesh.validate_model(round_trip, CLIP_COORDINATE)
    if round_trip.nodes[0].name != output_name:
        raise RuntimeError(
            f"Round-trip identity mismatch in {output_path}: {round_trip.nodes[0].name}."
        )

    output_triangle_count = sum(len(mesh.indices) // 3 for mesh in model.nodes[0].meshes)
    print(
        f"Created {output_path}: {source_vertex_count}->{len(output_vertices)} vertices, "
        f"{source_triangle_count}->{output_triangle_count} triangles."
    )


def main():
    arguments = parse_arguments()
    sys.path.insert(0, str(arguments.unitypy_dir.resolve()))
    sys.path.insert(0, str(arguments.timbermesh_plugin_dir.resolve()))

    import UnityPy
    import model_pb2

    environment = UnityPy.load(str(arguments.resources_assets.resolve()))
    for source_name, output_name, relative_path in MODEL_SPECS:
        generate_model(
            environment,
            model_pb2,
            source_name,
            output_name,
            arguments.output_root / relative_path,
        )


if __name__ == "__main__":
    main()
