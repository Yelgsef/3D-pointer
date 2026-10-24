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

0. **PASS**: Audit the live repository, scene, ROS connection, and current implementation before changes.
1. **PASS**: Replace the primitive human visual with an original, locally generated skinned humanoid rig and functional left/right pointing poses. No third-party character asset was downloaded.
2. **PASS**: Publish the existing RobotEyeRT as `sensor_msgs/Image` and matching `CameraInfo`; verify pixels, metadata, frame IDs, and runtime rate over ROS.
3. **PASS**: Add a reproducible human detector and pose estimator using documented model weights; keep predictions separate from Unity ground truth.
4. **PASS**: Document RGB-D and ground-truth extension points without presenting them as implemented.
5. **PENDING**: Reconcile older docs and run the required Unity, ROS, perception, navigation, camera, and scenario regression checks.

## Phase 1 result

Status: **PASS** for the rigged target and controllable arm poses.

- `HumanTarget` retains its root, `CharacterController`, and `Animator`; its visual is now an original Blender-generated FBX with one `SkinnedMeshRenderer`, 4,818 vertices, 22 bones, and a valid Humanoid Avatar with 16 mapped bones.
- `HumanTargetGestureController` restores the imported bind pose after Animator evaluation, then procedurally solves one two-segment arm toward its configured target. This avoids Unity's empty Humanoid Idle clip replacing the model's authored rest pose. Left/right arm bends include an elbow plane and are clamped to the arm's reachable length.
- `TargetObjectA` and `TargetObjectB` are scene objects. F8 selects the left-arm pose for A; F9 selects the right-arm pose for B. The original F1-F7 scenario paths remain in the same controls component.
- Unity 6000.6.3f1 script compilation completed with `failed=false`, no errors, and no compilation failure. Play Mode confirmed the human is rendered in `RobotEyeRT` / `RobotPOVCanvas`.
- Injected Input System states exercised F1-F7: F1/F2/F3 moved and oriented the human, F4 turned the observer to within 0.2 degrees of the human, F5 placed the occluder, F6 restored scenario transforms, and F7 selected Idle. F8/F9 selected the expected gesture and target.
- Captures: `docs/evidence/human-target-left-point.png`, `docs/evidence/human-target-right-point.png`, `docs/evidence/scenario-f2-out-of-view.png`, and `docs/evidence/scenario-f4-turned-to-human.png`. The F2 Robot POV omits the person; after F4 turns, the person enters the preview.

Limitations: the character is intentionally stylized and low-poly. The gesture controller provides arm pointing poses; it does not estimate pointing targets from images or implement a full-body animation system.

## Phase 2 result

Status: **PASS** for the ROS RGB image and CameraInfo pipeline.

- `RobotCameraPublisher` on the existing `RobotEyeCamera` publishes the existing 640 x 360 RenderTexture at a configured 5 Hz. It uses asynchronous GPU readback when available and a reusable synchronous fallback, converts bottom-left Unity rows to top-left ROS rows, and pools outgoing messages.
- ROS 2 Jazzy registered exactly one publisher on each required topic: `/camera/image_raw` (`sensor_msgs/msg/Image`) and `/camera/camera_info` (`sensor_msgs/msg/CameraInfo`). A capture subscriber validated paired timestamps and frame IDs, `rgb8`, dimensions 640 x 360, step 1920, 691200 bytes, zero distortion, and intrinsics `fx=fy=577.9135`, `cx=320`, `cy=180`.
- A 12-frame run previously measured 5.198 Hz; current F4/F5 image samples measured 5.9/8.7 Hz over three-frame windows. Rate varies with Unity Editor scheduling; the component enforces a 5 Hz minimum interval, not real-time scheduling.
- Camera evidence confirms the human absent in F2, visible after the observer turns toward that target, and fully occluded in F5. F4 turning and F5 occlusion were exercised through the live Unity scenario controller; this runtime session's synthetic keyboard injection did not change transforms, so the corresponding controller actions were invoked directly for the camera captures.
- Unity's RGB image buffer is top-left-origin RGB and its header names the ROS optical frame convention. A static TF for the camera optical frame is not yet published and is documented as a required geometry extension.

