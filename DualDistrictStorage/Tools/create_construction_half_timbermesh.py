import argparse
import sys
import zlib
from pathlib import Path

import create_half_timbermesh as timbermesh


def parse_arguments():
    parser = argparse.ArgumentParser(
        description="Extract and clip a stock construction stage to its entrance-side half."
    )
    parser.add_argument("--resources-assets", required=True, type=Path)
    parser.add_argument("--unitypy-dir", required=True, type=Path)
    parser.add_argument("--timbermesh-plugin-dir", required=True, type=Path)
    parser.add_argument("--source-model", required=True)
    parser.add_argument("--output-model", required=True)
    parser.add_argument("--output", required=True, type=Path)
    return parser.parse_args()


def main():
    arguments = parse_arguments()
    sys.path.insert(0, str(arguments.unitypy_dir.resolve()))
    sys.path.insert(0, str(arguments.timbermesh_plugin_dir.resolve()))

    import UnityPy
    import model_pb2

    environment = UnityPy.load(str(arguments.resources_assets.resolve()))
    serialized_model = timbermesh.find_source_blob(environment, arguments.source_model)
    model = model_pb2.Model()
    model.ParseFromString(serialized_model)
    if len(model.nodes) != 1:
        raise RuntimeError(f"Expected one source node, found {len(model.nodes)}.")

    source_vertex_count = model.nodes[0].vertexCount
    source_triangle_count = sum(len(mesh.indices) // 3 for mesh in model.nodes[0].meshes)
    model.nodes[0].name = arguments.output_model
    _, output_vertices = timbermesh.clip_node(model.nodes[0])
    timbermesh.validate_model(model)

    arguments.output.parent.mkdir(parents=True, exist_ok=True)
    arguments.output.write_bytes(zlib.compress(model.SerializeToString()))
    output_triangle_count = sum(len(mesh.indices) // 3 for mesh in model.nodes[0].meshes)
    print(
        f"Created {arguments.output}: "
        f"{source_vertex_count}->{len(output_vertices)} vertices, "
        f"{source_triangle_count}->{output_triangle_count} triangles."
    )


if __name__ == "__main__":
    main()
