# Simulation Ground Truth Interface

`SimulationGroundTruthPublisher` publishes test labels from Unity transforms on
the `/simulation/ground_truth/...` namespace. The MediaPipe node subscribes
only to `/camera/image_raw`; prediction and ground-truth streams are separate.
Offline evaluation may join them by ROS header stamp or the UTC stamp in JSON
messages.

All positions use `map` in ROS FLU coordinates (forward, left, up), with one
Unity unit treated as one meter. Stamps use system UTC, matching the current
camera and TF sources.

| Topic | Type | Contents |
| --- | --- | --- |
| `/simulation/ground_truth/human_joints` | `geometry_msgs/PoseArray` | Seven ordered positions: head reference, left/right shoulder, left/right elbow, left/right wrist. `header.frame_id=map`. |
| `/simulation/ground_truth/objects` | `geometry_msgs/PoseArray` | Two poses in fixed order: `TargetObjectA`, `TargetObjectB`; `header.frame_id=map`. |
| `/simulation/ground_truth/pointing_ray` | `geometry_msgs/PoseStamped` | Ray origin at the derived head reference; orientation points along eye-to-wrist direction for the active gesture. `header.frame_id=map`. |
| `/simulation/ground_truth/pointing_label` | `std_msgs/String` | JSON with `stamp_utc`, `frame_id`, `arm` (`left`, `right`, or `none`) and `intended_target_id` (stable object ID or empty). |
| `/simulation/ground_truth/camera_visibility` | `std_msgs/String` | JSON with `stamp_utc`, `frame_id`, and `human_visible`. Visibility means the head-height sample is in the camera frustum and its ray is not blocked before reaching the HumanTarget collider. |

Joint order is fixed because `PoseArray` has no names field. The head reference
is the head bone position plus 0.08 m along the head's up axis; this rig has no
eye bones. Wrist positions use the rig's `LeftHand` and `RightHand` bones.
When Humanoid bone mapping omits a shoulder, the publisher resolves the actual
named rig transform instead of fabricating a root position.

These topics expose intended labels and simulation geometry for evaluation
only. Do not subscribe to them from pose reconstruction or pointing
estimation. Ground-truth visibility is a single-point frustum/raycast check,
not a per-pixel segmentation mask.
