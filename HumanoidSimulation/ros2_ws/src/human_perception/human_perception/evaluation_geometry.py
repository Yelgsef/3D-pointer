"""Offline-only comparisons between predictions and simulation truth."""

from human_perception.pointing_geometry import angle_degrees, semantic_arm_label


GROUND_TRUTH_JOINTS = (
    "head_reference",
    "left_shoulder", "right_shoulder",
    "left_elbow", "right_elbow",
    "left_wrist", "right_wrist",
)


def keypoint_error_metrics(predicted_points, truth_points, swap_sides=True):
    """Compare valid map-frame predictions against the ordered truth joints."""
    predicted_head = None
    eyes = [predicted_points[name] for name in ("left_eye", "right_eye")
            if name in predicted_points]
    if eyes:
        predicted_head = tuple(sum(point[i] for point in eyes) / len(eyes)
                               for i in range(3))
    elif "nose" in predicted_points:
        predicted_head = predicted_points["nose"]

    errors = {}
    for truth_name in GROUND_TRUTH_JOINTS:
        if truth_name == "head_reference":
            prediction = predicted_head
        else:
            side, joint = truth_name.split("_", 1)
            prediction_side = semantic_arm_label(side, swap_sides)
            prediction = predicted_points.get(f"{prediction_side}_{joint}")
        truth = truth_points.get(truth_name)
        if prediction is None or truth is None:
            errors[truth_name] = None
            continue
        errors[truth_name] = sum(
            (prediction[i] - truth[i]) ** 2 for i in range(3)
        ) ** 0.5

    valid_errors = [error for error in errors.values() if error is not None]
    return {
        "errors_m": errors,
        "valid_count": len(valid_errors),
        "valid_rate": len(valid_errors) / len(GROUND_TRUTH_JOINTS),
        "mean_error_m": sum(valid_errors) / len(valid_errors) if valid_errors else None,
    }


def pointing_angular_error(predicted_direction, truth_direction):
    """Return the angle between map-frame predicted and truth rays."""
    return angle_degrees(predicted_direction, truth_direction)
