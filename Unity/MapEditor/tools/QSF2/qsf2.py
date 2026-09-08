"""QSF2 quantization, validation, and legacy QSF1 decoding helpers."""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
import math
import struct

import numpy as np


QSF1_MAGIC = 0x31534651
QSF2_MAGIC = 0x32534651
QSF1_HEADER = struct.Struct("<Iiii6fiii")
QSF2_HEADER = struct.Struct("<Iiii6fiiiiiii")
QSF2_VERSION = 2
QSF_TEXTURE_WIDTH = 2048
QSF_CHUNK_SIZE = 256
QSF_CHUNK = struct.Struct("<4I6f6I")

VECTOR_NORM16 = 1
VECTOR_NORM11 = 2
COLOR_NORM8X4 = 2


class QsfError(RuntimeError):
    pass


@dataclass
class LegacyFrame:
    source_index: int
    splat_count: int
    bounds_min: np.ndarray
    bounds_max: np.ndarray
    positions: np.ndarray
    rotations: np.ndarray
    scales: np.ndarray
    colors: np.ndarray


@dataclass
class QuantizationMetrics:
    max_position_error: float = 0.0
    max_scale_relative_error: float = 0.0
    max_color_error: float = 0.0
    max_opacity_error: float = 0.0

    def merge(self, other: "QuantizationMetrics") -> None:
        self.max_position_error = max(self.max_position_error, other.max_position_error)
        self.max_scale_relative_error = max(self.max_scale_relative_error, other.max_scale_relative_error)
        self.max_color_error = max(self.max_color_error, other.max_color_error)
        self.max_opacity_error = max(self.max_opacity_error, other.max_opacity_error)


def align4(value: int) -> int:
    return (value + 3) & ~3


