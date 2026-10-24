# Pointing Estimator and Evaluation

`pointing_estimator` consumes only `/human_perception/pose_3d`. It never reads
gesture labels, Unity bones, target IDs, or visibility topics. Candidate
positions are an explicit prior registry parameter, defaulting to the current
scene's configured map positions:

```json
{"TargetObjectA":[6.0,-1.3,0.28],"TargetObjectB":[6.0,1.3,0.28]}
```

The node transforms predicted points through TF to `map` at the observation
stamp. Method A forms a normalized eye-midpoint-to-wrist ray (falling back to
the predicted nose if neither eye is valid). Method B independently compares
the elbow-to-wrist direction with candidate directions from the elbow. The
arm producing the smallest Method A angular error is reported. Candidate
ranking is by angular error, with score `1 - angle/180`.

The estimator abstains if it cannot form a valid eye/elbow/wrist ray, if the
best candidate is farther than 45 degrees, or if the best-vs-second-best
margin is under 8 degrees. TF lookup is nonblocking in the pose callback; a
latest-transform fallback is accepted only within 250 ms of the image stamp.

`human_perception_msgs/msg/PointingEstimate` publishes the ray, predicted arm,
ranked candidate IDs and scores, both method winners, ambiguity, abstention,
and selected target. `/human_perception/pointing_markers` publishes separate
ROS visualization markers for the predicted arm skeleton, ray, and labeled
candidate objects. No annotation is drawn into the raw RGB stream.

## Initial live trial

| Scenario | Ground-truth label (evaluator only) | Method A ranking | Method B | Decision |
| --- | --- | --- | --- | --- |
| F8 left arm | TargetObjectA | B 72.4°, A 80.0° | B | Abstain |
| F9 right arm | TargetObjectB | A 72.7°, B 80.2° | A | Abstain |

Both outputs correctly identified which predicted arm was extended, but the
candidate ranking was wrong and the angular errors exceeded the 45-degree
limit, so no target prediction was emitted. This is a measured failure of the
current pointing geometry in these two trials. It does not demonstrate target
recognition. The large downward/lateral wrist rays suggest that the stylized
arm pose, eye-to-wrist baseline, or limb correspondence needs calibration.

## Controlled results

`docs/evidence/pointing_trials_controlled.csv`,
`docs/evidence/pointing_trials_layout2.csv`,
`docs/evidence/pointing_trials_occlusion.csv`, and
`docs/evidence/pointing-metrics.json` contain the frame-level records and
rollups. The evaluator joined labels after inference; the estimator never
subscribed to them. Frames in each stationary setup are repeated observations,
not independent trials.

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
