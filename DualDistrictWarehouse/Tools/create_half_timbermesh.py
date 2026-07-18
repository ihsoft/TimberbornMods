import argparse
import math
import struct
import sys
import zlib
from pathlib import Path


CLIP_AXIS = 2
CLIP_COORDINATE = 1.0
EPSILON = 1e-6


def parse_arguments():
    parser = argparse.ArgumentParser(
        description="Extract a stock Timbermesh and clip it to the entrance-side 3x1 half."
    )
    parser.add_argument("--resources-assets", required=True, type=Path)
    parser.add_argument("--unitypy-dir", required=True, type=Path)
    parser.add_argument("--timbermesh-plugin-dir", required=True, type=Path)
    parser.add_argument("--source-model", required=True)
    parser.add_argument("--output-model", required=True)
    parser.add_argument("--output", required=True, type=Path)
    return parser.parse_args()


def component_pointer(component):
    return component[1] if isinstance(component, tuple) else component.component


def find_source_blob(environment, source_model):
    source_game_object = None
    for game_object_reader in environment.objects:
        if game_object_reader.type.name != "GameObject":
            continue
        game_object = game_object_reader.read()
        if game_object.m_Name == source_model:
            source_game_object = game_object
            break

    if source_game_object is None:
        raise RuntimeError(f"GameObject not found: {source_model}")

    candidates = []
    for component in source_game_object.m_Component:
        reader = component_pointer(component).deref()
        if reader is None or reader.type.name != "MonoBehaviour":
            continue
        raw_data = reader.get_raw_data()
        if len(raw_data) < 38:
            continue
        compressed_size = struct.unpack_from("<I", raw_data, 32)[0]
        compressed_data = raw_data[36 : 36 + compressed_size]
        if len(compressed_data) != compressed_size:
            continue
        try:
            serialized_model = zlib.decompress(compressed_data)
        except zlib.error:
            continue
        candidates.append(serialized_model)

    if len(candidates) != 1:
        raise RuntimeError(
            f"Expected one compressed Timbermesh component on {source_model}, found {len(candidates)}."
        )
    return candidates[0]


def decode_vertex_properties(node):
    properties = []
    for vertex_property in node.vertexProperties:
        if vertex_property.scalarType != 4:
            raise RuntimeError(
                f"Unsupported scalar type {vertex_property.scalarType} in {vertex_property.name}."
            )
        dimension = vertex_property.scalarTypeDimension
        expected_size = node.vertexCount * dimension * 4
        if len(vertex_property.data) != expected_size:
            raise RuntimeError(
                f"Unexpected data size for {vertex_property.name}: "
                f"{len(vertex_property.data)} instead of {expected_size}."
            )
        values = struct.unpack(f"<{node.vertexCount * dimension}f", vertex_property.data)
        vectors = [
            tuple(values[index : index + dimension])
            for index in range(0, len(values), dimension)
        ]
        properties.append((vertex_property, vectors))
    return properties


def interpolate_vector(first, second, factor):
    return tuple(a + (b - a) * factor for a, b in zip(first, second))


def normalized(vector):
    length = math.sqrt(sum(component * component for component in vector))
    if length <= EPSILON:
        return vector
    return tuple(component / length for component in vector)


def interpolate_vertex(first, second, factor):
    result = []
    for property_index, (first_value, second_value) in enumerate(zip(first, second)):
        value = interpolate_vector(first_value, second_value, factor)
        if property_index in (1, 2):
            direction = normalized(value[:3])
            value = direction + value[3:]
        result.append(value)
    position = list(result[0])
    position[CLIP_AXIS] = CLIP_COORDINATE
    result[0] = tuple(position)
    return result


def clip_triangle(indices, source_vertices, intersection_cache):
    polygon = [(('original', index), source_vertices[index]) for index in indices]
    clipped = []
    previous_key, previous_vertex = polygon[-1]
    previous_inside = previous_vertex[0][CLIP_AXIS] <= CLIP_COORDINATE + EPSILON

    for current_key, current_vertex in polygon:
        current_inside = current_vertex[0][CLIP_AXIS] <= CLIP_COORDINATE + EPSILON
        if previous_inside != current_inside:
            first_index = previous_key[1]
            second_index = current_key[1]
            edge_key = ('intersection', min(first_index, second_index), max(first_index, second_index))
            intersection = intersection_cache.get(edge_key)
            if intersection is None:
                previous_coordinate = previous_vertex[0][CLIP_AXIS]
                current_coordinate = current_vertex[0][CLIP_AXIS]
                factor = (CLIP_COORDINATE - previous_coordinate) / (
                    current_coordinate - previous_coordinate
                )
                intersection = interpolate_vertex(previous_vertex, current_vertex, factor)
                intersection_cache[edge_key] = intersection
            clipped.append((edge_key, intersection))
        if current_inside:
            clipped.append((current_key, current_vertex))
        previous_key = current_key
        previous_vertex = current_vertex
        previous_inside = current_inside

    return clipped