def texture_height(splat_count: int) -> int:
    rows = max(1, (splat_count + QSF_TEXTURE_WIDTH - 1) // QSF_TEXTURE_WIDTH)
    return ((rows + 15) // 16) * 16


def texture_capacity(splat_count: int) -> int:
    return QSF_TEXTURE_WIDTH * texture_height(splat_count)


def splat_to_texture_indices(splat_count: int) -> np.ndarray:
    indices = np.arange(splat_count, dtype=np.uint32)
    local = indices & 0xFF
    x = np.zeros(splat_count, dtype=np.uint32)
    y = np.zeros(splat_count, dtype=np.uint32)
    for bit in range(4):
        x |= ((local >> (bit * 2)) & 1) << bit
        y |= ((local >> (bit * 2 + 1)) & 1) << bit
    block = indices >> 8
    return (((block // 128) * 16 + y) * QSF_TEXTURE_WIDTH + (block % 128) * 16 + x).astype(np.int64)


def square_centered01(values: np.ndarray) -> np.ndarray:
    centered = values - np.float32(0.5)
    return centered * np.abs(centered) * np.float32(2.0) + np.float32(0.5)


def inverse_square_centered01(values: np.ndarray) -> np.ndarray:
    centered = (values - np.float32(0.5)) * np.float32(0.5)
    return np.sqrt(np.abs(centered)) * np.sign(centered) + np.float32(0.5)


def _spread_21(values: np.ndarray) -> np.ndarray:
    values = values.astype(np.uint64, copy=False) & np.uint64(0x1FFFFF)
    values = (values | (values << np.uint64(32))) & np.uint64(0x1F00000000FFFF)
    values = (values | (values << np.uint64(16))) & np.uint64(0x1F0000FF0000FF)
    values = (values | (values << np.uint64(8))) & np.uint64(0x100F00F00F00F00F)
    values = (values | (values << np.uint64(4))) & np.uint64(0x10C30C30C30C30C3)
    values = (values | (values << np.uint64(2))) & np.uint64(0x1249249249249249)
    return values


def morton_order(positions: np.ndarray) -> np.ndarray:
    bounds_min = positions.min(axis=0)
    bounds_max = positions.max(axis=0)
    extent = np.maximum(bounds_max - bounds_min, np.float32(1.0e-5))
    normalized = np.clip((positions - bounds_min) / extent, 0.0, 1.0)
    integer = (normalized * np.float32((1 << 21) - 1)).astype(np.uint64)
    codes = _spread_21(integer[:, 0]) | (_spread_21(integer[:, 1]) << np.uint64(1)) | (_spread_21(integer[:, 2]) << np.uint64(2))
    return np.argsort(codes, kind="stable")


def _half_pair(low: float, high: float) -> tuple[int, float, float]:
    low16 = np.float16(low)
    high16 = np.float16(high)
    if float(low16) > low:
        low16 = np.nextafter(low16, np.float16(-math.inf), dtype=np.float16)
    if float(high16) < high:
        high16 = np.nextafter(high16, np.float16(math.inf), dtype=np.float16)
    if not float(high16) > float(low16):
        high16 = np.nextafter(low16, np.float16(math.inf), dtype=np.float16)
    low_bits = int(np.asarray(low16, dtype="<f2").view("<u2"))
    high_bits = int(np.asarray(high16, dtype="<f2").view("<u2"))
    return low_bits | (high_bits << 16), float(low16), float(high16)


def _encode_norm16(values: np.ndarray) -> np.ndarray:
    return np.floor(np.clip(values, 0.0, 1.0) * np.float32(65535.5)).astype("<u2")


def _encode_norm11(values: np.ndarray) -> np.ndarray:
    values = np.clip(values, 0.0, 1.0)
    x = np.floor(values[:, 0] * np.float32(2047.5)).astype(np.uint32)
    y = np.floor(values[:, 1] * np.float32(1023.5)).astype(np.uint32)
    z = np.floor(values[:, 2] * np.float32(2047.5)).astype(np.uint32)
    return (x | (y << 11) | (z << 21)).astype("<u4")


def _decode_norm11(values: np.ndarray) -> np.ndarray:
    values = values.astype(np.uint32, copy=False)
    return np.column_stack((
        (values & 0x7FF) / np.float32(2047.0),
        ((values >> 11) & 0x3FF) / np.float32(1023.0),
        ((values >> 21) & 0x7FF) / np.float32(2047.0),
    )).astype(np.float32)


def _encode_unorm8(values: np.ndarray) -> np.ndarray:
    return np.floor(np.clip(values, 0.0, 1.0) * np.float32(255.5)).astype(np.uint8)


def estimate_qsf2_bytes(splat_count: int) -> int:
    chunks = (splat_count + QSF_CHUNK_SIZE - 1) // QSF_CHUNK_SIZE
    return (
        QSF2_HEADER.size
        + chunks * QSF_CHUNK.size
        + align4(splat_count * 6)
        + splat_count * 8
        + texture_capacity(splat_count) * 4
    )


def _validate_source_arrays(positions: np.ndarray, other_words: np.ndarray, colors: np.ndarray) -> None:
    count = positions.shape[0]
    if positions.shape != (count, 3) or other_words.shape != (count, 4) or colors.shape != (count, 4) or count <= 0:
        raise QsfError("QSF source arrays have incompatible shapes.")
    scales = other_words[:, 1:4].view("<f4")
    if not np.isfinite(positions).all() or not np.isfinite(scales).all() or not np.isfinite(colors).all():
        raise QsfError("QSF source contains NaN or infinity.")
    if np.any(scales <= 0.0):
        raise QsfError("QSF scale components must be positive.")
    if np.any(colors[:, 3] < 0.0) or np.any(colors[:, 3] > 1.0):
        raise QsfError("QSF opacity must be in the 0..1 range.")


def encode_qsf2_payload(
    positions: np.ndarray,
    other_words: np.ndarray,
    colors: np.ndarray,
) -> tuple[np.ndarray, bytes, bytes, bytes, bytes, QuantizationMetrics]:
    positions = np.asarray(positions, dtype="<f4")
    other_words = np.asarray(other_words, dtype="<u4")
    colors = np.asarray(colors, dtype="<f4")
    _validate_source_arrays(positions, other_words, colors)

    order = morton_order(positions)
    positions = positions[order]
    other_words = other_words[order]
    colors = colors[order]
    scales = other_words[:, 1:4].view("<f4").copy()
    scale_root = np.power(scales, np.float32(1.0 / 8.0)).astype(np.float32)
    transformed_colors = colors.copy()
    transformed_colors[:, 3] = square_centered01(transformed_colors[:, 3])

    count = positions.shape[0]
    chunk_count = (count + QSF_CHUNK_SIZE - 1) // QSF_CHUNK_SIZE
    chunk_records = np.zeros(chunk_count, dtype=np.dtype([
        ("col", "<u4", (4,)), ("pos", "<f4", (6,)), ("tail", "<u4", (6,))
    ]))
    pos_encoded = np.empty((count, 3), dtype="<u2")
    scale_encoded = np.empty(count, dtype="<u4")
    color_encoded = np.empty((count, 4), dtype=np.uint8)
    metrics = QuantizationMetrics()

    for chunk_index in range(chunk_count):
        begin = chunk_index * QSF_CHUNK_SIZE
        end = min(begin + QSF_CHUNK_SIZE, count)
        pos = positions[begin:end]
        scl = scale_root[begin:end]
        col = transformed_colors[begin:end]

        pos_min = pos.min(axis=0)
        pos_max = np.maximum(pos.max(axis=0), pos_min + np.float32(1.0e-5))
        pos_normalized = (pos - pos_min) / (pos_max - pos_min)
        pos_q = _encode_norm16(pos_normalized)
        pos_encoded[begin:end] = pos_q

        scl_min = scl.min(axis=0)
        scl_max = np.maximum(scl.max(axis=0), scl_min + np.float32(1.0e-5))
        scl_pairs = [_half_pair(float(scl_min[i]), float(scl_max[i])) for i in range(3)]
        scl_low = np.array([pair[1] for pair in scl_pairs], dtype=np.float32)
        scl_high = np.array([pair[2] for pair in scl_pairs], dtype=np.float32)
        scl_normalized = (scl - scl_low) / (scl_high - scl_low)
        scl_q = _encode_norm11(scl_normalized)
        scale_encoded[begin:end] = scl_q

        col_min = col.min(axis=0)
        col_max = np.maximum(col.max(axis=0), col_min + np.float32(1.0e-5))
        col_pairs = [_half_pair(float(col_min[i]), float(col_max[i])) for i in range(4)]
        col_low = np.array([pair[1] for pair in col_pairs], dtype=np.float32)
        col_high = np.array([pair[2] for pair in col_pairs], dtype=np.float32)
        col_normalized = (col - col_low) / (col_high - col_low)
        col_q = _encode_unorm8(col_normalized)
        color_encoded[begin:end] = col_q

        chunk_records[chunk_index]["col"] = [pair[0] for pair in col_pairs]
        chunk_records[chunk_index]["pos"] = [
            pos_min[0], pos_max[0], pos_min[1], pos_max[1], pos_min[2], pos_max[2]
        ]
        chunk_records[chunk_index]["tail"][:3] = [pair[0] for pair in scl_pairs]

        pos_decoded = pos_min + (pos_q.astype(np.float32) / np.float32(65535.0)) * (pos_max - pos_min)
        scl_decoded_root = scl_low + _decode_norm11(scl_q) * (scl_high - scl_low)
        scl_decoded = np.power(scl_decoded_root, np.float32(8.0))
        col_decoded = col_low + (col_q.astype(np.float32) / np.float32(255.0)) * (col_high - col_low)
        alpha_decoded = inverse_square_centered01(col_decoded[:, 3])
        metrics.max_position_error = max(metrics.max_position_error, float(np.max(np.abs(pos_decoded - pos))))
        metrics.max_scale_relative_error = max(
            metrics.max_scale_relative_error,
            float(np.max(np.abs(scl_decoded - scales[begin:end]) / np.maximum(scales[begin:end], 1.0e-20))),
        )
        metrics.max_color_error = max(metrics.max_color_error, float(np.max(np.abs(col_decoded[:, :3] - colors[begin:end, :3]))))
        metrics.max_opacity_error = max(metrics.max_opacity_error, float(np.max(np.abs(alpha_decoded - colors[begin:end, 3]))))

    pos_bytes = bytearray(pos_encoded.tobytes(order="C"))
    pos_bytes.extend(b"\0" * (align4(len(pos_bytes)) - len(pos_bytes)))
    packed_other = np.column_stack((other_words[:, 0], scale_encoded)).astype("<u4", copy=False)
    color_texture = np.zeros((texture_capacity(count), 4), dtype=np.uint8)
    color_texture[splat_to_texture_indices(count)] = color_encoded
    return order, chunk_records.tobytes(), bytes(pos_bytes), packed_other.tobytes(), color_texture.tobytes(), metrics


def write_qsf2(
    path: Path,
    source_index: int,
    positions: np.ndarray,
    other_words: np.ndarray,
    colors: np.ndarray,
) -> tuple[int, QuantizationMetrics]:
    count = int(positions.shape[0])
    bounds_min = np.asarray(positions, dtype=np.float32).min(axis=0)
    bounds_max = np.asarray(positions, dtype=np.float32).max(axis=0)
    _, chunks, pos, other, color, metrics = encode_qsf2_payload(positions, other_words, colors)
    header = QSF2_HEADER.pack(
        QSF2_MAGIC, QSF2_VERSION, source_index, count,
        *[float(value) for value in bounds_min], *[float(value) for value in bounds_max],
        VECTOR_NORM16, VECTOR_NORM11, COLOR_NORM8X4,
        len(chunks), len(pos), len(other), len(color),
    )
    with path.open("wb") as stream:
        stream.write(header)
        stream.write(chunks)
        stream.write(pos)
        stream.write(other)
        stream.write(color)
    return len(header) + len(chunks) + len(pos) + len(other) + len(color), metrics


def validate_qsf2(path: Path) -> dict:
    with path.open("rb") as stream:
        raw = stream.read(QSF2_HEADER.size)
        if len(raw) != QSF2_HEADER.size:
            raise QsfError(f"Truncated QSF2 header: {path}")
        values = QSF2_HEADER.unpack(raw)
    magic, version, source_index, count = values[:4]
    pos_format, scale_format, color_format = values[10:13]
    chunk_length, pos_length, other_length, color_length = values[13:17]
    if magic != QSF2_MAGIC or version != QSF2_VERSION:
        if magic == QSF1_MAGIC:
            raise QsfError(f"QSF v1 is not supported; convert this frame to QSF v2: {path}")
        raise QsfError(f"Unsupported QSF frame: {path}")
    if count <= 0 or (pos_format, scale_format, color_format) != (VECTOR_NORM16, VECTOR_NORM11, COLOR_NORM8X4):
        raise QsfError(f"Unsupported QSF2 encoding: {path}")
    expected = (
        ((count + QSF_CHUNK_SIZE - 1) // QSF_CHUNK_SIZE) * QSF_CHUNK.size,
        align4(count * 6), count * 8, texture_capacity(count) * 4,
    )
    if (chunk_length, pos_length, other_length, color_length) != expected:
        raise QsfError(f"QSF2 payload sizes do not match the splat count: {path}")
    if path.stat().st_size != QSF2_HEADER.size + sum(expected):
        raise QsfError(f"QSF2 file length does not match its header: {path}")
    return {"sourceIndex": source_index, "splatCount": count, "payloadLengths": expected}


def read_qsf1(path: Path) -> LegacyFrame:
    with path.open("rb") as stream:
        raw = stream.read(QSF1_HEADER.size)
        if len(raw) != QSF1_HEADER.size:
            raise QsfError(f"Truncated QSF1 header: {path}")
        values = QSF1_HEADER.unpack(raw)
        magic, version, source_index, count = values[:4]
        if magic != QSF1_MAGIC or version != 1 or count <= 0:
            raise QsfError(f"Not a valid QSF1 frame: {path}")
        pos_length, other_length, color_length = values[-3:]
        expected = (count * 12, count * 16, texture_capacity(count) * 16)
        if (pos_length, other_length, color_length) != expected:
            raise QsfError(f"Invalid QSF1 payload lengths: {path}")
        pos_raw = stream.read(pos_length)
        other_raw = stream.read(other_length)
        color_raw = stream.read(color_length)
        if len(pos_raw) != pos_length or len(other_raw) != other_length or len(color_raw) != color_length or stream.read(1):
            raise QsfError(f"Truncated or trailing QSF1 payload: {path}")
    positions = np.frombuffer(pos_raw, dtype="<f4").reshape(count, 3).copy()
    other = np.frombuffer(other_raw, dtype="<u4").reshape(count, 4).copy()
    color_texture = np.frombuffer(color_raw, dtype="<f4").reshape(-1, 4)
    colors = color_texture[splat_to_texture_indices(count)].copy()
    return LegacyFrame(
        source_index, count,
        np.array(values[4:7], dtype=np.float32), np.array(values[7:10], dtype=np.float32),
        positions, other[:, 0].copy(), other[:, 1:4].view("<f4").copy(), colors,
    )


def legacy_frame_arrays(frame: LegacyFrame) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
    other = np.empty((frame.splat_count, 4), dtype="<u4")
    other[:, 0] = frame.rotations
    other[:, 1:4] = frame.scales.astype("<f4", copy=False).view("<u4")
    return frame.positions, other, frame.colors
