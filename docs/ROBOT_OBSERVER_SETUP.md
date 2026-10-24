# Robot Observer Setup

The active scene is `HumanoidSimulation/Assets/Scenes/HumanoidSimulation.unity`. It now separates the mobile observer from the existing humanoid and keeps the original ground, obstacle, lighting, and Main Camera.

![Play Mode showing RobotObserver, HumanTarget, and the live Robot POV inset](../HumanoidSimulation/Assets/Screenshots/RobotObserver-PlayMode.png)

## Scene hierarchy

```text
RobotObserver
|-- RobotBody
|-- UpperChassis
|-- FrontSensor
|-- LeftWheel
|-- RightWheel
|-- CameraMount
|   |-- MountStem
|   `-- RobotEyeCamera
|-- CharacterController
|-- RosAnimationController
|-- RobotPosePublisher
`-- RobotObserverScenarioControls

HumanTarget
|-- Visuals
|   `-- Pelvis, Torso, Head, arms, legs, and facial parts
|-- CharacterController
`-- Animator

Main Camera (ThirdPersonFollowCamera targets RobotObserver)
RobotPOVCanvas (Screen Space - Overlay)
`-- RobotPOVPanel
    |-- Title ("ROBOT POV")
    `-- LivePreview (RawImage)
```

`HumanTarget` reuses the previous `Humanoid` scene object, Animator, materials, and animation controller. It is at `(0, 0, -4)` facing the observer. `RobotObserver` starts at `(0, 0, -9)` facing forward along `+Z`; the camera mount is 1.4 m above its root. The humanoid and observer are independent scene roots.

The available target is a stylized model assembled from primitive meshes. It has an Animator and a CharacterController collider, but it does not have a skinned mesh or a humanoid bone rig. The existing `HumanoidLocomotion` controller has Idle, Walk, and Run clips. No Pointing clip is present, so no pointing gesture is claimed.

## Camera and preview

`RobotEyeCamera` is parented under `RobotObserver/CameraMount`, so it follows observer translation and rotation. Its perspective camera renders the actual scene, including normal depth occlusion, to `Assets/RobotVision/RenderTextures/RobotEyeRT.renderTexture` at 640 x 360.

Unity's `Camera.fieldOfView` is vertical. The camera uses 34.6 degrees vertical FOV at 16:9, which gives approximately 57.95 degrees horizontal FOV (58 degrees target), a 0.03 m near plane, and a 100 m far plane. The camera has no AudioListener; the preserved Main Camera is the only AudioListener.

The Screen Space - Overlay canvas shows the live RenderTexture in a bottom-right 360 x 202 pixel RawImage with a dark border and title. Its RawImage and title do not raycast, so the preview does not intercept pointer input. The preview contains only the rendered camera image; there are no labels, IDs, boxes, or skeleton overlays in that image.

## Controls and ROS

The existing `RosAnimationController` is now on `RobotObserver`; it was removed from `HumanTarget` so only the observer subscribes to `/cmd_vel`. `linear.x` drives forward/backward motion, and `angular.z` drives yaw using the existing ROS FLU to Unity conversion. The controller retains its acceleration, speed limits, and 0.7 second command timeout. `RobotPosePublisher` now publishes the observer's pose on `/robot/pose`.

For local ROS 2 testing, start the TCP endpoint in a terminal:

```bash
cd HumanoidSimulation
source /opt/ros/jazzy/setup.bash
source ros2_ws/install/setup.bash
ros2 run ros_tcp_endpoint default_server_endpoint
```

With Unity in Play Mode, publish a short movement command from another terminal:

```bash
source /opt/ros/jazzy/setup.bash
ros2 topic pub -r 10 /cmd_vel geometry_msgs/msg/Twist '{linear: {x: 0.5}, angular: {z: 0.4}}'
```

Stop publishing to test the command timeout; the robot decelerates to a stop. The endpoint used for verification listened on port 10000 and connected to Unity successfully.

For perception, the RenderTexture is the current image source. Image publishing, `sensor_msgs/CameraInfo`, timestamps, optical frame metadata, and depth/RGB-D output remain future work. `/robot/pose` is already available separately from the image stream.

## Scenario controls

Click the Game view in Play Mode to give it keyboard focus, then use the `F` keys handled by `RobotObserverScenarioControls`:

| Key | Scenario |
| --- | --- |
| F1 | Put the target in front of the observer, facing it |
| F2 | Put the target to the left, outside the initial horizontal FOV |
| F3 | Put the target behind the observer |
| F4 | Turn the observer toward the current target |
| F5 | Move and enlarge the opaque obstacle between them |
| F6 | Restore the starting positions and obstacle size |
| F7 | Set the available human Animator parameters to Idle |

The scenario helper is for visual tests; normal robot movement remains on `/cmd_vel`. The Pointing gesture cannot be tested because no compatible clip exists.

## Verification results

| Check | Result | Evidence |
| --- | --- | --- |
| Independent observer and reused target | PASS | Separate roots; no robot movement or pose-publishing scripts remain on `HumanTarget`. |
| Camera parenting and settings | PASS | Camera is under `CameraMount`; 34.6 degree vertical FOV measures 57.95 degrees horizontal; near/far are 0.03/100 m. |
| RenderTexture and live overlay | PASS | `RobotEyeRT` is created at 640 x 360 and assigned to both the camera and RawImage; Play Mode screenshot shows the inset updating with the scene. |
| Human in view and main observer view | PASS | The default target appears in both Robot POV and the preserved Main Camera. |
| Human outside robot FOV | PASS | With the target at `(-6, 0, -4)`, it disappears from Robot POV and remains visible in the Main Camera. |
| Observer rotation | PASS | At 25 degrees the off-axis target enters the image edge; at 50 degrees it is centered. |
| Opaque occlusion | PASS | A 3 m tall obstacle at the midpoint fully hides the target in Robot POV. F6 restores the original obstacle dimensions. |
| Human animation | PARTIAL | The Animator is enabled and the Idle clip is active; this primitive model has no humanoid skeleton and no Pointing animation. |
| ROS `/cmd_vel` movement and timeout | PASS | A live endpoint accepted `/cmd_vel`; Unity moved from `(0, 0, -9)` to about `(-0.33, 0, -8.16)`, turned about 42 degrees, then returned its commanded speeds to zero after publishing stopped. |
| Compilation and saved scene | PASS | Unity script recompile completed with no errors; the scene was saved in Edit Mode after configuration. |

The scenario images and ROS movement test were performed in Play Mode. Runtime test transforms were not saved into the scene; it opens at the default in-view setup. The Play Mode capture is [RobotObserver-PlayMode.png](../HumanoidSimulation/Assets/Screenshots/RobotObserver-PlayMode.png).
