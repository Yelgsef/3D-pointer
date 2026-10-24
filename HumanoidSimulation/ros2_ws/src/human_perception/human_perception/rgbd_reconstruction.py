"""Pure helpers for aligned depth sampling and optical-frame back-projection."""

import math

import numpy as np


def sample_depth(depth, u, v, radius=1, max_spread=None):
    """Return a robust local depth or None at invalid/discontinuous pixels."""
    height, width = depth.shape
    if not math.isfinite(u) or not math.isfinite(v):
        return None
    x = int(math.floor(u + 0.5))
    y = int(math.floor(v + 0.5))
    if x < 0 or y < 0 or x >= width or y >= height:
        return None

    region = depth[max(0, y - radius):min(height, y + radius + 1),
                   max(0, x - radius):min(width, x + radius + 1)]
    values = region[np.isfinite(region) & (region > 0.0)]
    if values.size == 0:
        return None
    median = float(np.median(values))
    spread = float(np.percentile(values, 90) - np.percentile(values, 10))
    limit = max_spread if max_spread is not None else max(0.08, median * 0.05)
    if spread > limit:
        return None
    return median


def back_project(u, v, depth_m, fx, fy, cx, cy):
    """Convert pixel and optical-axis depth to metric optical XYZ."""
    values = (u, v, depth_m, fx, fy, cx, cy)
    if not all(math.isfinite(value) for value in values):
        raise ValueError("Projection inputs must be finite")
    if depth_m <= 0 or fx <= 0 or fy <= 0:
        raise ValueError("Depth and focal lengths must be positive")
    return ((u - cx) * depth_m / fx, (v - cy) * depth_m / fy, depth_m)
