# Robot ROS Camera Pipeline

## Unity source

`RobotCameraPublisher` is attached to the existing `RobotEyeCamera` under
`RobotObserver/CameraMount`. It reads that camera's existing 640 x 360
`RobotEyeRT`; it does not add a camera or alter the POV preview. The serialized
publish rate is 5 Hz. When supported, Unity `AsyncGPUReadback` supplies RGB24
pixels; a reusable `Texture2D` is used as a synchronous fallback. A two-message
pool is configured for both ROS message types.

Run the existing endpoint and enter Play Mode:

```bash
./scripts/start_ros_endpoint.sh
```

The publisher writes:

| Topic | Type | Details |
| --- | --- | --- |
| `/camera/image_raw` | `sensor_msgs/msg/Image` | `rgb8`, 640 x 360, `step=1920`, 691200 bytes |
| `/camera/camera_info` | `sensor_msgs/msg/CameraInfo` | Pinhole intrinsics, zero `plumb_bob` distortion |

Both messages from a capture share their acquisition timestamp and frame ID,
`robot_camera_optical_frame`. Timestamps are generated from UTC Unix time; use
ROS system time for subscribers (do not enable simulated ROS time unless the
publisher is changed to use the simulation clock).

## Pixel and camera conventions

Rows are flipped from Unity's bottom-left texture origin to ROS's top-left
image origin. The byte order is RGB. The frame ID denotes the ROS optical
convention: +x right, +y down, +z forward. Unity's camera looks along local +z
with local +y up; this pipeline labels the image in optical convention but does
not yet publish a `tf` transform for that frame. Consumers needing geometric
transforms should add and validate the camera-to-robot optical-frame transform.

For a perspective camera with vertical FOV `theta`, height `H`, and width `W`,
the publisher computes `fy = H / (2 tan(theta/2))`, `fx = fy` for the square
pixel projection, and principal point `(W/2, H/2)`. At the current 34.6-degree
vertical FOV, CameraInfo reports `fx=fy=577.9135`, `cx=320`, `cy=180`. No lens
distortion is simulated.

## Inspection

Capture a paired image and CameraInfo sample:

```bash
source /opt/ros/jazzy/setup.bash
python3 scripts/capture_robot_camera.py --frames 12 --output /tmp/robot-camera.png
```

The script checks encoding, byte count, step, dimensions, timestamps, and frame
IDs, then reports the arrival rate and intrinsics. The saved PNG is useful for
checking orientation and color channels against the Unity POV preview.

Runtime evidence from Unity 6000.6.3f1 and ROS 2 Jazzy:

- `docs/evidence/ros-camera-sample.png`: visible human, upright image and
  correctly rendered scene colors; paired messages and 640 x 360 RGB metadata
  verified.
- `docs/evidence/ros-camera-f2-out-of-view.png`: F2 view with no person.
- `docs/evidence/ros-camera-f4-turned-to-human.png`: person enters the image
  after the observer turns toward the F2 target.
- `docs/evidence/ros-camera-f5-occluded.png`: obstacle fully blocks the person.

The measured rates in short 3- and 12-frame samples varied around the 5 Hz
setting (roughly 4.8-8.7 Hz); short windows and editor scheduling affect this
measurement. The publisher rate is a minimum interval between capture requests,
not a hard real-time guarantee. Images are rendered camera pixels, not synthetic
or Unity ground-truth data. No RGB-D output is implemented.
