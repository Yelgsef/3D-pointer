# TASK: Implement ROS 2 + Unity Animation-Based Humanoid Simulation

## 1. Role and Objective

You are a senior Unity, ROS 2, and robotics simulation engineer operating as an autonomous coding agent.

Your objective is to **implement, integrate, debug, and verify a functional ROS 2 + Unity humanoid simulation in the existing Unity project**.

You have access to:

- Local terminal and filesystem through Codex.
- Unity Editor through an MCP server, if connected.
- Existing Unity assets, scenes, prefabs, scripts, and project settings.
- ROS 2 installed locally.

**Do not merely explain how to implement the project. Perform the implementation using the available tools.**

Work autonomously through the implementation, compilation, testing, and debugging stages. Do not stop after writing scripts.

## 2. Environment

The development environment is:

| Component       | Configuration                           |
| --------------- | --------------------------------------- |
| OS              | Ubuntu 24.04 LTS                        |
| ROS             | ROS 2 Jazzy                             |
| Unity           | Existing Unity 6.x project              |
| Programming     | C# and Python                           |
| ROS workspace   | `~/unity_ros2_ws` (create if necessary) |
| Communication   | ROS-TCP-Connector and ROS-TCP-Endpoint  |
| ROS connection  | `127.0.0.1:10000`                       |
| Simulation type | Animation-based humanoid simulation     |

The Unity Scene already contains:

- A humanoid robot character.
- A ground plane.
- A rectangular obstacle.
- A Main Camera.
- Lighting and environment objects.

**Use the existing project and Scene. Do not create a replacement Unity project.**

Discover the actual project paths and names instead of assuming them.

ROS 2 Jazzy should run with the system Python interpreter. Avoid Conda interference.

## 3. Mandatory Agent Workflow

Follow this loop for every implementation stage:

1. INSPECT — Read the current project and runtime state.
2. PLAN — Determine the minimum necessary modifications.
3. IMPLEMENT — Modify source files and Unity Scene through the available tools.
4. COMPILE — Trigger Unity compilation and inspect errors.
5. TEST — Execute relevant Unity and ROS 2 checks.
6. DEBUG — Fix identified problems.
7. VERIFY — Confirm the requested behavior actually works.
8. REPORT — Record what was modified and the test results.

Do not declare a feature completed solely because its source code was written.

### Unity MCP requirements

First, discover which MCP servers and tools are available.

If Unity MCP is available:

- Inspect the active Scene.
- Query the GameObject hierarchy and components.
- Inspect scripts, prefabs, animation assets, and materials.
- Modify GameObjects and components using MCP tools.
- Inspect Unity Console errors.
- Trigger compilation and enter Play Mode when possible.
- Inspect runtime component values.
- Capture screenshots for verification if the MCP supports it.
- Save the modified Scene.

Do not assume MCP tool names. Discover and use the actual capabilities exposed by the connected server.

If Unity MCP is unavailable, do not pretend it is connected. Continue with source-level work that can be safely performed, and explicitly report the remaining Editor tasks.

### Safety constraints

- Preserve the existing Scene and robot assets.
- Do not delete existing prefabs, Animator Controllers, or scripts.
- Inspect existing input and movement components before editing them.
- Avoid making duplicate systems that control the same Transform.
- If Git is initialized, inspect `git status` before modifying files.
- Do not overwrite unrelated uncommitted changes.
- Create a recoverable checkpoint before significant Scene or prefab edits.
- Do not modify unrelated projects or system configuration.
- Do not run destructive cleanup commands on the Unity project.

---

# PHASE 1 — Inspect Existing Unity Project

## Task 1.1: Project discovery

Identify the Unity project root.

Inspect:

- `Assets/`
- `Packages/manifest.json`
- `ProjectSettings/ProjectVersion.txt`
- Active Scene
- Prefab references
- Relevant assembly definitions
- Unity Editor version

Determine whether the ROS-TCP-Connector package is already installed.

Determine whether a Unity MCP bridge is actively connected.

## Task 1.2: Humanoid inspection

Locate the humanoid robot in the active Scene.

Inspect its hierarchy and components, particularly:

- Transform
- Animator
- CharacterController
- Rigidbody
- Collider
- ThirdPersonController
- StarterAssetsInputs
- PlayerInput
- Other movement scripts

Determine:

1. Which GameObject represents the root of the character?
2. Which GameObject contains the visible mesh?
3. Which GameObject contains the Animator?
4. Which component currently controls movement?
5. Which animation clips are available?
6. Whether an Animator Controller and Blend Tree already exist.
7. Whether the robot's forward direction matches Unity local +Z.
8. Whether the camera uses Cinemachine.

