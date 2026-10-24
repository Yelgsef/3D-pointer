# Next Milestone Report — Robot RGB Perception Foundation

Audit date: 2026-10-24. The Unity Editor and ROS 2 Jazzy runtime were inspected
through the connected Unity bridge and live ROS topics.

## Phase 0 — Current-state audit

**Status: PASS.** The initial audit recorded the active scene, ROS connection,
existing movement/navigation scripts, available assets, camera setup, and
missing perception capabilities before implementation.

- Files: `docs/PROGRESS_AUDIT.md`.
- Scene objects: inspected only; no objects changed.
- Validation/evidence: Unity hierarchy and runtime inspection, ROS topic
  registration, script and asset inspection. Commit `136b737`.
- Limits: the initial audit described the pre-milestone primitive human and
  absent camera/perception publishers; the same audit now includes current
  phase status below.

## Phase 1 — Rigged HumanTarget

**Status: PASS.** `HumanTarget` now has a locally generated skinned Humanoid
visual, a valid Avatar, and controlled left/right pointing poses. The existing
RobotObserver and F1-F7 controls remain in the scene; F8/F9 select pointing
poses.

- Files: `Assets/Characters/HumanTarget_Rigged.fbx`,
  `Assets/Animations/HumanTargetGestures.controller`,
  `Assets/Scripts/HumanTargetGestureController.cs`,
  `Assets/Scripts/RobotObserverScenarioControls.cs`, editor installation and
  generation scripts, scene, and evidence images.
- Scene objects: `HumanTarget` / `HumanVisual`, its Animator and
  `SkinnedMeshRenderer`, `RobotObserverScenarioControls`, `TargetObjectA`, and
  `TargetObjectB`. `RobotObserver` movement was preserved.
- Validation: Unity compilation completed, Play Mode showed the human in the
  POV texture, Humanoid Avatar inspection found 22 bones and one skinned mesh,
  and F1-F9 poses/scenarios were exercised. Evidence includes left/right pose,
  F2, and F4 screenshots. Commit `36d3347`.
- Limits: the original low-poly character is stylized. Gestures are controlled
  poses, not computer-vision recognition.

## Phase 2 — ROS RGB camera and CameraInfo

**Status: PASS.** The existing `RobotEyeCamera` and `RobotEyeRT` publish real
camera pixels and matching pinhole calibration; the live preview remains
unchanged.

- Files: `Assets/Scripts/ROS/RobotCameraPublisher.cs`, scene component
  reference, `scripts/capture_robot_camera.py`, camera documentation and ROS
  image evidence.
- Scene objects: added `RobotCameraPublisher` to the existing
  `RobotObserver/CameraMount/RobotEyeCamera`; no second camera or texture.
- Validation: ROS registered one publisher for `/camera/image_raw` and
  `/camera/camera_info`. Samples verified paired timestamps/frame IDs,
  `rgb8`, 640 x 360, 1920-byte rows, top-left row order, RGB channels, and
  `fx=fy=577.9135`, `cx=320`, `cy=180`. F2 was out of view, F4 camera view
  showed the target, and F5 showed the occluder hiding it. Measured rates varied
  around the 5 Hz setting with Editor scheduling. Commit `e61a122`.
- Limits: UTC/system time only; no simulated ROS clock. The optical TF is not
  published. CameraInfo assumes square pixels and no lens distortion.

## Phase 3 — RGB detection and pose baseline

**Status: PASS on the existing stylized simulation scene.** MediaPipe Pose
Solutions 0.10.21 combines the BlazePose person detector with the full landmark
model. It runs on CPU with TFLite XNNPACK and publishes predictions separately
from any Unity truth.

- Files: ROS packages `human_perception` and `human_perception_msgs`, pinned
  perception requirements and venv scripts, `docs/PERCEPTION_BASELINE.md`,
  evaluation JSON, and debug images.
- Scene objects: none modified; the node subscribes only to
  `/camera/image_raw`.
- Validation: all four ROS workspace packages built. F1/F4/F6/F8 each detected
  20/20 frames; F2/F3/F5 each produced 0/20 detections. F1 important-keypoint
  visibility averaged 0.957 with 28.06 ms inference; F4 averaged 0.928 with
  28.93 ms. Camera delivery was 4.7-5.1 Hz. The separate debug images show
  bounding boxes and skeleton keypoints. Commit `f2a2a5b`.