Evidence: `docs/evidence/ros-camera-sample.png`, `ros-camera-f2-out-of-view.png`, `ros-camera-f4-turned-to-human.png`, and `ros-camera-f5-occluded.png`; see `docs/ROS_CAMERA_PIPELINE.md` for reproduction details and frame conventions.

Limitations: ROS system/UTC time is used; simulated ROS time is unsupported. CameraInfo assumes square pixels and no distortion. The publisher has no backpressure beyond avoiding overlapping GPU readbacks, and short sample rates vary by editor scheduling. No depth stream is implemented.

## Phase 3 result

Status: **PASS** for the single-human RGB detection and 2D pose baseline on the existing stylized Unity target.

- Added `human_perception_msgs` interfaces and the Python `human_perception` package. The node reads only `/camera/image_raw`, runs MediaPipe Pose Solutions 0.10.21 (BlazePose detection plus full pose landmarks), and publishes a per-frame detection/ROI, 33 named keypoints and visibility scores, and a separate debug image. No Unity transform, target ID, or ground-truth skeleton is read by this node.
- Model assets are bundled with the pinned MediaPipe wheel. The package is Apache-2.0. Input is 640 x 360 RGB; TFLite XNNPACK runs on CPU. MediaPipe's detector and tracking thresholds are 0.40; the node's 0.50 detection gate uses mean visibility over shoulders, elbows, wrists, hips, knees, and ankles.
- Added a project `.venv` setup that retains system ROS Jazzy `rclpy`, Ubuntu OpenCV 4.6, and NumPy 1.26 while installing pinned MediaPipe dependencies locally. `colcon build --symlink-install --packages-up-to human_perception` completed successfully.
- Live paired-image evaluation: F1 detected 20/20 frames (mean important-keypoint visibility 0.957, mean inference latency 28.06 ms); F4 detected 20/20 (0.928, 28.93 ms); F6 detected 20/20; F8 pointing detected 20/20. F2, F3, and F5 each had 0/20 detections and zero false positives. Camera delivery during these runs was 4.7-5.1 Hz.
- Evidence JSON is in `docs/evidence/perception-f1.json` through `perception-f6.json` plus `perception-f8-pointing.json`. `perception-debug-f1.png` and `perception-debug-f4.png` show detections/keypoints on a separate debug image; the raw camera topic remains unmodified.

Limitations: the custom `human_perception_msgs` interfaces are used because `vision_msgs` is absent from this ROS installation. Pose z is MediaPipe-relative, not metric depth. The visibility-based detection score is not a calibrated detector probability. Results only cover this stylized single-person simulation; no real-world robustness is claimed. Pointing-object inference is not implemented.

## Phase 4 result

Status: **PASS** for the requested RGB-D and ground-truth design documentation; no sensor or truth publisher was added.

- Inspected the active project settings and Unity runtime: Built-in Render Pipeline, `RobotEyeRT` at 640 x 360 with `R8G8B8A8_SRGB` color and `D24_UNorm_S8_UInt` depth-stencil, camera `depthTextureMode=None`, and 0.03/100 m near/far planes.
- Documented enabling the existing camera's depth texture or a dedicated linear-depth shader target, correct nonlinear-depth conversion, a proposed `32FC1` meter encoding, invalid-pixel convention, validation distances, and the uncalibrated one-Unity-unit-equals-one-meter assumption.
- Proposed separate `/simulation/ground_truth/...` interfaces for joints, gesture target, object poses, and camera visibility. Recorded that this rig has a head bone but no eye bones, and that camera optical TF is not published.
- Documented the future depth back-projection, TF, pointing-ray, and candidate-ranking stages. The perception node remains independent of all Unity truth.

Evidence: `docs/RGBD_AND_GROUND_TRUTH_PLAN.md`, current RenderTexture asset, `ProjectSettings/GraphicsSettings.asset`, and runtime inspection of `RobotEyeCamera.depthTextureMode` / `RobotEyeRT.graphicsFormat`.