Save the inspection findings in:

`docs/UNITY_SCENE_INSPECTION.md`

## Acceptance criteria

- The actual humanoid root GameObject is identified.
- Existing movement and animation systems are understood.
- The existing Scene remains intact.
- No unnecessary controllers or duplicate character objects are created.

---

# PHASE 2 — ROS 2 and Unity Communication

## Task 2.1: Configure ROS 2

Check ROS 2 Jazzy installation and environment.

Use:

`source /opt/ros/jazzy/setup.bash`

Verify that `ros2` and `colcon` work without Conda environment conflicts.

Create or reuse:

`~/unity_ros2_ws`

Inspect whether `ros_tcp_endpoint` is already present.

If missing, install the ROS 2-compatible branch of:

https://github.com/Unity-Technologies/ROS-TCP-Endpoint

Use the ROS 2 branch, install dependencies with rosdep, and build with colcon.

Verify the executable and its available ROS parameters.

## Task 2.2: Unity ROS Connector

Check whether this Unity package is installed:

`https://github.com/Unity-Technologies/ROS-TCP-Connector.git?path=/com.unity.robotics.ros-tcp-connector`

If missing, install it through Unity Package Manager or an equivalent safe project-editing workflow.

Configure:

- Protocol: ROS 2
- IP address: 127.0.0.1
- TCP port: 10000

Verify compatibility with the project's actual Unity version.

## Task 2.3: Communication test

Run the ROS-TCP-Endpoint.

Connect the Unity Editor during Play Mode.

Confirm that both processes recognize the connection.

Implement a minimal publish/subscribe test if necessary.

## Acceptance criteria

- ROS-TCP-Endpoint builds successfully.
- Unity resolves the ROS-TCP-Connector package.
- ROS 2 and Unity establish a working TCP connection.
- No ROS-related compile errors remain.
- Successful communication is supported by actual logs or test output.

---

# PHASE 3 — Implement Humanoid Movement

## Objective

ROS 2 publishes velocity commands to `/cmd_vel`.

Unity receives those commands and moves the humanoid using animation-based character locomotion.

Use `geometry_msgs/msg/Twist`.

Supported commands:

- Forward
- Backward
- Rotate left
- Rotate right
- Move while rotating
- Stop
- Idle

## Task 3.1: Create ROS movement controller

Create a maintainable C# controller, for example:

`Assets/Scripts/ROS/RosAnimationController.cs`

The controller should:

1. Subscribe to `/cmd_vel`.
2. Read `linear.x` as forward/backward velocity.
3. Read `angular.z` as yaw rate.
4. Apply configurable velocity limits.
5. Apply configurable acceleration and deceleration.
6. Move the character with CharacterController.
7. Update the Animator based on motion.
8. Implement a command timeout.
9. Stop smoothly when commands expire.
10. Support debugging without excessive log output.

Recommended initial settings:

- Maximum linear speed: 3.0 m/s.
- Maximum yaw rate: 1.57 rad/s.
- Acceleration: 4.0 m/s².
- Command timeout: 0.7 seconds.

Expose these settings in the Inspector.

Do not add a second CharacterController if the character already has one.

If the existing character uses Starter Assets, identify the safest integration strategy.

Disable conflicting movement behavior when necessary, but preserve the original components and configurations.

## Task 3.2: Coordinate systems

Use correct transformations between ROS and Unity.

ROS convention:

- X forward
- Y left
- Z up
- Positive angular Z for leftward yaw

Unity convention for this project:

- Z forward
- X right
- Y up

Check and implement the correct sign and axis conversions.

Use ROSGeometry utilities where appropriate.

Do not rely on assumed axis conversions without checking the actual character orientation.

## Task 3.3: CharacterController configuration

Inspect and configure:

- Capsule height
- Capsule radius
- Capsule center
- Ground collision
- Gravity behavior
- Step offset
- Skin width

Avoid collisions between incompatible movement systems, such as an actively driven Rigidbody and a CharacterController.

## Acceptance criteria

- A ROS velocity command moves the actual humanoid in the Scene.
- The robot can move forward and backward.
- Positive ROS angular Z turns the robot left.
- Negative ROS angular Z turns the robot right.
- Simultaneous linear/angular commands produce curved motion.
- The robot stops after the command timeout.
- The robot does not fall through the ground or move through the obstacle under normal collision conditions.

---

# PHASE 4 — Integrate Humanoid Animations

## Task 4.1: Inspect animation assets

Determine whether the existing humanoid already supports:

- Idle
- Walk
- Run
- Walk backward
- Turning animations

Reuse compatible existing animations wherever possible.

