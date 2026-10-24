# RGB-D Camera Implementation

## Capture path

`RobotCameraPublisher` continues to use the existing `RobotEyeCamera` and
`RobotEyeRT` for RGB. It enables `DepthTextureMode.Depth` on that camera, then
uses `RobotCameraLinearDepth.shader` to convert Unity's nonlinear camera depth
buffer into forward optical-axis depth in a 640 x 360 `RFloat` render target.
The depth target is read asynchronously where supported. A synchronous
`Texture2D.ReadPixels` fallback is retained for devices without asynchronous
GPU readback.

The publisher requests color and depth from the same camera render and waits
for both readbacks before publishing either. The RGB image, depth image, and
CameraInfo therefore share the same acquisition timestamp and
`robot_camera_optical_frame` frame ID. Rows are flipped from Unity's bottom-left
readback order to ROS's top-left image order. Depth is published as
`sensor_msgs/msg/Image`, `32FC1`, with one little-endian float in meters per
pixel (`step = width * 4`). Zero marks background, clipped, or invalid depth.
`LinearEyeDepth` gives optical-axis Z, not Euclidean range from the camera
center.

The existing RGB RenderTexture and preview UI are unchanged. No Unity object
transforms or target metadata are used to construct the depth image. Opaque
materials that participate in Unity's camera depth pass are represented;
materials that do not write camera depth (for example, some transparent or
custom shaders) will not produce valid depth.

## Validation

On Unity 6000.6.3f1 with the Built-in Render Pipeline and an RFloat-capable
graphics device, a temporary camera-facing plane was placed at known optical
axis distances. The center 9 x 9 depth neighborhood measured:

| Reference | Measured median | Absolute error |
| ---: | ---: | ---: |
| 2.0 m | 2.00002 m | 0.00002 m |
| 4.0 m | 4.00000 m | 0.00000 m |
| 6.0 m | 5.99996 m | 0.00004 m |

For an additional alignment check, a magenta plane at 6 m produced center RGB
`[255, 42, 244]` and depth `5.99996 m` in messages with identical timestamps
and frame IDs. A green occluder at 3 m in front of the 6 m plane changed the
same center pixel to green and its depth to `3.00000 m`. With the camera yawed
90 degrees and a plane placed 4 m along its new forward axis, the center depth
was `3.99994 m`. One normal scene frame contained 20,398 zero-valued pixels
out of 230,400, demonstrating explicit invalid/background values.

The existing F5 obstacle scenario was also exercised: the center region read
about 1.98 m for the obstacle, in front of the human surface at about 4.72 m.
A two-plane edge test read 3 m on the foreground plane and 6 m immediately
outside it (`u=220: 6 m`, `u=230: 3 m`, `u=410: 3 m`, `u=420: 6 m`), with no
interpolated values at those samples.

The test planes were created and removed in Play Mode; the saved scene is not
modified. Machine-readable measurements are in
`docs/evidence/depth-distance-check.json`. The errors demonstrate the capture
and linearization path against Unity-authored distances; they do not calibrate
the scene's physical scale. One Unity unit is still assumed to equal one meter.

## Limitations

- This is ideal simulated depth, not a model of sensor noise, missing returns,
  multipath, or depth quantization.
- Although capture is configured for 5 Hz and uses asynchronous GPU readback,
  short ROS delivery measurements varied: about 4.0 Hz RGB and 2.4 Hz depth.
  The paired image payload and Editor scheduling need further performance
  tuning before treating 5 Hz as a sustained rate.
- Transparent and custom shaders need an appropriate depth pass to appear.
- The camera's optical frame is still not connected to `base_link` or `map` by
  TF; see `docs/TF_FRAME_CONVENTIONS.md` as that phase is implemented.
- The perspective ray alignment is inherited from the same camera projection;
  later 3D back-projection still depends on correct TF and CameraInfo handling.
