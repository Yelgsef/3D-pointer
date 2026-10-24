import numpy as np
import pytest

from human_perception.rgbd_reconstruction import back_project, sample_depth
from human_perception.pointing_geometry import (
    angle_degrees, parse_candidates, rank_candidates, transform_point,
)


def test_center_pixel_reconstructs_on_optical_axis():
    assert back_project(320.0, 180.0, 2.0, 577.9135, 577.9135, 320.0, 180.0) == (0.0, 0.0, 2.0)


def test_off_center_pixel_uses_camera_info_intrinsics():
    point = back_project(435.5827, 237.7914, 2.0, 577.9135, 577.9135, 320.0, 180.0)
    assert point == pytest.approx((0.4, 0.2, 2.0), abs=0.001)


def test_depth_sampler_uses_valid_neighborhood_median():
    depth = np.full((5, 5), 2.0, dtype=np.float32)
    depth[1, 1] = 0.0
    assert sample_depth(depth, 2, 2) == pytest.approx(2.0)


def test_depth_sampler_rejects_invalid_and_discontinuous_regions():
    assert sample_depth(np.zeros((3, 3), dtype=np.float32), 1, 1) is None
    depth = np.full((3, 3), 2.0, dtype=np.float32)
    depth[:, 2] = 6.0
    assert sample_depth(depth, 1, 1) is None


def test_depth_sampler_rejects_pixels_outside_image():
    assert sample_depth(np.ones((2, 2), dtype=np.float32), -0.6, 0) is None


def test_back_projection_rejects_nonpositive_optical_depth():
    with pytest.raises(ValueError):
        back_project(1.0, 1.0, 0.0, 1.0, 1.0, 0.0, 0.0)


def test_candidate_ranking_prefers_angularly_aligned_object():
    candidates = {"A": (3.0, 0.0, 0.0), "B": (0.0, 3.0, 0.0)}
    assert rank_candidates((0.0, 0.0, 0.0), (1.0, 0.0, 0.0), candidates) == [
        ("A", 0.0), ("B", 90.0)
    ]


def test_quaternion_transform_rotates_and_translates_point():
    half_sqrt = 2 ** -0.5
    result = transform_point((1.0, 0.0, 0.0), (2.0, 3.0, 4.0),
                             (0.0, 0.0, half_sqrt, half_sqrt))
    assert result == pytest.approx((2.0, 4.0, 4.0))


def test_opposite_ray_is_180_degrees():
    assert angle_degrees((1.0, 0.0, 0.0), (-1.0, 0.0, 0.0)) == pytest.approx(180.0)


def test_candidate_registry_parses_stable_ids_and_xyz():
    registry = parse_candidates('{"A":[1,2,3],"B":[4,5,6]}')
    assert registry == {"A": (1.0, 2.0, 3.0), "B": (4.0, 5.0, 6.0)}


def test_candidate_registry_rejects_incomplete_map():
    with pytest.raises(ValueError):
        parse_candidates('{"A":[1,2,3]}')
