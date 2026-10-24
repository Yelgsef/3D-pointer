# Unity Scene Inspection

## Project baseline

- Project: `HumanoidSimulation/`
- Editor: Unity `6000.6.3f1`
- Preserved template scene: `Assets/Scenes/SampleScene.unity`
- Active simulation scene: `Assets/Scenes/HumanoidSimulation.unity`
- ROS-TCP-Connector: `com.unity.robotics.ros-tcp-connector` `v0.7.0`
- ROS 2 endpoint: `127.0.0.1:10000`

The original Unity template contained only a Main Camera and Directional Light.
The current simulation was extended in its own scene; `SampleScene.unity` remains
intact.

## Current scene architecture

`HumanoidSimulation.unity` contains independent `RobotObserver` and
`HumanTarget` roots. The observer owns the CharacterController, ROS velocity
subscriber, robot pose publisher, scenario controls, and camera mount. The
human owns its CharacterController, Humanoid Animator, gesture controller, and
skinned rigged visual. The target model is generated locally and has a 22-bone
armature and a valid Humanoid Avatar.

`RobotEyeCamera` is under `RobotObserver/CameraMount` and renders the existing
640 x 360 `RobotEyeRT`. `RobotPOVCanvas/RobotPOVPanel/LivePreview` displays the
same texture. `Main Camera` remains active and follows `RobotObserver` for the
simulation view; it is not the robot's sensor. The existing ROSConnection
retains `/goal_pose` and `/goal_cancel` publishers.

## Runtime interfaces

- `/cmd_vel` (`geometry_msgs/msg/Twist`) drives `RobotObserver`.
- `/robot/pose` (`geometry_msgs/msg/Pose`) reports the observer pose.
- `/camera/image_raw` (`sensor_msgs/msg/Image`) publishes actual `rgb8` camera
  pixels at a configured 5 Hz.
- `/camera/camera_info` (`sensor_msgs/msg/CameraInfo`) uses intrinsics computed
  from RenderTexture size and camera FOV.
- `/human_perception/detection` and `/human_perception/pose` contain model
  predictions; `/human_perception/debug/image` contains a separate overlay.
- F1-F7 baseline scenarios remain available; F8/F9 select pointing poses.

The camera image frame is labeled `robot_camera_optical_frame`, but a TF for
that frame is not yet published. There is no depth image or Unity ground-truth
ROS interface. Future design details are in
[RGBD_AND_GROUND_TRUTH_PLAN.md](RGBD_AND_GROUND_TRUTH_PLAN.md).

## Scene modification caution

Do not run `BuildHumanoidScene.Build()` or a “Build Demo Scene” command in this
project. The editor generator saves over the active scene path and can erase the
RobotObserver setup. The existing scene and components have been checked and
extended in place. `SampleScene.unity` and the active simulation scene must
remain separate.

See [ROBOT_OBSERVER_SETUP.md](ROBOT_OBSERVER_SETUP.md) for hierarchy and
controls, and [NEXT_MILESTONE_REPORT.md](NEXT_MILESTONE_REPORT.md) for the
phase-by-phase verification record.
