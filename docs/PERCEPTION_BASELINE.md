# Human Perception Baseline

## Model and runtime

The ROS 2 `human_perception` package consumes only `/camera/image_raw`. It uses
Google MediaPipe Pose Solutions **0.10.21**, which combines the BlazePose person
detector with the full pose landmark model (`pose_detection.tflite` and
`pose_landmark_full.tflite`). Both model assets are included in the pinned
MediaPipe wheel; the implementation and distributed assets are Apache-2.0
licensed. No model or Unity ground-truth metadata is loaded by the node.

The camera frame is 640 x 360 RGB. MediaPipe internally scales/crops the input
for its detector and landmark networks. This baseline runs TFLite XNNPACK on the
CPU; it does not require CUDA or a GPU delegate. The ROS camera's 5 Hz input
rate is below the measured single-frame inference capacity on the development
CPU. The detector uses MediaPipe `min_detection_confidence=0.40` and
`min_tracking_confidence=0.40`. The output gate is 0.50, applied to the mean
MediaPipe visibility score for shoulders, elbows, wrists, hips, knees, and
ankles; it is a visibility proxy, not the detector model's raw probability.

The environment uses Python 3.12 in a project-local `.venv` created with
`--system-site-packages`. This keeps ROS Jazzy `rclpy`, Ubuntu OpenCV 4.6, and
NumPy 1.26 available without replacing them with incompatible pip wheels.
MediaPipe and its runtime dependencies are pinned in
`scripts/perception-requirements.txt`; its wheel is installed with `--no-deps`
to avoid replacing ROS-compatible NumPy/OpenCV. No system Python packages are
modified.

## Build and run

```bash
./scripts/setup_perception_venv.sh
source /opt/ros/jazzy/setup.bash
cd HumanoidSimulation/ros2_ws
colcon build --symlink-install --packages-up-to human_perception
cd ../..
./scripts/start_human_perception.sh
```

Keep the ROS-TCP-Endpoint running on port 10000 and Unity in Play Mode. The
node publishes:

| Topic | Type | Meaning |
| --- | --- | --- |
| `/human_perception/detection` | `human_perception_msgs/msg/HumanDetection2D` | Per-frame detection flag, confidence proxy, pixel ROI, inference latency |
| `/human_perception/pose` | `human_perception_msgs/msg/HumanPose2D` | 33 MediaPipe landmarks as pixel x/y and relative normalized z, with visibility scores |
| `/human_perception/debug/image` | `sensor_msgs/msg/Image` | Separate BGR visualization with the detection box and pose overlay |

The custom `human_perception_msgs` interfaces carry both the bounding box and
named keypoints because `vision_msgs` is not installed in this ROS image. The
debug image is a separate topic; `/camera/image_raw` is never modified. When no
pose is detected, the node publishes `detected=false` and 33 zero-valued
keypoints/confidences so each pose message has a stable schema. The z value is
MediaPipe-relative depth, not metric camera depth.

## Scenario evaluation

The evaluation node pairs image, detection, and pose messages by ROS header
timestamp. It writes machine-readable per-scenario results with camera delivery
rate, detection rate, false positives/negatives, important-keypoint visibility,
and mean inference latency. Scenario visibility expectations are supplied by
the command-line scenario label; the node never reads Unity transforms or
ground-truth skeleton data.

```bash
source /opt/ros/jazzy/setup.bash
source HumanoidSimulation/ros2_ws/install/setup.bash
PYTHONNOUSERSITE=1 .venv/bin/python -m human_perception.evaluate \
  --scenario F1 --frames 30 --output docs/evidence/perception-f1.json \
  --debug-output docs/evidence/perception-debug-f1.png
```

Repeat with `F2`, `F3`, `F4`, `F5`, `F6`, `F7`, `F8`, or `F9` for each
controlled scene. Current JSON evidence covers F1-F6 and F8. Results on this
stylized, single-person scene were: F1, F4, F6, and F8 visible in 20/20 frames;
F2, F3, and F5 had zero detections in 20/20 frames. F1 mean keypoint visibility
was 0.957 and mean inference latency was 28.06 ms; F4 mean visibility was
0.928 and latency 28.93 ms. Camera delivery in these runs ranged from 4.7 to
5.1 Hz. See the individual JSON files for per-scenario values and
`docs/evidence/perception-debug-f1.png` / `perception-debug-f4.png` for overlays.

## Limits

The MediaPipe model is a generic human pose model; this successful synthetic
scene result does not establish accuracy on real camera footage or other
stylized characters. It tracks a single pose and may retain poor estimates
under strong occlusion or truncation. Detection confidence is a landmark
visibility proxy. Pointing intent, object identity, metric 3D joints, RGB-D,
and Unity ground truth are not inferred or published by this package.
