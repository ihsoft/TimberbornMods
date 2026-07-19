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
    return parser.parse_args()


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

    model.nodes[0].name = arguments.output_model
    output_bytes = zlib.compress(model.SerializeToString())
    validation_model = model_pb2.Model()
    validation_model.ParseFromString(zlib.decompress(output_bytes))
    if len(validation_model.nodes) != 1 or validation_model.nodes[0].name != arguments.output_model:
        raise RuntimeError("Copied model failed serialization validation.")

    arguments.output.parent.mkdir(parents=True, exist_ok=True)
    arguments.output.write_bytes(output_bytes)
    print(
        f"Created {arguments.output} from {arguments.source}: "
        f"{len(source_bytes)}->{len(output_bytes)} bytes, "
        f"SHA-256 {hashlib.sha256(output_bytes).hexdigest()}."
    )


if __name__ == "__main__":
    main()
