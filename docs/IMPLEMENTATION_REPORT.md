# Implementation Report

## Current status

The Unity scene now uses the `RobotObserver` + `HumanTarget` architecture. The
robot movement, pose feedback, and waypoint navigation from the original ROS 2
simulation remain in place. The current milestone adds a rigged human with
controlled pointing poses, a live ROS RGB/CameraInfo feed from the existing
robot camera, and a MediaPipe RGB human detector/pose estimator.

| Area | Status | Evidence |
| --- | --- | --- |
| Unity / ROS 2 Jazzy connection | PASS | Unity Play Mode registers the existing ROS topics through the TCP endpoint on `127.0.0.1:10000`. |
| `/cmd_vel` and `/robot/pose` | PASS | A 0.4 m/s command moved the observer by 0.32 m; `/robot/pose` measured about 9.2 Hz. |
| Waypoint navigation | PASS | A live `map` goal was accepted and reached within the 0.15 m tolerance. Six navigation math tests pass. |
| Rigged human and pointing poses | PASS | Humanoid Avatar, 22-bone armature, one skinned mesh, and F8/F9 controlled pointing poses. |
| RGB and CameraInfo | PASS | Real RobotEyeRT frames arrive at about 5 Hz with matching metadata and FOV-derived intrinsics. |
| Human RGB detection and pose | PASS on the stylized scene | F1/F4/F6/F8 detected 20/20; F2/F3/F5 had zero detections in 20/20. |
| RGB-D, simulation truth, object inference | NOT IMPLEMENTED | Design only: `RGBD_AND_GROUND_TRUTH_PLAN.md`. |

## Architecture

- `RobotObserver` owns the CharacterController, `/cmd_vel` subscriber,
  `/robot/pose` publisher, F1-F9 scenario controls, and `CameraMount`.
- The existing `RobotEyeCamera` renders to `RobotEyeRT`; `RobotCameraPublisher`
  publishes that image on `/camera/image_raw` and matching intrinsics on
  `/camera/camera_info`.
- `HumanTarget` is an independent skinned Humanoid with a CharacterController,
  Animator, and procedural left/right arm pose controller.
- The ROS 2 `human_perception` node reads only RGB and publishes a detection,
  33 image keypoints, and a separate debug image. It does not read Unity target
  transforms or ground-truth annotations.
- The ROS `humanoid_navigation` package remains a proportional goal follower
  using `/goal_pose`, `/robot/pose`, `/goal_cancel`, and `/cmd_vel`.

## Validation evidence

- ROS image metadata: `rgb8`, 640 x 360, `step=1920`, 691200 data bytes,
  frame ID `robot_camera_optical_frame`, paired timestamps, and intrinsics
  `fx=fy=577.9135`, `cx=320`, `cy=180`.
- Topic rates measured during final regression: `/camera/image_raw` 4.76 Hz
  and `/robot/pose` 9.17 Hz.
- F1 live control moved the observer from ROS X -9.0 to -8.68. Navigation then
  accepted a goal at -8.20 and stopped at -8.325, 0.125 m from target.
- `colcon build --symlink-install` built all four ROS packages. Navigation
  tests: `6 passed`.
- Perception JSON and debug images are in `docs/evidence/`. See
  `NEXT_MILESTONE_REPORT.md` for phase-by-phase test details.

## Limitations

- The human is original, low-poly, and stylized. Synthetic-scene results do not
  establish performance on real people or other render styles.
- The camera frame has no published TF. CameraInfo assumes square pixels and
  no lens distortion. Timestamps use UTC/system time, not ROS simulation time.
- MediaPipe detection confidence is a visibility proxy. Pose z is relative,
  not metric. `vision_msgs` is not installed, so detections use the project's
  `human_perception_msgs` interface package.
- There is no depth stream, simulation ground-truth topic, metric 3D pose,
  pointing recognition, or object-resolution model. These are described as
  future work, not implemented features.
- Waypoint control remains proportional and does not avoid obstacles.

## Start commands

```bash
./scripts/start_ros_endpoint.sh
# Open HumanoidSimulation/Assets/Scenes/HumanoidSimulation.unity in Unity and enter Play Mode.
./scripts/setup_perception_venv.sh
./scripts/start_human_perception.sh
```

Use either `scripts/start_teleop.sh` or `scripts/start_navigation.sh` as the
robot movement source; do not run both concurrently. See `ROS2_UNITY_SETUP.md`
for the full setup.
