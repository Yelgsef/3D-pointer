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

Unity uses +Z forward, +X right, and +Y up. ROS pose conversion uses FLU
(X forward, Y left, Z up). The image header is labeled
`robot_camera_optical_frame`; static camera TF is not currently published.

## Prerequisites and build

- Ubuntu 24.04 with ROS 2 Jazzy at `/opt/ros/jazzy`.
- `colcon`, `rosdep`, and Git.
- Unity `6000.6.3f1` with project packages resolved.
- Python 3.12 and an interactive TTY for keyboard teleoperation.

Build the ROS workspace and fetch the Unity ROS TCP Endpoint source if needed:

```bash
./scripts/setup_ros_workspace.sh
```

This builds the endpoint, navigation package, custom perception messages, and
perception package. Workspace outputs and the local endpoint checkout are
ignored by Git.

## Start Unity and ROS

Start the endpoint in one terminal:

```bash
./scripts/start_ros_endpoint.sh
```

Open `HumanoidSimulation/` in Unity Hub, open
`Assets/Scenes/HumanoidSimulation.unity`, and enter Play Mode. `ROSConnection`
connects to port 10000.

## Human perception

Prepare the project-local ROS-compatible environment and run the model node in
another terminal:

```bash
./scripts/setup_perception_venv.sh
./scripts/start_human_perception.sh
```

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
| `/human_perception/detection` | `human_perception_msgs/msg/HumanDetection2D` | ROS internal | Detection flag and bounding box |
| `/human_perception/pose` | `human_perception_msgs/msg/HumanPose2D` | ROS internal | 33 RGB-predicted 2D keypoints |
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
