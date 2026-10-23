# Unity Scene Inspection

## Project

- Project root: `HumanoidSimulation/`
- Editor: Unity `6000.6.3f1`
- Preserved template scene: `Assets/Scenes/SampleScene.unity`
- Active simulation scene: `Assets/Scenes/HumanoidSimulation.unity`
- ROS-TCP-Connector: `com.unity.robotics.ros-tcp-connector` `v0.7.0`
- ROS protocol: ROS 2 through ROS-TCP-Endpoint

## Initial scene

The original clean 3D template scene contains only:

- `Main Camera`: Transform, Camera, AudioListener.
- `Directional Light`: Transform, Light.

The camera faces Unity local +Z by default. The template contains no humanoid,
mesh, Animator, Animator Controller, CharacterController, Rigidbody, floor,
obstacle, movement script, or camera-follow system. There are no imported
humanoid animation clips or Avatar assets to reuse.

The implementation preserves `SampleScene.unity` and authors the simulation
in a separate `HumanoidSimulation.unity` scene, which is now the active build
scene. The humanoid is built as a
simple articulated primitive rig with generated looping Animator clips. This is
an animation-capable simulation character, not a retargeted imported humanoid
Avatar.

## Coordinate frame

The simulation root faces Unity local +Z. Unity world axes are X right, Y up,
and Z forward. ROS FLU uses X forward, Y left, and Z up. ROS-TCP-Connector's
`ROSGeometry` FLU conversions are used for pose messages; positive ROS yaw is
applied as negative Unity rotation around world Y.