Do not replace a working Animator Controller unnecessarily.

If animation assets are missing, inspect the project's available packages and assets for compatible alternatives.

Do not claim animation integration is complete when the required clips are unavailable.

## Task 4.2: Animator configuration

Configure an Animator Controller or adapt the existing one.

Use an appropriate locomotion Blend Tree.

Recommended parameters:

- `Speed`: Float
- `MoveDirection`: Float, if needed for backward locomotion.
- `Turn`: Float, if turning clips are available.

Recommended default speed thresholds:

- Idle = 0.0
- Walk = 1.0
- Run = 3.0

These thresholds may be adjusted to match the existing animation system.

Disable Apply Root Motion for controller-driven movement unless the existing project requires a different architecture and that choice is documented.

Ensure the animation playback speed visually matches the character's movement speed as closely as possible.

Avoid modifying unrelated Animator states.

## Task 4.3: Runtime validation

Test transitions:

- Idle → Walk
- Walk → Idle
- Walk → Run
- Run → Walk
- Forward → Backward
- Movement → Stop

If backward or turning clips are unavailable, implement a documented fallback rather than pretending those animations exist.

## Acceptance criteria

- The visible humanoid plays Idle while stationary.
- Walking animation plays during normal motion.
- Running animation plays when commanded speed reaches the run threshold.
- Transitions are smooth.
- The robot does not unintentionally slide or teleport due to Root Motion conflicts.
- The existing mesh and Avatar remain valid.

---

# PHASE 5 — Publish Robot State to ROS 2

## Task 5.1: Pose publishing

Implement Unity-to-ROS pose feedback.

Topic:

`/robot/pose`

Message type:

`geometry_msgs/msg/Pose`

Initial publishing frequency:

10 Hz.

Read the character's actual world position and orientation.

Convert Unity coordinates to ROS FLU correctly.

Do not publish fake or hardcoded position values.

## Task 5.2: State validation

Use ROS 2 tools to verify:

- The topic is registered.
- Messages are received.
- Position values change when the robot moves.
- Orientation values change when the robot rotates.
- Values remain finite and reasonable.

Document the coordinate frame assumptions.

For future navigation functionality, consider adding a timestamped pose topic, such as PoseStamped, without breaking `/robot/pose`.

## Acceptance criteria

- `/robot/pose` contains live Unity transform data.
- ROS 2 can read the topic.
- Position and orientation match observed movement.

---

# PHASE 6 — Integrate Camera

## Objective

Ensure that the existing Main Camera follows the humanoid during ROS-controlled movement.

## Task 6.1: Inspect existing camera

Check:

- Main Camera
- Cinemachine components
- Camera follow targets
- CameraRoot objects
- Existing camera scripts
- Camera-related input systems

Prefer reusing the existing camera system.

If a working Cinemachine camera exists, update its tracking target as needed.

If no follow system exists, implement a simple third-person follow camera.

## Task 6.2: Runtime validation

Verify:

- Camera follows the moving humanoid.
- Robot remains visible during normal movement.
- Camera does not fight another control script.
- Rotation is smooth.
- No excessive jitter occurs.
- The current Scene composition is preserved.

Do not add duplicate active cameras without a specific reason.

---

# PHASE 7 — ROS 2 Teleoperation

## Task 7.1: Keyboard control

Install or verify:

`ros-jazzy-teleop-twist-keyboard`

Configure it to publish `/cmd_vel`.

Verify standard keyboard motion commands work with the Unity character.

## Task 7.2: Automation scripts

Create reusable development scripts where appropriate, for example:

- `scripts/start_ros_endpoint.sh`
- `scripts/start_teleop.sh`
- `scripts/test_cmd_vel.sh`
- `scripts/check_ros_topics.sh`

Scripts should:

- Source the ROS 2 environment.
- Source the workspace overlay when available.
- Avoid Conda conflicts.
- Use the correct package names.
- Fail clearly when dependencies are missing.

Do not silently launch duplicate ROS endpoints.

## Acceptance criteria

- ROS keyboard teleoperation controls the Unity robot.
- The user can start the essential ROS processes without retyping long commands.
- The scripts are executable and documented.

---

# PHASE 8 — Basic Waypoint Navigation

Only begin this phase after Phases 1–7 have passed their essential checks.

## Objective

Allow the user to select a destination in Unity and have ROS 2 navigate the character toward it.

Architecture:

Unity mouse click → goal publisher → ROS navigation node → `/cmd_vel` → Unity character → `/robot/pose` feedback.

## Task 8.1: Unity goal publisher

Implement ground-plane click-to-goal selection.

Use a camera raycast to find a destination on the ground.

