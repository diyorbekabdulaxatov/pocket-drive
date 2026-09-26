"""Bake out front wheel toe/yaw in the v0.2.0 GLB, without changing topology.

Usage: python3 tools/fix_porsche_wheels.py input.glb output.glb
Only vertex positions/normals and front hub translations change. Texture bytes,
indices, materials and triangle counts remain untouched. Uses Python stdlib.
"""
import copy
import json
import math
import struct
import sys
from pathlib import Path


def fix(source, destination):
    blob = Path(source).read_bytes()
    assert blob[:4] == b'glTF'
    json_size = struct.unpack_from('<I', blob, 12)[0]
    doc = json.loads(blob[20:20 + json_size])
    bin_start = 20 + json_size
    bin_size, bin_type = struct.unpack_from('<II', blob, bin_start)
    assert bin_type == 0x004e4942
    data = bytearray(blob[bin_start + 8:bin_start + 8 + bin_size])

    def read(index):
        a = doc['accessors'][index]
        assert a['componentType'] == 5126 and a['type'] == 'VEC3'
        view = doc['bufferViews'][a['bufferView']]
        offset = view.get('byteOffset', 0) + a.get('byteOffset', 0)
        stride = view.get('byteStride', 12)
        return [struct.unpack_from('<fff', data, offset + i * stride) for i in range(a['count'])]

    def bounds(points):
        low = [min(p[i] for p in points) for i in range(3)]
        high = [max(p[i] for p in points) for i in range(3)]
        return low, high

    def rotate(p, angle):
        c, s = math.cos(angle), math.sin(angle)
        return (c * p[0] + s * p[2], p[1], -s * p[0] + c * p[2])

    def append_accessor(old_index, points):
        while len(data) % 4:
            data.append(0)
        offset = len(data)
        for point in points:
            data.extend(struct.pack('<fff', *point))
        view_index = len(doc['bufferViews'])
        doc['bufferViews'].append({'buffer': 0, 'byteOffset': offset, 'byteLength': len(points) * 12, 'target': 34962})
        accessor = copy.deepcopy(doc['accessors'][old_index])
        accessor.update(bufferView=view_index, byteOffset=0)
        if 'min' in accessor or 'max' in accessor:
            accessor['min'], accessor['max'] = bounds(points)
        doc['accessors'].append(accessor)
        return len(doc['accessors']) - 1

    wheels = {n['name']: n for n in doc['nodes'] if n.get('name', '').startswith('Wheel')}
    hubs = [wheels[name]['translation'] for name in ('WheelFL', 'WheelFR')]
    axle_z = sum(p[2] for p in hubs) / 2
    axle_y = sum(p[1] for p in hubs) / 2
    track_half = sum(abs(p[0]) for p in hubs) / 2
    for name in ('WheelFL', 'WheelFR'):
        wheel = wheels[name]
        children = [doc['nodes'][i] for i in wheel['children']]
        tyre = next(n for n in children if '_2_' in n['name'])
        prim = doc['meshes'][tyre['mesh']]['primitives'][0]
        points = read(prim['attributes']['POSITION'])

        # The narrowest projected tyre width identifies its axle, without
        # assuming a turn angle or being biased by uneven vertex density.
        def width(degrees):
            angle = math.radians(degrees)
            x = [rotate(p, angle)[0] for p in points]
            return max(x) - min(x)

        coarse = min(range(-300, 301), key=lambda i: width(i / 10)) / 10
        degrees = min((coarse + i / 1000 for i in range(-100, 101)), key=width)
        angle = math.radians(degrees)
        low, high = bounds([rotate(p, angle) for p in points])
        centre = [(a + b) / 2 for a, b in zip(low, high)]
        for child in children:
            for primitive in doc['meshes'][child['mesh']]['primitives']:
                attributes = primitive['attributes']
                for semantic in ('POSITION', 'NORMAL'):
                    if semantic not in attributes:
                        continue
                    old = attributes[semantic]
                    changed = [rotate(p, angle) for p in read(old)]
                    if semantic == 'POSITION':
                        changed = [tuple(v - c for v, c in zip(p, centre)) for p in changed]
                    attributes[semantic] = append_accessor(old, changed)
        wheel['translation'] = [-track_half if name.endswith('L') else track_half, axle_y, axle_z]
        print(f'{name}: baked yaw correction {degrees:.3f} degrees; recentered by {centre}; hub {wheel["translation"]}')

    doc['buffers'][0]['byteLength'] = len(data)
    encoded = json.dumps(doc, separators=(',', ':')).encode()
    encoded += b' ' * (-len(encoded) % 4)
    data.extend(b'\0' * (-len(data) % 4))
    result = struct.pack('<4sII', b'glTF', 2, 28 + len(encoded) + len(data))
    result += struct.pack('<II', len(encoded), 0x4e4f534a) + encoded
    result += struct.pack('<II', len(data), 0x004e4942) + data
    Path(destination).write_bytes(result)


if __name__ == '__main__':
    fix(sys.argv[1], sys.argv[2])