- Limits: one stylized person only; no real-world performance claim. Detection
  confidence is a landmark-visibility proxy, pose z is relative, and
  `vision_msgs` is unavailable in this ROS installation, so the package uses
  its own message interfaces.

## Phase 4 — RGB-D and ground-truth design

**Status: PASS for documentation only.** Current Unity is Built-in Render
Pipeline. `RobotEyeRT` has a depth-stencil attachment, but the camera's depth
texture mode is `None` and no depth output is consumed.

- Files: `docs/RGBD_AND_GROUND_TRUTH_PLAN.md` and progress audit.
- Scene objects: inspected only; no depth camera, texture, or truth publisher
  added.
- Validation: inspected GraphicsSettings, RenderTexture graphics/depth formats,
  camera depth settings, near/far planes, and current rig bones. Commit
  `51ee209`.
- Limits: depth, TF, truth topics, metric 3D pose, pointing rays, and candidate
  ranking remain unimplemented. The scene scale's one-unit-equals-one-meter
  assumption has not been independently calibrated.

## Phase 5 — Documentation and regression validation

**Status: PASS.** Older setup and architecture documents now describe the
current RobotObserver + HumanTarget design and separate present features from
future work.

- Files: `RobotCameraPublisher.cs` and `RobotPosePublisher.cs` (publisher
  recovery after Editor script reload), ROS shutdown guards in `pose_detector.py`,
  `evaluate.py`, and `capture_robot_camera.py`, F7 evaluation/debug evidence,
  `HUMAN_TARGET_SETUP.md`, `ROS_CAMERA_PIPELINE.md`,
  `PERCEPTION_BASELINE.md`, `RGBD_AND_GROUND_TRUTH_PLAN.md`,
  `NEXT_MILESTONE_REPORT.md`, `PROGRESS_AUDIT.md`, and reconciled
  `ROBOT_OBSERVER_SETUP.md`, `ROS2_UNITY_SETUP.md`,
  `UNITY_SCENE_INSPECTION.md`, and `IMPLEMENTATION_REPORT.md`.
- Scene objects: no saved scene objects changed during regression; inspected
  live camera, preview, ROS connection, robot control/publisher, target rig,
  and scenario control references.
- Validation: forced Unity script compilation completed with
  `failed=false`, `errors=[]`, and `compilationFailed=false`; after restarting
  Play Mode the paired camera stream and pose publisher recovered without
  further runtime exceptions. Full `colcon build --symlink-install` succeeded for endpoint,
  navigation, perception messages, and perception node. Navigation unit tests:
  `6 passed`. `/cmd_vel` had one Unity subscriber; a 0.4 m/s, 1-second
  command moved ROS pose X from -9.000 to -8.680. `/robot/pose` rate was
  9.68 Hz. A navigation goal at -8.20 was reached at -8.342, within 0.15 m.
  Camera delivery measured 4.83 Hz. F1-F7 scenario controller behaviors were
  exercised in Unity; F4 ended at zero heading error, F5 placed the obstacle,
  F6 restored transforms, and F7 selected Idle. F7 and F8 perception each
  detected 20/20 frames.
- The perception package was rebuilt after guarding its ROS shutdown call;
  starting the node and sending SIGINT exited cleanly with code 0.
- Evidence: `docs/evidence/ros-camera-*.png`,
  `docs/evidence/perception-f1.json` through `perception-f8-pointing.json`,
  and debug images for F1, F4, and F7.
- Limits: the Unity keyboard simulator did not change scene transforms during
  this final session; scenario methods were invoked through Unity runtime for
  verification. The existing endpoint build emits setuptools dash-option
  deprecation warnings but succeeds.

## Final state

The requested foundation now runs from the existing RobotEye preview into a
ROS RGB camera and MediaPipe detection/pose pipeline. The evidence supports the
stylized simulation scenarios listed above; it does not support claims about
real-world accuracy. RGB-D, simulation ground truth, camera TF, and
pointing-object resolution remain future research milestones.
