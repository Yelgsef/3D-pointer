"""Coordinate conversions shared by RGB-D reconstruction and TF tests."""


def unity_ruf_to_flu(point):
    """Convert a Unity right/up/forward vector to ROS forward/left/up."""
    x, y, z = point
    return z, -x, y


def flu_to_camera_optical(point):
    """Convert base_link forward/left/up coordinates to optical right/down/forward."""
    forward, left, up = point
    return -left, -up, forward


def project_optical_point(point, fx, fy, cx, cy):
    """Project an optical-frame XYZ point into pixel coordinates."""
    x, y, z = point
    if z <= 0:
        raise ValueError("Optical Z must be positive to project a point")
    return cx + fx * x / z, cy + fy * y / z
