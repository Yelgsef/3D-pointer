"""Deterministic geometry helpers for candidate ranking and TF transforms."""

import math
import json


def normalize(vector):
    length = math.sqrt(sum(component * component for component in vector))
    if not math.isfinite(length) or length < 1e-8:
        raise ValueError("Cannot normalize a zero or nonfinite vector")
    return tuple(component / length for component in vector)


def angle_degrees(first, second):
    a = normalize(first)
    b = normalize(second)
    dot = max(-1.0, min(1.0, sum(x * y for x, y in zip(a, b))))
    return math.degrees(math.acos(dot))


def rotate_by_quaternion(vector, quaternion):
    """Rotate a 3-vector by an xyzw unit quaternion."""
    x, y, z, w = quaternion
    vx, vy, vz = vector
    tx = 2.0 * (y * vz - z * vy)
    ty = 2.0 * (z * vx - x * vz)
    tz = 2.0 * (x * vy - y * vx)
    return (
        vx + w * tx + (y * tz - z * ty),
        vy + w * ty + (z * tx - x * tz),
        vz + w * tz + (x * ty - y * tx),
    )


def transform_point(point, translation, quaternion):
    rotated = rotate_by_quaternion(point, quaternion)
    return tuple(rotated[i] + translation[i] for i in range(3))


def rank_candidates(origin, direction, candidates):
    ranked = []
    for candidate_id, position in candidates.items():
        to_candidate = tuple(position[i] - origin[i] for i in range(3))
        ranked.append((candidate_id, angle_degrees(direction, to_candidate)))
    return sorted(ranked, key=lambda item: (item[1], item[0]))


def parse_candidates(serialized):
    """Validate a JSON map from stable object IDs to map-frame XYZ positions."""
    candidates = json.loads(serialized)
    if not isinstance(candidates, dict) or len(candidates) < 2:
        raise ValueError("Candidate registry must contain at least two objects")
    validated = {}
    for candidate_id, position in candidates.items():
        if not isinstance(candidate_id, str) or len(position) != 3:
            raise ValueError("Each candidate needs a stable string ID and XYZ position")
        values = tuple(float(value) for value in position)
        if not all(math.isfinite(value) for value in values):
            raise ValueError("Candidate positions must be finite")
        validated[candidate_id] = values
    return validated
