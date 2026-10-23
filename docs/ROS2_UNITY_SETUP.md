# ROS 2 + Unity Setup

## Architecture

The Unity 6 project is `HumanoidSimulation/`; the simulation scene is
`Assets/Scenes/HumanoidSimulation.unity`. The original template scene,
`Assets/Scenes/SampleScene.unity`, remains in the project. Unity connects to the
ROS 2 Jazzy TCP endpoint at `127.0.0.1:10000` using ROS-TCP-Connector 0.7.0.

`/cmd_vel` (`geometry_msgs/msg/Twist`) drives the humanoid's
`CharacterController`. The Animator uses the generated Idle, Walk, and Run
clips. `/robot/pose` (`geometry_msgs/msg/Pose`) publishes the root transform at
10 Hz. ROS coordinates use FLU (X forward, Y left, Z up); Unity uses +Z forward,
+X right, and +Y up. Connector ROSGeometry conversions handle pose values, and
positive `angular.z` turns left.

## Prerequisites

- Ubuntu with ROS 2 Jazzy installed at `/opt/ros/jazzy`.
- `colcon`, `rosdep`, and Git.
- Unity `6000.6.3f1` with the project package dependencies resolved.
- `ros-jazzy-teleop-twist-keyboard` for keyboard control.

The helper scripts unset Conda and Python override variables before sourcing
ROS. Use the system ROS Python environment.

## Build the ROS workspace

From the repository root, run this once to clone the ROS 2 endpoint branch,
install dependencies, and build it:

```bash
./scripts/setup_ros_workspace.sh
```

The local endpoint checkout and workspace build outputs are ignored by Git.
The endpoint source uses the `main-ros2` branch of
`Unity-Technologies/ROS-TCP-Endpoint`.

## Start the simulation

In separate terminals:

```bash
./scripts/start_ros_endpoint.sh
```

Open `HumanoidSimulation/` in Unity Hub, open
`Assets/Scenes/HumanoidSimulation.unity`, and enter Play Mode. The scene's
`ROSConnection` component connects to `127.0.0.1:10000` on startup.

Start keyboard teleoperation in a terminal with a usable TTY:

```bash
./scripts/start_teleop.sh
```

The standard `u i o / j k l / m , .` keys publish velocity commands. A terminal
without an interactive TTY cannot provide keyboard input to this node.

For click-to-goal navigation, start the navigation node instead of keyboard
teleoperation:

```bash
./scripts/start_navigation.sh
```

Click the ground in the Unity Game view to publish `/goal_pose`; press Escape
to cancel. The proportional controller turns in place for large heading
errors, drives toward the goal, slows near the target, and stops within its
position tolerance. This is basic goal seeking, not obstacle avoidance.

## Topics and parameters

| Topic | Type | Direction | Purpose |
| --- | --- | --- | --- |
| `/cmd_vel` | `geometry_msgs/msg/Twist` | ROS to Unity | Linear X velocity and angular Z yaw rate |
| `/robot/pose` | `geometry_msgs/msg/Pose` | Unity to ROS | Live world position and orientation |
| `/goal_pose` | `geometry_msgs/msg/PoseStamped` | Unity to ROS | Clicked destination in the `map` frame |
| `/goal_cancel` | `std_msgs/msg/Bool` | Unity to ROS | Cancel the active goal (`true`) |

The Unity movement component exposes maximum linear speed (3.0 m/s), maximum
yaw rate (1.57 rad/s), acceleration/deceleration (4.0), command timeout (0.7
s), and gravity in the Inspector. Pose publishing defaults to 10 Hz.

## Verify and debug

```bash
./scripts/check_ros_topics.sh
./scripts/test_cmd_vel.sh 0.8 0.0 1.5
source /opt/ros/jazzy/setup.bash
source HumanoidSimulation/ros2_ws/install/setup.bash
python3 -m pytest HumanoidSimulation/ros2_ws/src/humanoid_navigation/test
```

The motion helper publishes a zero Twist when the requested duration ends. To
inspect a live pose directly:

```bash
source /opt/ros/jazzy/setup.bash
ros2 topic echo /robot/pose
```

If Unity does not connect, confirm the endpoint is listening on port 10000,
Unity is in Play Mode, and the endpoint did not reject a duplicate process. If
topics disappear after a Unity script recompile, exit and re-enter Play Mode so
the connector starts a fresh connection. If the robot does not move, inspect
the `/cmd_vel` subscription count and check that the command has not expired.

## Extension points

The scene rig is generated from primitives because the original template had
no humanoid Avatar or animation assets. There is no dedicated reverse-walk or
turning clip; backwards movement uses the available walk cycle while the root
moves backwards. Waypoint navigation is a basic proportional goal seeker and
does not provide obstacle avoidance.
