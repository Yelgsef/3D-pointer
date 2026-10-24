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

## Corrected live demonstration

| Scenario | Ground-truth label (evaluator only) | Method A ranking | Method B | Decision |
| --- | --- | --- | --- | --- |
| F8 left arm | TargetObjectA | A 18.0°, B 68.2° | A | Select A; reported arm left |
| F9 right arm | TargetObjectB | B 20.6°, A 70.1° | B | Select B; reported arm right |

The first live run exposed a sign error in the candidate map prior: the Unity
objects are at map X=-6, but the estimator used X=+6. Correcting that static
prior changed both default target rankings and removed the false abstentions.
MediaPipe also assigned the opposite wrist side in this camera view, so the
reported anatomical arm label uses a configurable side swap; ray geometry
continues to use only predicted keypoints.

## Controlled results

The earlier files `pointing_trials_controlled.csv`,
`docs/evidence/pointing_trials_layout2.csv`,
`docs/evidence/pointing_trials_occlusion.csv`, and
`docs/evidence/pointing-metrics.json` contain the initial run's frame-level
records and rollups. That run used X=+6 and is not a result for the corrected
registry. New corrected-registry controlled files will replace these rollups;
the evaluator joins labels after inference and the estimator never subscribes
to them. Frames in each stationary setup are repeated observations, not
independent trials.

- Default target layout: F8/A ranked A first on 2/113 frames and selected it
  correctly twice; it abstained on 111 frames. F9/B ranked B first on 0/55
  ranked frames and selected nothing. A second candidate layout yielded 3/116
  top-1 A rankings for F8 and 0/43 top-1 B rankings for F9; all frames
  abstained. Scores were generally outside the 45-degree acceptance limit.
- At 7 m, all 109 ranked F3 frames placed A first, but the angular gate still
  abstained on all 109. This shows ranking alone is not enough to claim a
  usable target prediction.
- Out-of-view F2 produced 59 hidden samples with zero detections and no target
  selection. After turning, F4 had 44 visible frames and 44 detections. F5
  full-body occlusion had 78 hidden samples and zero false detections.
- In the arm-only occlusion sample, the head visibility check and person
  detection remained true while the left wrist's 3D validity bit became
  false. This verified invalid-depth/keypoint handling under partial
  occlusion; it did not produce a target estimate.
- Logged end-to-end latency averaged about 87–92 ms in these runs. Top-2
  accuracy is 100% for ranked frames because the registry contains exactly
  two candidates, so that metric is not discriminative here.

Phase 6 is **PARTIAL**: the controlled visibility, recovery, distance, layout,
and occlusion behavior is measured, but target identification is not reliable
enough. More distinct poses and object geometries are needed; the present
frame counts must not be reported as independent-trial accuracy.

The geometry helpers have deterministic unit tests. Controlled multi-layout
evaluation and accuracy/abstention reporting remain necessary before any
success claim.

## Recording trials

Start the evaluator in a separate terminal; it joins `/human_perception/pointing_estimate`
with the simulation-only labels after inference and writes a row per matched
frame:

```bash
./scripts/evaluate_pointing.sh --ros-args \
  -p output_csv:=docs/evidence/pointing_trials.csv \
  -p trial_id:=F8_initial \
  -p trial_config_json:='{"gesture":"F8","observer_pose":"initial","distance_m":5}'
```

Change `trial_id` and `trial_config_json` between distinct configurations
with `ros2 param set /pointing_trial_logger ...`. Stop the logger with
Ctrl-C. The evaluator is the only node allowed to subscribe to both predicted
results and `/simulation/ground_truth/...` labels. Compute top-1, top-2,
abstention, visibility, detection, and latency metrics from this CSV; frames
from one unchanged pose are repeated observations, not independent trials.
