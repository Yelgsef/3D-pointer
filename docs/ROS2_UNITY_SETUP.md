# ROS 2 + Unity Setup

## Architecture

`HumanoidSimulation/` is the Unity 6 project. The active scene,
`Assets/Scenes/HumanoidSimulation.unity`, separates a mobile `RobotObserver`
from a rigged `HumanTarget`. Unity `6000.6.3f1` uses ROS-TCP-Connector 0.7.0 to
connect to the ROS 2 Jazzy TCP endpoint at `127.0.0.1:10000`.

`RobotObserver` subscribes to `/cmd_vel` and publishes `/robot/pose`. Its
existing RobotEyeCamera renders 640 x 360 RGB pixels to `RobotEyeRT`; the same
texture drives the live POV preview and `/camera/image_raw`. Matching
`/camera/camera_info` reports a pinhole model. A separate MediaPipe node reads
only `/camera/image_raw` and publishes human detection, 33 pose keypoints, and
a separate debug image. Predictions are not mixed with Unity ground truth.

`RobotPosePublisher` publishes `map → base_link` on `/tf`. The mounted camera
transform is published through `/unity/tf_static` and relayed to `/tf_static`
with transient-local QoS. See [TF_FRAME_CONVENTIONS.md](TF_FRAME_CONVENTIONS.md).
An isolated simulation-only publisher provides evaluation labels under
`/simulation/ground_truth/...`; its schema and independence boundary are
documented in [GROUND_TRUTH_INTERFACE.md](GROUND_TRUTH_INTERFACE.md).
`human_pose_3d` consumes the RGB-predicted 2D pose with aligned depth and
CameraInfo; see [3D_POSE_ESTIMATION.md](3D_POSE_ESTIMATION.md).

Unity uses +Z forward, +X right, and +Y up. ROS pose conversion uses FLU
(X forward, Y left, Z up). The camera optical frame uses X right, Y down, and
Z forward.

## Prerequisites and build

- Ubuntu 24.04 with ROS 2 Jazzy at `/opt/ros/jazzy`.
- `colcon`, `rosdep`, and Git.
- Unity `6000.6.3f1` with project packages resolved.
- Python 3.12 and an interactive TTY for keyboard teleoperation.

Build the ROS workspace and fetch the Unity ROS TCP Endpoint source if needed:

```bash
./scripts/setup_ros_workspace.sh
```

This builds the endpoint, navigation package, custom perception messages,
perception package, and static TF relay. Workspace outputs and the local
endpoint checkout are ignored by Git.

## Start the project

Run setup once after cloning, then start the endpoint, perception node, and
Unity Editor together:

```bash
./scripts/setup_ros_workspace.sh
./scripts/setup_perception_venv.sh
./scripts/start_all.sh
```

If Unity is installed outside `~/Unity/Hub/Editor/`, set `UNITY_EDITOR` to its
executable path before running `start_all.sh`. The script opens the
`HumanoidSimulation/` project when it can find Unity, but Unity still requires
you to open `Assets/Scenes/HumanoidSimulation.unity` if needed and enter Play
Mode. `ROSConnection` then connects to port 10000. Startup logs are saved under
`HumanoidSimulation/Logs/start_all/`.

Stop the endpoint, perception node, and Unity Editor started by the script
with:

```bash
./scripts/start_all.sh --stop
```

The command leaves processes started separately untouched. Alternatively, run
the endpoint and perception node individually with `start_ros_endpoint.sh` and
`start_human_perception.sh`.

Toggle human perception without stopping Unity or the camera endpoint:

```bash
./scripts/start_all.sh --stop-perception
./scripts/start_all.sh --start-perception
```

To start Unity and the endpoint with perception disabled, use
`./scripts/start_all.sh --no-perception`.

## Human perception

The project-local ROS-compatible environment is prepared by
`setup_perception_venv.sh`; `start_all.sh` starts the model node after setup.

The venv uses system ROS Jazzy `rclpy`, OpenCV, and NumPy, while MediaPipe
0.10.21 and its dependencies are installed locally with pinned versions. The
TFLite model runs on CPU using XNNPACK. For setup, outputs, and per-scenario
evaluation see [PERCEPTION_BASELINE.md](PERCEPTION_BASELINE.md).

## Waypoint navigation and teleoperation

Start one control source at a time. For keyboard teleoperation:

```bash
./scripts/start_teleop.sh
```

