import pytest

from human_perception.coordinate_frames import (
    flu_to_camera_optical,
    project_optical_point,
    unity_ruf_to_flu,
)


def test_unity_forward_maps_to_ros_forward_and_optical_depth():
    flu = unity_ruf_to_flu((0.0, 0.0, 2.0))
    assert flu == (2.0, 0.0, 0.0)
    assert flu_to_camera_optical(flu) == (0.0, 0.0, 2.0)


def test_unity_right_and_up_map_to_optical_right_and_down():
    assert flu_to_camera_optical(unity_ruf_to_flu((1.0, 0.0, 0.0))) == (1.0, 0.0, 0.0)
    assert flu_to_camera_optical(unity_ruf_to_flu((0.0, 1.0, 0.0))) == (0.0, -1.0, 0.0)


def test_projection_matches_camera_info_intrinsics():
    pixel = project_optical_point((0.0, 0.0, 2.0), 577.9135, 577.9135, 320.0, 180.0)
    assert pixel == pytest.approx((320.0, 180.0))

    pixel = project_optical_point((0.4, 0.2, 2.0), 577.9135, 577.9135, 320.0, 180.0)
    assert pixel == pytest.approx((435.5827, 237.7914), abs=0.001)


def test_projection_rejects_points_behind_camera():
    with pytest.raises(ValueError):
        project_optical_point((0.0, 0.0, -1.0), 577.9135, 577.9135, 320.0, 180.0)
