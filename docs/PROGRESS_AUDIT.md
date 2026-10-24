# Progress Audit

Audit date: 2026-10-24. Repository checkpoint: `f3e10a1` (`Remove root instructions file`).

## Instruction 4 baseline audit

Audit date: 2026-10-24. This audit is against commit `1253bc8`, the active
`HumanoidSimulation` scene, the running Unity Editor, and the live ROS 2 graph.
No scene generator was run and no scene objects were changed during this
audit.

| Requirement | Status | Evidence / discrepancy |
| --- | --- | --- |
| Existing RGB and CameraInfo | PASS | Active scene has one `RobotCameraPublisher` on the existing `RobotEyeCamera`, using `RobotEyeRT` at 640 x 360 and 5 Hz. Play Mode published `/camera/image_raw` at about 4.7 Hz. CameraInfo was live with matching `robot_camera_optical_frame`, 640 x 360, and `fx=fy=577.9135`, `cx=320`, `cy=180`. |
| Human rig and gesture source wiring | PASS | Live Unity hierarchy has `HumanTarget`, Animator, `HumanTargetGestureController`, 22-bone Armature, and one skinned mesh; scenario controls reference `TargetObjectA` and `TargetObjectB`. |
| F8/F9 input bindings | PARTIAL | `activeInputHandler: 1` enables the new Input System, and `RobotObserverScenarioControls.Update` checks `Keyboard.current.f8Key` / `f9Key`. Unity MCP `simulate_key` returned success but did not change the runtime gesture from Idle, so actual key-to-action delivery is not verified through this Editor session. |
| 2D pose schema and synchronization | PASS (code inspection) | `HumanPose2D` carries 33 named landmarks, pixel-space x/y, MediaPipe-relative z, confidences, and the source image header. MediaPipe indices used for shoulders/elbows/wrists are 11/12, 13/14, and 15/16. The pose is not metric 3D. |
| Camera calibration and frame convention | PASS for intrinsics; PARTIAL for frames | Runtime FOV is 34.6 degrees vertical; the published pinhole intrinsics match the 640 x 360 square-pixel model. The image is labeled `robot_camera_optical_frame`, but the live `/tf` graph has zero publishers, so the frame is not connected to `base_link` or `map`. |
| Depth capture and render pipeline | BLOCKED before implementation | Unity reports `depthTextureMode=None`; the project uses Built-in Render Pipeline and the existing RenderTexture depth attachment is not an exported depth image. No metric depth topic exists yet. |

The Editor entered Play Mode for the RGB and calibration checks. Runtime
evidence confirms the old RGB pipeline is alive, but also exposes a critical
dependency for 3D work: no TF publisher exists. The next phase must validate a
depth capture path against known distances before adding TF, ground truth, or
3D inference dependencies.

### Instruction 4 Phase 1 — Metric depth camera

Status: **PASS** for simulated metric axial depth capture and message
synchronization.

- Extended the existing `RobotCameraPublisher`; no second camera or RGB
  RenderTexture was added. The existing preview remains sourced from
  `RobotEyeRT`.
- Unity compilation completed with `failed=false`, `errors=[]`, and
  `compilationFailed=false`. Runtime confirms `DepthTextureMode.Depth`, a
  found linearization shader, and RFloat render-target support.
- `/camera/depth/image_raw` publishes `sensor_msgs/msg/Image` with `32FC1`,
  640 x 360, 2560-byte rows, meters along camera optical Z, zero invalid
  pixels, and the same timestamps/frame IDs as RGB and CameraInfo.
- Known-plane errors were 0.00002 m at 2 m, 0 m at 4 m, and 0.00004 m at 6 m.
  A rotated-camera test measured 3.99994 m at 4 m. A magenta RGB plane paired
  with 5.99996 m depth at the same pixel and timestamp. The existing F5
  obstacle read about 1.98 m in front of the human at 4.72 m; a controlled
  near/far plane edge switched directly between 3 m and 6 m.
- A normal scene capture had 20,398 zero-valued pixels out of 230,400. Runtime
  validation objects were removed and scenario transforms reset; the saved
  scene remained clean.

Limitations: Unity units are assumed to be meters and have not been calibrated
against physical measurements. Although configured for 5 Hz, short observed
delivery windows varied around 4.0 Hz RGB and 2.4 Hz depth; sustained rate
needs further performance tuning. The camera depth pass does not include
transparent or custom materials without a suitable depth pass. TF is still
absent; this is addressed by Phase 2.

### Instruction 4 Phase 2 — ROS coordinate frames

Status: **PASS** for the connected `map → base_link →
robot_camera_optical_frame` tree and tested coordinate conversion.

- `RobotPosePublisher` publishes the dynamic `map → base_link` transform on
  `/tf` from the observer's actual Unity Transform and the camera mount from
  the actual `RobotEyeCamera` Transform. Unity-to-ROS conversion uses FLU for
  the body and ROS optical axes (right/down/forward) for the camera.
