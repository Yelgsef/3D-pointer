# Pointing Estimator and Evaluation

`pointing_estimator` consumes only `/human_perception/pose_3d`. It never reads
gesture labels, Unity bones, target IDs, or visibility topics. Candidate
positions are an explicit prior registry parameter, defaulting to the current
scene's configured map positions:

```json
{"TargetObjectA":[-6.0,-1.3,0.28],"TargetObjectB":[-6.0,1.3,0.28]}
```

These coordinates are ROS `map` XYZ from the static scene registry. The Unity
ground-truth pose topics independently confirmed the objects at map X=-6; an
earlier registry version used X=+6 and produced the failed rankings in the
initial trial tables below. Those initial metrics are retained as historical
evidence and are not results for the corrected registry.

The node transforms predicted points through TF to `map` at the observation
stamp. Method A forms a normalized eye-midpoint-to-wrist ray (falling back to
the predicted nose if neither eye is valid). Method B independently compares
the elbow-to-wrist direction with candidate directions from the elbow. The
arm producing the smallest Method A angular error determines the ray. The
reported anatomical arm label is then calibrated from that MediaPipe side.
Candidate ranking is by angular error, with score `1 - angle/180`.

The estimator abstains if it cannot form a valid eye/elbow/wrist ray, if the
best candidate is farther than 45 degrees, or if the best-vs-second-best
margin is under 8 degrees. TF lookup is nonblocking in the pose callback; a
latest-transform fallback is accepted only within 250 ms of the image stamp.

The default `swap_predicted_arm_labels=true` corrects the measured left/right
landmark inversion in the current HumanTarget camera view. It changes only the
reported anatomical arm label; the ray and candidate ranking still use the
predicted landmark coordinates. Set it to `false` for a camera/model
calibration whose MediaPipe side labels match the subject's anatomical sides.

`human_perception_msgs/msg/PointingEstimate` publishes the ray, predicted arm,
ranked candidate IDs and scores, both method winners, ambiguity, abstention,
and selected target. `/human_perception/pointing_markers` publishes separate
ROS visualization markers for the predicted arm skeleton, ray, and labeled
candidate objects. No annotation is drawn into the raw RGB stream.

## Corrected-registry evaluation

The evaluator joins labels after inference and is the only perception-side
node subscribed to Unity ground truth. The estimator continues to consume
only predicted 3D landmarks, TF, and the candidate-position prior.
`docs/evidence/pointing_trials_corrected.csv` contains the frame-level records;
`docs/evidence/pointing_trials_corrected_summary.json` contains descriptive
per-configuration rollups. Samples within one fixed pose are repeated frames,
not independent trials.

| Trial/configuration | Frames | Top-1 ranking | Correct selected target | Mean ray error vs truth | Mean 3D keypoint error | Abstentions |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| F8/A, default, 5 m | 159 | 159/159 | 159/159 | 6.45° | 0.133 m | 0% |
| F9/B, default, 5 m | 129 | 129/129 | 129/129 | 6.44° | 0.126 m | 0% |
| F8/A, default, 7 m, observer yaw 10° | 115 | 115/115 | 115/115 | 6.11° | 0.126 m | 0% |
| F8/A, wide layout (Y=±2 m), 7 m, yaw 10° | 154 | 154/154 | 154/154 | 7.20° | 0.121 m | 0% |
| F9/B, wide layout (Y=±2 m), 7 m, yaw 10° | 129 | 129/129 | 129/129 | 7.31° | 0.117 m | 0% |
| C: human outside FOV | 95 | no candidate ray | 0 selected | n/a | n/a | 100% |
| D: visible after restoring view | 145 | 145/145 | 145/145 | 6.62° | 0.128 m | 0% |
| E: pointing wrist occluded | 94 | B ranked first | 0 selected | 77.97° | 0.136 m | 100% |

For C, all 95 rows reported invisible, undetected, and abstained. For E, all
94 rows retained person visibility and detection while the tested wrist became
invalid in 3D; the estimator abstained because its best ray exceeded the
45-degree acceptance limit. Top-2 is not informative with only two candidates.

Phase 6 remains **PARTIAL**. Both gestures succeeded in two candidate layouts,
including a 7 m observation at a 10-degree observer yaw, and the expected
out-of-view and occlusion abstentions were measured. These are still repeated
frames from one stylized human and a small number of fixed geometries; they do
not establish independent-trial generalization or real-world performance.
Ground-truth wrist/elbow comparisons are surface-depth measurements and may
not coincide with internal skeletal joint centers.

## Recording trials

Start the evaluator in a separate terminal; it joins `/human_perception/pointing_estimate`
with the simulation-only labels after inference and writes a row per matched
frame:

```bash
./scripts/evaluate_pointing.sh --ros-args \
  -p output_csv:=docs/evidence/pointing_trials.csv \
  -p trial_id:=F8_initial \
  -p trial_config_json:='"{\"gesture\":\"F8\",\"observer_pose\":\"initial\",\"distance_m\":5}"'
```

Change `trial_id` and `trial_config_json` between distinct configurations
with `ros2 param set /pointing_trial_logger ...`. Stop the logger with
Ctrl-C. The evaluator is the only node allowed to subscribe to both predicted
results and `/simulation/ground_truth/...` labels. Compute top-1, top-2,
abstention, visibility, detection, and latency metrics from this CSV; frames
from one unchanged pose are repeated observations, not independent trials.
