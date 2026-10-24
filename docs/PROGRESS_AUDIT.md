# Progress Audit

Audit date: 2026-10-24. Repository checkpoint: `f3e10a1` (`Remove root instructions file`).

This audit was made against the current Unity Editor, active scene, ROS 2 endpoint, source tree, and installed Python tools. The older implementation documents describe the earlier humanoid-as-robot architecture and are not authoritative for the current scene.

## Verified baseline

| Feature | Status | Evidence |
| --- | --- | --- |
| Independent `RobotObserver` and `HumanTarget` | PASS | Unity hierarchy shows both as independent roots. `HumanTarget` has `CharacterController` and `Animator`; observer movement/publishing components are on `RobotObserver`. |
| Robot camera and live preview | PASS | Runtime inspection: `RobotEyeCamera` is under `RobotObserver/CameraMount`; vertical FOV 34.6 degrees, horizontal FOV 57.948 degrees, aspect 16:9, clip planes 0.03/100 m. `RobotEyeRT` is 640x360 and is assigned to both camera and preview RawImage. |
| Main camera and audio | PASS | `Main Camera` remains active and follows `RobotObserver`. Two cameras are active, with one AudioListener. |
| `/cmd_vel` input | PASS | With the endpoint and Unity in Play Mode, `ros2 topic info /cmd_vel --verbose` reported one Unity subscription. The subscriber component is `RosAnimationController` on `RobotObserver`; none remains on the human. |
| `/robot/pose` output | PASS | ROS reported one publisher. `ros2 topic echo --once /robot/pose` returned finite pose data for the observer at its initial Unity position. |
| Waypoint navigation | PASS | `scripts/start_navigation.sh` and `humanoid_navigation/controller.py` exist. The six navigation math tests pass (`6 passed`). Unity still registers `/goal_pose` and `/goal_cancel` publishers. |
| Rigged human target | FAIL at baseline | The human visual is the original primitive assembly under `HumanTarget/Visuals`. No FBX/OBJ/Blend/GLB model, Humanoid Avatar, or `SkinnedMeshRenderer` asset/component is present. |
| Pointing gestures | FAIL at baseline | `HumanoidLocomotion.controller` has only a locomotion blend tree with Idle/Walk/Run. No armature, pointing clip, or pointing state exists. |
| ROS camera image and CameraInfo | FAIL at baseline | Live ROS topic list has no `/camera/image_raw` or `/camera/camera_info`; project contains no corresponding generated C# message classes or publishers. `sensor_msgs/msg/Image` and `CameraInfo` are available in ROS Jazzy. |
| Human detection / pose estimation | FAIL at baseline | No perception ROS package, model weights, detector, pose node, or prediction output exists. |
| Available local tools | PARTIAL | Blender is installed; MediaPipe 0.10.21 and OpenCV 4.6.0 import in the system Python. PyTorch, ONNX Runtime, and Ultralytics are not installed. No third-party humanoid model is present in project assets. |
| Unity compilation | PASS | The connected Unity 6000.6.3f1 Editor reports no failed or pending script compilation. No relevant compile/runtime exception appeared in the current log tail. |

## Evidence locations

- Scene: `HumanoidSimulation/Assets/Scenes/HumanoidSimulation.unity`
- Existing observer configuration: `HumanoidSimulation/Assets/Editor/ConfigureRobotObserver.cs`
- Existing scenario controls: `HumanoidSimulation/Assets/Scripts/RobotObserverScenarioControls.cs`
- ROS movement and pose: `HumanoidSimulation/Assets/Scripts/ROS/RosAnimationController.cs`, `RobotPosePublisher.cs`
- Existing animation controller: `HumanoidSimulation/Assets/Animations/HumanoidLocomotion.controller`
- Navigation: `HumanoidSimulation/ros2_ws/src/humanoid_navigation/humanoid_navigation/controller.py`
- Earlier scene/ROS documentation: `docs/UNITY_SCENE_INSPECTION.md`, `docs/ROS2_UNITY_SETUP.md`, `docs/IMPLEMENTATION_REPORT.md`

## Corrections required

`docs/IMPLEMENTATION_REPORT.md` and `docs/UNITY_SCENE_INSPECTION.md` still say `Humanoid` is the robot and `Main Camera` follows it. The live scene instead uses `RobotObserver` and a separate `HumanTarget`. `docs/ROS2_UNITY_SETUP.md` also describes `/cmd_vel` animating the humanoid. These documents must be reconciled with the final architecture during the documentation phase.

## Phase plan

1. Replace the primitive human visual with an original, locally generated skinned humanoid rig and functional left/right pointing poses. No licensed third-party model was found, so authoring must avoid unlicensed downloads.
2. Publish the existing RobotEyeRT as `sensor_msgs/Image` and matching `CameraInfo`, then verify pixel order, metadata, frame IDs, and runtime rate over the existing endpoint.
3. Add a reproducible human detector and pose estimator using compatible, documented model weights; keep predictions separate from Unity ground truth.
4. Document the RGB-D and ground-truth extension points without presenting them as implemented.
5. Reconcile older docs and run the required Unity, ROS, perception, navigation, camera, and scenario regression checks.