- The ROS TCP Endpoint does not retain static messages for late subscribers.
  Added `unity_tf_bridge` to relay `/unity/tf_static` to `/tf_static` using
  `RELIABLE` and `TRANSIENT_LOCAL` QoS. Late-subscriber echo received the
  transform.
- Runtime `tf2_echo map robot_camera_optical_frame` connected successfully.
  At the initial pose the camera origin was `(-8.88, 0, 1.4)` m; a point 2 m
  along optical Z transformed to map `(-6.88, 0, 1.4)` and projected to the
  CameraInfo principal point `(320, 180)`.
- `/tf` matched `/robot/pose`. A 0.4 m/s command for 3 seconds moved the base
  from `(-9, 0, 0)` to `(-8.258769, 0, 0.01)` in map coordinates. The scene
  was reset after the motion check.
- Four coordinate conversion and projection tests pass; the existing six
  navigation tests also pass. Full ROS workspace build completed for five
  packages. Unity compilation completed without errors.
- Evidence: `docs/TF_FRAME_CONVENTIONS.md` and
  `docs/evidence/tf-validation.json`.

Limitation: transform stamps use system UTC, matching the current camera
publisher; simulated ROS time is not enabled. Camera rotation and motion were
checked during the runtime phase, but should remain part of regression checks
as the 3D pose pipeline is added.

### Instruction 4 Phase 3 — Separate simulation ground truth

Status: **PASS** for publishing Unity simulation labels on isolated topics and
verifying that the current perception node does not consume them.

- Added `SimulationGroundTruthPublisher` to the existing scene's
  `ROSConnection` object. It publishes Unity bone positions, stable-order
  Target A/B poses, an eye-to-wrist simulation ray, gesture arm/target labels,
  and a camera frustum/raycast visibility sample. It uses actual rig transform
  names when a Humanoid shoulder mapping is absent; the eye reference is
  explicitly derived from the head bone.
- Runtime graph exposed all five `/simulation/ground_truth/...` topics.
  `/human_joints` published seven positions stamped in `map`; observed head
  reference was `(-4.0, 0.0, 1.76)` m. `/objects` published both registered
  candidates in stable order. Camera visibility reported `true` for the visible
  scenario.
- Direct controller trials produced `left / TargetObjectA`, `right /
  TargetObjectB`, and `none / empty` in Idle. Unity compilation succeeded with
  no errors; the scene was saved with the publisher attached and the runtime
  gesture was restored to Idle.
- Independence check: `human_perception/pose_detector.py` subscribes only to
  `/camera/image_raw`; no ground-truth topic is referenced by perception.
- Interface and evidence: `docs/GROUND_TRUTH_INTERFACE.md` and
  `docs/evidence/ground-truth-validation.json`.

Limitation: visibility is a single head-height sample checked against the
camera frustum and first raycast hit, not a pixel mask. Ground-truth messages
are intentionally evaluation inputs only and must not be wired into the
3D-pose or pointing estimator.

This audit was made against the current Unity Editor, active scene, ROS 2 endpoint, source tree, and installed Python tools. Older documents that described the humanoid-as-robot architecture have been reconciled with the current scene.

## Current verified state

| Feature | Status | Evidence |
| --- | --- | --- |
| Independent `RobotObserver` and `HumanTarget` | PASS | Unity hierarchy shows separate roots. Observer movement/publishing scripts remain on `RobotObserver`; the human has its own collider, Animator, gesture controller, and rigged visual. |
| Robot camera and live preview | PASS | `RobotEyeCamera` remains under `RobotObserver/CameraMount`; 34.6-degree vertical FOV, 57.95-degree horizontal FOV, and 640 x 360 `RobotEyeRT` assigned to camera and preview. |
| Main camera and audio | PASS | `Main Camera` follows `RobotObserver`; one AudioListener remains active. |
| `/cmd_vel` and `/robot/pose` | PASS | Unity has one `/cmd_vel` subscriber and one `/robot/pose` publisher. A 0.4 m/s command moved the robot 0.32 m; ROS measured the pose topic at 9.68 Hz after compilation and Play Mode restart. |
| Waypoint navigation | PASS | Live goal at ROS X -8.20 stopped at -8.342, 0.142 m away within the 0.15 m tolerance. Six navigation tests pass. |
| Rigged human and gestures | PASS | Original 22-bone FBX, valid Humanoid Avatar, one `SkinnedMeshRenderer`, and detected left-point pose in F8. No third-party model downloaded. |
| ROS RGB and CameraInfo | PASS | One publisher per topic; 640 x 360 `rgb8`, correct paired metadata, and `fx=fy=577.9135`, `cx=320`, `cy=180`. Current delivery measured at 4.76 Hz. |
| Human detection and pose | PASS on this stylized simulation | F1/F4/F6/F7/F8 each detected 20/20 frames; F2/F3/F5 each had 0/20 false detections. Mean F1 important-keypoint visibility 0.957; inference latency 28.06 ms. |
| Unity compilation and Play Mode | PASS | Forced compilation completed with no errors. After a Play Mode restart, the camera and pose publishers ran without runtime exceptions. |
| RGB-D, ground truth, optical TF | PARTIAL / planned | Design is documented. No depth image, simulation truth interface, or camera TF publisher has been implemented. |

