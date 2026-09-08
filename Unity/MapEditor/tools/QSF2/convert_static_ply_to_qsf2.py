"""Convert one binary Gaussian PLY into the fixed, SH0-only QSF2 profile."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import numpy as np

from qsf2 import validate_qsf2, write_qsf2


SH_C0 = np.float32(0.2820948)
PLY_TYPES = {
    "char": "i1",
    "uchar": "u1",
    "short": "<i2",
    "ushort": "<u2",
    "int": "<i4",
    "uint": "<u4",
    "float": "<f4",
    "double": "<f8",
}
REQUIRED_PROPERTIES = (
    "x", "y", "z", "f_dc_0", "f_dc_1", "f_dc_2", "opacity",
    "scale_0", "scale_1", "scale_2", "rot_0", "rot_1", "rot_2", "rot_3",
)


def read_gaussian_ply(path: Path) -> np.ndarray:
    with path.open("rb") as stream:
        if stream.readline().strip() != b"ply" or stream.readline().strip() != b"format binary_little_endian 1.0":
            raise ValueError("Only binary_little_endian PLY 1.0 files are supported")

        vertex_count = None
        current_element = None
        properties: list[tuple[str, str]] = []
        while True:
            raw = stream.readline()
            if not raw:
                raise ValueError("Unexpected EOF in PLY header")
            line = raw.decode("ascii").strip()
            if line == "end_header":
                break
            if line.startswith("element "):
                _, current_element, count = line.split()
                if current_element == "vertex":
                    vertex_count = int(count)
                elif int(count) != 0:
                    raise ValueError(f"Unsupported non-empty PLY element: {current_element}")
            elif line.startswith("property ") and current_element == "vertex":
                parts = line.split()
                if len(parts) != 3 or parts[1] == "list" or parts[1] not in PLY_TYPES:
                    raise ValueError(f"Unsupported vertex property: {line}")
                properties.append((parts[2], PLY_TYPES[parts[1]]))

        if not vertex_count or not properties:
            raise ValueError("PLY has no vertex payload")
        missing = sorted(set(REQUIRED_PROPERTIES) - {name for name, _ in properties})
        if missing:
            raise ValueError("PLY is missing Gaussian properties: " + ", ".join(missing))

        vertices = np.fromfile(stream, dtype=np.dtype(properties), count=vertex_count)
        if vertices.shape[0] != vertex_count or stream.read(1):
            raise ValueError("PLY vertex payload is truncated or has trailing bytes")
        return vertices


def pack_smallest_three(wxyz: np.ndarray) -> np.ndarray:
    norms = np.linalg.norm(wxyz, axis=1, keepdims=True)
    if np.any(~np.isfinite(norms)) or np.any(norms <= 0):
        raise ValueError("PLY contains an invalid quaternion")

    # Match the Unity Gaussian importer: wxyz to xyzw, then mirror handedness on Y/Z.
    xyzw = (wxyz / norms)[:, [1, 2, 3, 0]].astype(np.float32)
    xyzw[:, 1:3] *= np.float32(-1.0)
    largest = np.argmax(np.abs(xyzw), axis=1)
    packed = np.empty((xyzw.shape[0], 4), dtype=np.float32)
    for index in range(4):
        rows = largest == index
        if not np.any(rows):
            continue
        q = xyzw[rows]
        reordered = q[:, [1, 2, 3, 0]] if index == 0 else (
            q[:, [0, 2, 3, 1]] if index == 1 else q[:, [0, 1, 3, 2]] if index == 2 else q
        )
        sign = np.where(reordered[:, 3:4] >= 0, 1.0, -1.0).astype(np.float32)
        packed[rows, :3] = reordered[:, :3] * sign * np.float32(np.sqrt(2.0) * 0.5) + np.float32(0.5)
        packed[rows, 3] = np.float32(index / 3.0)

    return (
        (np.clip(packed[:, 0], 0, 1) * np.float32(1023.5)).astype(np.uint32)
        | ((np.clip(packed[:, 1], 0, 1) * np.float32(1023.5)).astype(np.uint32) << np.uint32(10))
        | ((np.clip(packed[:, 2], 0, 1) * np.float32(1023.5)).astype(np.uint32) << np.uint32(20))
        | ((np.clip(packed[:, 3], 0, 1) * np.float32(3.5)).astype(np.uint32) << np.uint32(30))
    )


def convert_arrays(vertices: np.ndarray) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
    positions = np.column_stack((vertices["x"], vertices["y"], vertices["z"])).astype(np.float32)
    positions[:, 0] *= np.float32(-1.0)
    colors = np.empty((positions.shape[0], 4), dtype=np.float32)
    colors[:, 0] = vertices["f_dc_0"] * SH_C0 + np.float32(0.5)
    colors[:, 1] = vertices["f_dc_1"] * SH_C0 + np.float32(0.5)
    colors[:, 2] = vertices["f_dc_2"] * SH_C0 + np.float32(0.5)
    colors[:, 3] = np.float32(1.0) / (np.float32(1.0) + np.exp(-vertices["opacity"]))
    scales = np.abs(np.exp(np.column_stack((vertices["scale_0"], vertices["scale_1"], vertices["scale_2"])))).astype(np.float32)
    rotations = pack_smallest_three(np.column_stack((vertices["rot_0"], vertices["rot_1"], vertices["rot_2"], vertices["rot_3"])))
    other = np.empty((positions.shape[0], 4), dtype=np.uint32)
    other[:, 0] = rotations
    other[:, 1:4] = scales.view(np.uint32)
    return positions, other, colors


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--source-index", type=int, default=0)
    parser.add_argument("--force", action="store_true")
    args = parser.parse_args()
    source = args.input.resolve()
    target = args.output.resolve()
    if target.exists() and not args.force:
        parser.error(f"Refusing to overwrite {target}; pass --force to replace it")

    target.parent.mkdir(parents=True, exist_ok=True)
    positions, other, colors = convert_arrays(read_gaussian_ply(source))
    byte_count, metrics = write_qsf2(target, args.source_index, positions, other, colors)
    validation = validate_qsf2(target)
    report = {
        "schemaVersion": 1,
        "converter": "Pascal Proto static PLY to QSF2",
        "sourceFile": source.name,
        "sourceSha256": sha256(source),
        "outputFile": target.name,
        "outputSha256": sha256(target),
        "sourceBytes": source.stat().st_size,
        "qsf2Bytes": byte_count,
        "reductionPercent": (1.0 - byte_count / source.stat().st_size) * 100.0,
        "splatCount": validation["splatCount"],
        "encoding": {"qsfVersion": 2, "chunkSize": 256, "position": "Norm16", "scale": "Norm11", "color": "Norm8x4", "sphericalHarmonics": "SH0"},
        "maxPositionAbsoluteError": metrics.max_position_error,
        "maxScaleRelativeError": metrics.max_scale_relative_error,
        "maxColorAbsoluteError": metrics.max_color_error,
        "maxOpacityAbsoluteError": metrics.max_opacity_error,
    }
    report_path = target.with_suffix(".conversion.json")
    report_path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
