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

The geometry helpers have deterministic unit tests. Controlled multi-layout
evaluation and accuracy/abstention reporting remain necessary before any
success claim.