## Evidence locations

- Scene: `HumanoidSimulation/Assets/Scenes/HumanoidSimulation.unity`
- Existing observer configuration: `HumanoidSimulation/Assets/Editor/ConfigureRobotObserver.cs`
- Existing scenario controls: `HumanoidSimulation/Assets/Scripts/RobotObserverScenarioControls.cs`
- ROS movement and pose: `HumanoidSimulation/Assets/Scripts/ROS/RosAnimationController.cs`, `RobotPosePublisher.cs`
- Existing animation controller: `HumanoidSimulation/Assets/Animations/HumanoidLocomotion.controller`
- Navigation: `HumanoidSimulation/ros2_ws/src/humanoid_navigation/humanoid_navigation/controller.py`
- Earlier scene/ROS documentation: `docs/UNITY_SCENE_INSPECTION.md`, `docs/ROS2_UNITY_SETUP.md`, `docs/IMPLEMENTATION_REPORT.md`

## Documentation reconciliation

The older documents have been updated to describe `RobotObserver` + `HumanTarget`, the real ROS camera, the perception node, and the future-only RGB-D/truth work. `docs/IMPLEMENTATION_REPORT.md` now distinguishes the original controller/navigation work from the current perception milestone.

## Phase plan

0. **PASS**: Audit the live repository, scene, ROS connection, and current implementation before changes.
1. **PASS**: Replace the primitive human visual with an original, locally generated skinned humanoid rig and functional left/right pointing poses. No third-party character asset was downloaded.
2. **PASS**: Publish the existing RobotEyeRT as `sensor_msgs/Image` and matching `CameraInfo`; verify pixels, metadata, frame IDs, and runtime rate over ROS.
3. **PASS**: Add a reproducible human detector and pose estimator using documented model weights; keep predictions separate from Unity ground truth.
4. **PASS**: Document RGB-D and ground-truth extension points without presenting them as implemented.
5. **PASS**: Reconcile older docs and run the required Unity, ROS, perception, navigation, camera, and scenario regression checks.

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

## Phase 5 result

Status: **PASS** for documentation reconciliation and required regression checks.

- Updated `HUMAN_TARGET_SETUP.md`, `ROBOT_OBSERVER_SETUP.md`, `ROS2_UNITY_SETUP.md`, `UNITY_SCENE_INSPECTION.md`, and `IMPLEMENTATION_REPORT.md` to use the current architecture and topic names. Added `NEXT_MILESTONE_REPORT.md` with the requested phase-by-phase status, files, scene components, test evidence, and limits.
- Updated `RobotCameraPublisher` and `RobotPosePublisher` to initialize/register their ROS publishers on enable and recover their pools after an Editor script-domain reload. Forced Unity script compilation completed with `failed=false`, `errors=[]`, and `compilationFailed=false`; stopping and re-entering Play Mode restored the ROS TCP graph without further runtime exceptions.
- Runtime inspection confirmed `RobotEyeCamera`, `RobotEyeRT`, POV preview, ROSConnection, `/cmd_vel`, `/robot/pose`, and F1-F9 control components. After compilation and a Play Mode restart, ROS again received paired camera topics and `/robot/pose` at about 9.68 Hz.
- Exercised F1-F7 behavior through the same scenario controller methods in Unity runtime: F1/F2/F3 positions, F4 ended at 0-degree heading error, F5 placed the 3 m obstacle at the observer/human midpoint, F6 restored initial transforms and obstacle scale, and F7 selected the Idle clip/gesture. F8 was also recognized by the perception model. Synthetic keyboard injection did not update the transforms in this final Editor session, so scenario methods were invoked directly for this regression.
- Full `colcon build --symlink-install` succeeded for all four packages. The existing navigation test suite reported `6 passed`. The endpoint exposed all expected camera, perception, command, pose, and navigation topics with the expected message types.
- After adding an `rclpy.ok()` shutdown guard, `human_perception` rebuilt and exited cleanly on SIGINT with process exit code 0.
- `/cmd_vel` had one Unity subscriber. A 1-second 0.4 m/s command changed ROS pose X from -9.000 to -8.678; `/robot/pose` measured 9.68 Hz. The navigation node accepted a map goal at -8.20 and stopped at -8.342, inside its 0.15 m tolerance. `/camera/image_raw` measured 4.83 Hz.
- Perception evaluation JSON and debug images for F1-F8 are under `docs/evidence/`; ROS camera PNGs and paired CameraInfo checks are also recorded there. No changes were made to the waypoint implementation.

Limitations: keyboard simulation itself was not confirmed in this final Editor session; the scenario controller's runtime actions were verified directly and the same visible/out-of-view/occlusion results were independently observed through the ROS camera/perception stream. RGB-D, optical TF, simulation ground truth, metric 3D pose, and pointing-object inference remain unimplemented.
