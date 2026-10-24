# RGB-D and Simulation Ground-Truth Plan

This document describes a future extension. The current implementation
publishes RGB only; it does not publish depth, simulation ground truth, metric
3D keypoints, or pointing-object estimates.

## Current capture path

`RobotEyeCamera` renders to the existing 640 x 360 `RobotEyeRT`. The project
uses Unity's Built-in Render Pipeline (`GraphicsSettings.currentRenderPipeline`
is null). The color texture is `R8G8B8A8_SRGB`; it has a
`D24_UNorm_S8_UInt` depth-stencil attachment, but the camera's
`depthTextureMode` is currently `None`. The existing attachment is used for
normal occlusion and is not currently read or published as an image.

For a first depth implementation, enable `DepthTextureMode.Depth` on this same
camera and capture the built-in `_CameraDepthTexture`, or add a replacement
shader pass that writes linear eye depth into a separate floating-point
RenderTexture. Keep the existing RGB RenderTexture and publisher unchanged.
The current camera near/far planes are 0.03 m and 100 m. Hardware depth is
nonlinear; convert it with Unity's supported linearization function (for
example `LinearEyeDepth`) using the active projection parameters. Do not
interpret raw depth-buffer values as meters.

The ROS output should be `/camera/depth/image_raw`,
`sensor_msgs/msg/Image`, `32FC1`, `step=width*4`, with each float storing
forward optical-axis depth in meters. Zero or non-finite values represent
invalid pixels. `16UC1` millimeters is an alternative if a consumer needs it,
but the chosen encoding and invalid-pixel convention must be consistent with
`CameraInfo`. Use the same acquisition timestamp, dimensions, and optical frame
as the RGB image. Test known distances in the scene, edge pixels, clipping,
occlusion, GPU readback format, and byte order before treating the stream as
metric.

Scene positions and controller speeds are authored as if one Unity unit equals
one meter, but there is no independent metrology calibration yet. Record that
assumption in any depth dataset and validate a set of measured distances before
using the data for metric evaluation.

## Keep simulation truth separate

Use a separate publisher and message package under `/simulation/ground_truth/`.
Suggested interfaces:

| Topic | Suggested content |
| --- | --- |
| `/simulation/ground_truth/human_joints` | Timestamped world-frame head, eye reference, shoulder, elbow, and wrist positions |
| `/simulation/ground_truth/pointing` | Human ID, current pointing arm/gesture, target object ID |
| `/simulation/ground_truth/objects` | Stable object IDs and timestamped world poses |
| `/simulation/ground_truth/camera_visibility` | Human ID, frustum inclusion, and unobstructed line-of-sight flags |

Read joint truth from the Animator's mapped Humanoid bones after animation has
been evaluated. The present rig exposes a head bone but no separate eye bones;
an eye reference would need an explicitly configured point relative to the
head, and must be described as derived rather than a tracked eye joint. Read
the pointing target from the simulation gesture controller and resolve it
through a stable scene object-ID registry. Compute visibility using the camera
frustum plus a raycast against the same occluder geometry used for rendering.

Use a declared ROS world frame and publish camera/base/world transforms via
`tf`. The existing `robot_camera_optical_frame` is only a frame label at
present; it has no published TF. The truth publisher must publish its own
messages from Unity simulation state and must not be a dependency of
`human_perception`. Never embed truth in `/camera/image_raw`, CameraInfo,
detector messages, or debug images. Evaluation tools may join truth and
predictions by timestamp after inference for offline scoring.

## Future RGB-D estimation path

1. Run the current RGB detector and pose model to obtain 2D pixels and
   per-keypoint visibility.
2. For each confident keypoint, sample a small neighborhood in the aligned
   depth image and use a robust statistic such as the median of valid depths.
   Reject pixels outside the person's detection, at depth discontinuities, or
   below the visibility threshold.
3. Back-project pixel `(u, v)` and metric optical depth `Z` with CameraInfo:
   `X=(u-cx)Z/fx`, `Y=(v-cy)Z/fy`, `Z=Z`. Transform optical points into the
   robot or map frame using TF.
4. Estimate a pointing ray from the shoulder/elbow/wrist/hand configuration,
   retaining uncertainty from keypoint visibility and depth sampling.
5. Rank visible object candidates by ray-to-object angular error and distance,
   while checking object visibility and depth/occlusion consistency. Preserve
   multiple candidates when geometry is ambiguous.

This describes an experiment plan only. Pointing recognition, object
resolution, and interactive questioning remain future work.