Publish the selected goal to ROS 2 through a documented topic such as:

`/goal_pose`

Use a suitable geometry message with an explicit coordinate frame.

Convert the target from Unity coordinates into ROS coordinates.

Avoid publishing clicks on unrelated UI elements.

## Task 8.2: ROS 2 navigation node

Create a Python ROS 2 node using `rclpy`.

It should:

1. Subscribe to the target destination.
2. Subscribe to robot pose feedback.
3. Calculate distance and heading to the target.
4. Compute forward velocity and angular velocity.
5. Publish `/cmd_vel`.
6. Reduce speed near the target.
7. Stop within a configurable position tolerance.
8. Handle unavailable or stale pose feedback safely.
9. Support goal cancellation and emergency stopping.

A basic proportional controller is sufficient for this milestone.

Do not introduce Nav2 unless the existing project already requires it.

This phase is **basic goal seeking**, not full obstacle avoidance.

## Task 8.3: Runtime test

Test at least:

- Goal directly ahead.
- Goal to the left.
- Goal to the right.
- Goal behind the robot.
- Goal close to the robot.
- Goal cancellation.
- Missing pose feedback.

Verify the character reaches unobstructed targets without continuous manual intervention.

## Acceptance criteria

- Clicking a reachable destination causes ROS 2 to generate motion commands.
- The character approaches the selected goal.
- The robot stops within the configured tolerance.
- The user can cancel navigation.
- Collision handling remains active.

---

# PHASE 9 — Automated Testing and Debugging

Perform the strongest tests supported by the available environment.

## Unity validation

- Check for compile errors.
- Verify scripts and components are attached to the correct objects.
- Check for null references.
- Check Animator parameter names.
- Enter Play Mode if supported.
- Inspect Unity Console.
- Observe runtime transforms and Animator values.
- Capture screenshots where supported.

## ROS 2 validation

- Build ROS workspace successfully.
- Confirm the Endpoint is running.
- Inspect `ros2 topic list`.
- Verify `/cmd_vel` type.
- Verify `/robot/pose` type.
- Publish test velocity commands.
- Check that Unity responds.
- Check pose feedback.
- Run unit tests for navigation calculations where applicable.

## End-to-end scenarios

Test:

1. Idle after starting the simulation.
2. Move forward for a controlled duration.
3. Stop after commands expire.
4. Rotate left.
5. Rotate right.
6. Move in a curve.
7. Move backward.
8. Move while colliding with the obstacle.
9. Receive pose updates in ROS.
10. Reach a waypoint if Phase 8 is implemented.

For automated validation, compare Unity transform values before and after commands rather than relying exclusively on visual inspection.

Do not fabricate successful test results.

---

# PHASE 10 — Documentation and Final Deliverables

Create:

`docs/ROS2_UNITY_SETUP.md`

Include:

- System architecture.
- Actual Unity project/Scene information.
- Prerequisites.
- Installation steps.
- Running ROS-TCP-Endpoint.
- Starting Unity.
- Running teleoperation.
- ROS topic definitions.
- Configuration parameters.
- Debugging common failures.
- How to extend the system.

Create:

`docs/IMPLEMENTATION_REPORT.md`

Include:

1. Features implemented.
2. Files created or modified.
3. Unity GameObjects and components modified.
4. ROS packages created or modified.
5. Compilation results.
6. ROS connection results.
7. Runtime tests executed.
8. Screenshots or log evidence if available.
9. Remaining issues.
10. Required manual steps, if any.

## Definition of Done

The core task is complete only when:

- The existing humanoid is preserved.
- ROS 2 can command its movement.
- CharacterController moves it correctly.
- Animator reflects movement.
- Main Camera follows the robot.
- Unity publishes live pose data to ROS 2.
- No relevant compilation errors remain.
- Integration is verified with actual ROS 2 messages.

Waypoint navigation is a second milestone and must be reported separately.

Use these statuses in the final report:

- PASS — verified by an actual test.
- PARTIAL — implemented but not fully verified.
- BLOCKED — missing dependency, asset, tool, or runtime access.
- FAIL — tested and not working.

**Never mark a feature PASS based solely on code inspection.**

## Execution Instruction

Start immediately by discovering the connected MCP tools, locating the active Unity project, and inspecting the current Scene.

Then implement the phases sequentially.

Prioritize a working end-to-end ROS 2 → Unity → ROS 2 loop before optional enhancements.

Fix problems when they arise. Do not stop at the first compilation failure.

Use existing packages and project architecture where possible.

At completion, provide a concise engineering report with the actual changes, tests, and remaining limitations.

**Your job is to complete and verify the implementation, not to produce another implementation plan.**
