import math

import pytest

from humanoid_navigation.controller import compute_command, normalize_angle


def test_goal_ahead_moves_forward():
    linear, angular, reached = compute_command(0.0, 0.0, 0.0, 3.0, 0.0)
    assert linear > 0.0
    assert angular == 0.0
    assert not reached


def test_goal_left_rotates_left_before_driving():
    linear, angular, reached = compute_command(0.0, 0.0, 0.0, 0.0, 3.0)
    assert linear == 0.0
    assert angular > 0.0
    assert not reached


def test_goal_right_rotates_right_before_driving():
    linear, angular, reached = compute_command(0.0, 0.0, 0.0, 0.0, -3.0)
    assert linear == 0.0
    assert angular < 0.0
    assert not reached


def test_goal_behind_rotates_instead_of_reversing():
    linear, angular, reached = compute_command(0.0, 0.0, 0.0, -3.0, 0.0)
    assert linear == 0.0
    assert abs(angular) == 1.2
    assert not reached


def test_close_goal_stops():
    linear, angular, reached = compute_command(1.0, 2.0, 0.0, 1.1, 2.0)
    assert (linear, angular, reached) == (0.0, 0.0, True)


def test_command_limits_and_angle_wrap():
    assert normalize_angle(3.0 * math.pi) == pytest.approx(math.pi)
    linear, angular, reached = compute_command(0, 0, 0, 0, 20)
    assert linear == 0.0
    assert angular == 1.2
    assert not reached
