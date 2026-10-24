import pytest

from human_perception.pointing_geometry import (
    DEFAULT_CANDIDATES,
    angle_degrees,
    parse_candidates,
    rank_candidates,
    semantic_arm_label,
)


def test_default_candidate_map_uses_unity_scene_map_coordinates():
    candidates = parse_candidates(
        '{"TargetObjectA":[-6.0,-1.3,0.28],"TargetObjectB":[-6.0,1.3,0.28]}'
    )
    assert candidates == DEFAULT_CANDIDATES


def test_observed_left_and_right_rays_select_their_scene_targets():
    eye = (-4.15, 0.0, 1.76)
    left_ray = (-0.3814, -0.6125, -0.6924)
    right_ray = (-0.3969, 0.6150, -0.6814)

    left_ranking = rank_candidates(eye, left_ray, DEFAULT_CANDIDATES)
    right_ranking = rank_candidates(eye, right_ray, DEFAULT_CANDIDATES)

    assert left_ranking[0][0] == "TargetObjectA"
    assert left_ranking[0][1] == pytest.approx(20.9, abs=0.2)
    assert right_ranking[0][0] == "TargetObjectB"
    assert right_ranking[0][1] == pytest.approx(19.9, abs=0.2)
    assert angle_degrees(left_ray, right_ray) > 50.0


@pytest.mark.parametrize(
    ("landmark_arm", "expected"),
    (("right", "left"), ("left", "right")),
)
def test_scene_calibration_corrects_reversed_landmark_side_labels(landmark_arm, expected):
    assert semantic_arm_label(landmark_arm, swap_sides=True) == expected
    assert semantic_arm_label(landmark_arm, swap_sides=False) == landmark_arm