def triangle_is_degenerate(indices, vertices):
    if len(set(indices)) != 3:
        return True
    first = vertices[indices[0]][0]
    second = vertices[indices[1]][0]
    third = vertices[indices[2]][0]
    ab = tuple(second[index] - first[index] for index in range(3))
    ac = tuple(third[index] - first[index] for index in range(3))
    cross = (
        ab[1] * ac[2] - ab[2] * ac[1],
        ab[2] * ac[0] - ab[0] * ac[2],
        ab[0] * ac[1] - ab[1] * ac[0],
    )
    return sum(component * component for component in cross) <= EPSILON * EPSILON


def clip_node(node):
    decoded_properties = decode_vertex_properties(node)
    source_vertices = [
        [vectors[index] for _, vectors in decoded_properties]
        for index in range(node.vertexCount)
    ]
    output_vertices = []
    output_indices_by_key = {}
    intersection_cache = {}

    def output_index(key, vertex):
        existing_index = output_indices_by_key.get(key)
        if existing_index is not None:
            return existing_index
        index = len(output_vertices)
        output_indices_by_key[key] = index
        output_vertices.append(vertex)
        return index

    for mesh in node.meshes:
        source_indices = list(mesh.indices)
        if len(source_indices) % 3 != 0:
            raise RuntimeError(f"Mesh {mesh.material} does not contain triangle-list indices.")
        del mesh.indices[:]
        for offset in range(0, len(source_indices), 3):
            polygon = clip_triangle(
                source_indices[offset : offset + 3], source_vertices, intersection_cache
            )
            if len(polygon) < 3:
                continue
            polygon_indices = [output_index(key, vertex) for key, vertex in polygon]
            for index in range(1, len(polygon_indices) - 1):
                triangle = [polygon_indices[0], polygon_indices[index], polygon_indices[index + 1]]
                if triangle_is_degenerate(triangle, output_vertices):
                    continue
                mesh.indices.extend(triangle)

    node.vertexCount = len(output_vertices)
    for property_index, (vertex_property, _) in enumerate(decoded_properties):
        flattened = [
            scalar
            for vertex in output_vertices
            for scalar in vertex[property_index]
        ]
        vertex_property.data = struct.pack(f"<{len(flattened)}f", *flattened)

    return source_vertices, output_vertices


def validate_model(model):
    if len(model.nodes) != 1:
        raise RuntimeError(f"Expected one model node, found {len(model.nodes)}.")
    node = model.nodes[0]
    decoded_properties = decode_vertex_properties(node)
    positions = decoded_properties[0][1]
    if not positions:
        raise RuntimeError("Clipped model has no vertices.")
    if max(position[CLIP_AXIS] for position in positions) > CLIP_COORDINATE + EPSILON:
        raise RuntimeError("Clipped model still contains vertices behind the cut plane.")
    for mesh in node.meshes:
        if len(mesh.indices) % 3 != 0:
            raise RuntimeError(f"Output mesh {mesh.material} is not a triangle list.")
        if mesh.indices and max(mesh.indices) >= node.vertexCount:
            raise RuntimeError(f"Output mesh {mesh.material} has an invalid vertex index.")


def main():
    arguments = parse_arguments()
    sys.path.insert(0, str(arguments.unitypy_dir.resolve()))
    sys.path.insert(0, str(arguments.timbermesh_plugin_dir.resolve()))

    import UnityPy
    import model_pb2

    environment = UnityPy.load(str(arguments.resources_assets.resolve()))
    serialized_model = find_source_blob(environment, arguments.source_model)
    model = model_pb2.Model()
    model.ParseFromString(serialized_model)
    if len(model.nodes) != 1:
        raise RuntimeError(f"Expected one source node, found {len(model.nodes)}.")

    source_vertex_count = model.nodes[0].vertexCount
    source_triangle_count = sum(len(mesh.indices) // 3 for mesh in model.nodes[0].meshes)
    model.nodes[0].name = arguments.output_model
    _, output_vertices = clip_node(model.nodes[0])
    validate_model(model)

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
