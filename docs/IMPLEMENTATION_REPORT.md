# Implementation Report

## Status

Core ROS 2 + Unity control loop: **PASS**. Basic waypoint navigation:
**PASS** for the tested ground-click, cardinal-direction, tolerance, cancel,
and stale-pose scenarios. Obstacle avoidance is outside this milestone.

## Features and evidence

| Requirement | Status | Evidence |
| --- | --- | --- |
| Preserve the original Unity project and scene | PASS | Unity 6 project reused; `SampleScene.unity` remains intact and the simulation has its own active scene. |
| ROS 2 Jazzy endpoint | PASS | `ros_tcp_endpoint` rebuilt successfully with `colcon`; executable is `default_server_endpoint`, listening at `127.0.0.1:10000`. |
| Unity ROS connector | PASS | Connector 0.7.0 resolves; Editor Play Mode registered `/cmd_vel` and `/robot/pose` over the TCP connection. |
| Forward/backward movement | PASS | Forward pose X changed from `-4.00` to `-3.47`; backward test then changed X from `-3.47` to `-3.67`. |
| Left/right and curved movement | PASS | Positive yaw produced a positive ROS yaw (~0.23 rad); the opposite command returned toward the starting heading. A combined linear/yaw command changed position and heading together. |
| Command timeout and idle | PASS | After a one-shot command expired, runtime `CurrentLinearSpeed` and Animator `Speed` returned to `0`; the current clip was `Idle`. |
| Walk and run animation | PASS | At 0.5 m/s Animator `Speed` was `0.167` and the locomotion tree selected Walk; at 3.0 m/s it reported `Run` with weight 1.00. |
| Obstacle collision | PASS | A 1.5 m/s forward command stopped the CharacterController at ROS X `2.04`, against the obstacle. |
| Live pose feedback | PASS | `ros2 topic echo /robot/pose` returned finite changing position and quaternion values during movement and turning. |
| Camera follow | PASS | Runtime inspection showed `ThirdPersonFollowCamera.Target` references `Humanoid`; camera position tracks the root offset. |
| Keyboard teleoperation | PASS | A TTY keyboard `i` command published linear X `0.5`; the Unity pose changed from ROS X `-3.73` to `-3.57`. |
| Unity click-to-goal publisher | PASS | An Input System click produced `/goal_pose` in `map` at ROS `(-2.006, 0.0, 0.0)`; Escape publishes `/goal_cancel`. |
| Goal directly ahead | PASS | The clicked goal was accepted and the pose settled at ROS X `-2.072`, within the 0.15 m tolerance. |
| Goals left, right, and behind | PASS | Each ROS `PoseStamped` goal was reached without manual motion commands; final poses were within the configured tolerance. |
| Close goal and cancellation | PASS | A goal 0.05 m away stopped immediately; cancellation published a zero `Twist`. |
| Missing and stale pose | PASS | A goal without pose feedback and a goal after the pose timeout both produced zero `Twist` and were cancelled. |
| Navigation math | PASS | Six pytest cases cover forward, left, right, behind, close goals, angle wrapping, and output limits. |
| ROS workspace | PASS | `scripts/setup_ros_workspace.sh` installed resolvable dependencies and built both `humanoid_navigation` and `ros_tcp_endpoint`. |
| Compilation | PASS | Unity script recompile completed with `failed=false`, `errors=[]`, and `compilationFailed=false`. |

## Files and scene changes

- Added `Assets/Editor/BuildHumanoidScene.cs` to author the simulation scene,
  primitive character, materials, locomotion assets, obstacle, ROS connection,
  and camera.
- Added `RosAnimationController`, `RobotPosePublisher`, and
  `ThirdPersonFollowCamera` runtime scripts.
- Added ROS setup, endpoint, teleop, command-test, and topic-check scripts.
- Added the `humanoid_navigation` ROS 2 package, waypoint controller, math
  checks, and `scripts/start_navigation.sh`.
- Added `ClickGoalPublisher` to publish map-frame `PoseStamped` goals from the
  Game view and cancellation from Escape.
- Added the ROS-TCP-Connector 0.7.0 dependency and enabled the simulation scene
  in Build Settings with its correct asset GUID.
- Added the setup guide and this report. Runtime screenshot evidence is in
  `docs/evidence/humanoid-simulation.png`.

## Limitations

- The starting project was the clean Unity 6 template: it had no humanoid model,
  Avatar, Animator Controller, movement script, or animation clips. The
  simulation therefore uses an articulated primitive rig and generated generic
  Idle/Walk/Run clips, not an imported humanoid Avatar.
- There are no dedicated backward-walk or turning clips. Backwards commands
  move the root backwards while the generic walk cycle plays; turning is applied
  to the root.
- Waypoint navigation uses a proportional controller and does not perform
  obstacle avoidance. A goal behind the obstacle can therefore remain
  unreachable until the user chooses a clear path.
- The ROS 2 endpoint build reports existing setuptools `script-dir` and
  `install-scripts` deprecation warnings; both workspace packages build.
- The Unity Editor and ROS endpoint are closed after final verification.

## Required manual steps

Run `scripts/setup_ros_workspace.sh` once on a machine without the built ROS
workspace, start `scripts/start_ros_endpoint.sh`, open the Unity project and
simulation scene, then enter Play Mode. Use either `scripts/start_teleop.sh` in
an interactive terminal or `scripts/start_navigation.sh` for click-to-goal
navigation; do not run both controllers at once.
