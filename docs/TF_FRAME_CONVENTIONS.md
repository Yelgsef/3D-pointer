# TF Frame Conventions

## Published tree

```text
map
└── base_link
    └── robot_camera_optical_frame
```

`RobotPosePublisher` publishes the dynamic `map → base_link` transform on
`/tf` at the same configured 10 Hz rate as `/robot/pose`. `base_link` is the
`RobotObserver` root. The transform uses the root's actual world position and
rotation; it does not infer odometry from commanded velocity.

The same component reads the actual child `RobotEyeCamera` Transform and sends
its rigid mount transform on `/unity/tf_static`. The `unity_tf_bridge` ROS 2
node republishes this on `/tf_static` with reliable, transient-local QoS, so
late TF subscribers receive the latest transform. The Unity ROS TCP Endpoint
currently ignores its publisher `latch` flag and creates volatile publishers;
the relay is required to provide standard ROS 2 `/tf_static` durability.
Unity refreshes the source transform once per second so the relay can recover
if either side starts or reconnects later.

All TF timestamps use UTC/system time, matching the camera publisher. The
existing `/robot/pose` remains available and has the same position/orientation
values as `map → base_link`.

## Axes and conversions

| Frame | +X | +Y | +Z |
| --- | --- | --- | --- |
| Unity world / RobotObserver | right | up | forward |
| ROS `map` / `base_link` (FLU) | forward | left | up |
| `robot_camera_optical_frame` | image right | image down | optical forward |

For a Unity vector `(x, y, z)`, conversion to ROS FLU is `(z, -x, y)`.
The optical-frame transform then maps FLU `(forward, left, up)` to optical
`(right, down, forward)` as `(-left, -up, forward)`.

The camera is rigidly mounted at Unity-local position `(0, 1.4, 0.12)` with
identity body rotation. Its ROS FLU translation is `(0.12, 0, 1.4)` meters.
The child optical rotation maps its axes to the base frame as:

```text
optical X -> base -Y
optical Y -> base -Z
optical Z -> base +X
```

The published quaternion is `(-0.5, 0.5, -0.5, 0.5)` in xyzw order (the
equivalent opposite-sign quaternion is the same rotation).

## Projection check

With the robot at Unity `(0, 0, -9)` facing `+Z`, the optical camera origin is
map `(-8.88, 0, 1.4)`. A point 2 m forward is map `(-6.88, 0, 1.4)` and
transforms to optical `(0, 0, 2)`. Using the live CameraInfo intrinsics
`fx=fy=577.9135`, `cx=320`, `cy=180`, this projects to pixel `(320, 180)`.
The measured `/tf` chain and arithmetic are recorded in
`docs/evidence/tf-validation.json`.

## Verification

```bash
source /opt/ros/jazzy/setup.bash
source HumanoidSimulation/ros2_ws/install/setup.bash
ros2 run tf2_ros tf2_echo map robot_camera_optical_frame
```

Coordinate conversion and projection unit tests:

```bash
source /opt/ros/jazzy/setup.bash
source HumanoidSimulation/ros2_ws/install/setup.bash
python3 -m pytest -q \
  HumanoidSimulation/ros2_ws/src/humanoid_navigation/test \
  HumanoidSimulation/ros2_ws/src/human_perception/test
```