The standard `u i o / j k l / m , .` keys publish velocity commands from a
usable TTY. For click-to-goal navigation instead:

```bash
./scripts/start_navigation.sh
```

Click the ground in Unity's Game view to publish `/goal_pose`; press Escape to
cancel with `/goal_cancel`. This is a proportional controller and does not
perform obstacle avoidance.

## Topics

| Topic | Type | Direction | Purpose |
| --- | --- | --- | --- |
| `/cmd_vel` | `geometry_msgs/msg/Twist` | ROS to Unity | Observer velocity command |
| `/robot/pose` | `geometry_msgs/msg/Pose` | Unity to ROS | Observer world pose |
| `/goal_pose` | `geometry_msgs/msg/PoseStamped` | Unity to ROS | Clicked `map` goal |
| `/goal_cancel` | `std_msgs/msg/Bool` | Unity to ROS | Cancel navigation |
| `/camera/image_raw` | `sensor_msgs/msg/Image` | Unity to ROS | Real RGB camera image (`rgb8`) |
| `/camera/camera_info` | `sensor_msgs/msg/CameraInfo` | Unity to ROS | FOV-derived pinhole intrinsics |
| `/camera/depth/image_raw` | `sensor_msgs/msg/Image` | Unity to ROS | Aligned optical-axis depth (`32FC1`, meters) |
| `/tf` | `tf2_msgs/msg/TFMessage` | Unity to ROS | Dynamic `map → base_link` transform |
| `/unity/tf_static` | `tf2_msgs/msg/TFMessage` | Unity to relay | Actual camera mount transform source |
| `/tf_static` | `tf2_msgs/msg/TFMessage` | ROS relay | Transient-local `base_link → robot_camera_optical_frame` |
| `/simulation/ground_truth/human_joints` | `geometry_msgs/msg/PoseArray` | Unity to evaluation | Seven Unity joint positions, fixed order, `map` frame |
| `/simulation/ground_truth/objects` | `geometry_msgs/msg/PoseArray` | Unity to evaluation | Target A then Target B poses in `map` |
| `/simulation/ground_truth/pointing_ray` | `geometry_msgs/msg/PoseStamped` | Unity to evaluation | Simulation ray origin and direction |
| `/simulation/ground_truth/pointing_label` | `std_msgs/msg/String` | Unity to evaluation | JSON arm and intended target label |
| `/simulation/ground_truth/camera_visibility` | `std_msgs/msg/String` | Unity to evaluation | JSON single-point frustum/raycast result |
| `/human_perception/detection` | `human_perception_msgs/msg/HumanDetection2D` | ROS internal | Detection flag and bounding box |
| `/human_perception/pose` | `human_perception_msgs/msg/HumanPose2D` | ROS internal | 33 RGB-predicted 2D keypoints |
| `/human_perception/pose_3d` | `human_perception_msgs/msg/HumanPose3D` | ROS internal | Metric optical-frame keypoints with per-point validity |
| `/human_perception/debug/image` | `sensor_msgs/msg/Image` | ROS internal | Separate visualization overlay |

The scene also exposes F1-F7 scenario controls and F8/F9 pointing-pose controls.
RGB-D, metric 3D joints, camera TF, simulation ground-truth messages, and
pointing-object inference remain future work. See
[RGBD_AND_GROUND_TRUTH_PLAN.md](RGBD_AND_GROUND_TRUTH_PLAN.md).

## Verify and troubleshoot

```bash
./scripts/check_ros_topics.sh
./scripts/test_cmd_vel.sh 0.8 0.0 1.5
source /opt/ros/jazzy/setup.bash
source HumanoidSimulation/ros2_ws/install/setup.bash
python3 -m pytest HumanoidSimulation/ros2_ws/src/humanoid_navigation/test
```

To validate the RGB stream and paired CameraInfo, follow
[ROS_CAMERA_PIPELINE.md](ROS_CAMERA_PIPELINE.md). To inspect a changing pose:

```bash
source /opt/ros/jazzy/setup.bash
ros2 topic echo /robot/pose
```

If topics disappear after a Unity script reload, exit and re-enter Play Mode so
the connector opens a fresh TCP connection. If `/cmd_vel` has a subscriber but
the robot does not move, confirm a command is still being published and the
0.7 second command timeout has not expired. For the confirmed scene hierarchy
and limits, see [ROBOT_OBSERVER_SETUP.md](ROBOT_OBSERVER_SETUP.md).
