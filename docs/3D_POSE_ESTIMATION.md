# RGB-D 3D Human Pose

`human_pose_3d` synchronizes `/human_perception/pose`,
`/camera/depth/image_raw`, and `/camera/camera_info` with an approximate-time
slop of 100 ms. The 2D pose header keeps the RGB acquisition stamp; the output
copies that stamp and labels its points `robot_camera_optical_frame`.

For each of the 33 landmarks, the node requires visibility >= 0.50, an
in-bounds pixel, and a finite positive neighborhood depth. It takes the median
of valid values in a 3 x 3 region and rejects the region when its 10th-to-90th
percentile spread exceeds max(0.08 m, 5% of the median). Aligned input
dimensions and depth/CameraInfo frame IDs must agree. Depth must be `32FC1`;
invalid and rejected points are output as zero coordinates with `valid=false`.

Back-projection uses the CameraInfo intrinsics:

```text
X = (u - cx) * Z / fx
Y = (v - cy) * Z / fy
Z = depth(u, v)
```

The output interface `human_perception_msgs/msg/HumanPose3D` has parallel
`keypoint_names`, `keypoints`, `confidences`, and `valid` arrays. Consumers
must check `valid` before using a point and may transform valid optical points
to `map` through TF. This node reads no simulation ground-truth topic.

## Runtime check

With the current visible idle scenario, Unity RGB-D and MediaPipe output drove
`/human_perception/pose_3d` at about 4.9 Hz. A sampled frame contained 31/33
valid points (93.9%); the two foot-index depths were invalid. Camera optical Z
for the head/torso/wrist region was about 4.7–4.9 m. The `valid` mask marked
both missing points false and retained the other landmarks. The publisher
keeps the incoming acquisition timestamp rather than the arrival time.

An illustrative comparison used that static view and Unity truth transformed
through the measured camera TF. After pairing left/right limb locations by
spatial side (the stylized rig and detector labels are reversed in this rear
view), wrist-center offsets were about 0.03 m and elbow offsets about 0.04 m;
shoulder offsets were about 0.25 m because the rendered shoulder landmarks
and rig shoulder joints are not coincident. These are one-sample surface/joint
comparisons, not an independent accuracy estimate. RGB-D measures visible
surface pixels, while Unity truth records internal rig joints.

Unit tests cover projection, metric depth sampling, invalid/background depth,
depth discontinuities, out-of-image pixels, and nonpositive Z.
