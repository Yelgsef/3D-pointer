# HumanTarget Setup

## Rig and scene integration

The active simulation scene uses separate `RobotObserver` and `HumanTarget`
roots. The target keeps its root `CharacterController`, `Animator`, and
`HumanTargetGestureController`; the skinned visual is the locally generated
`Assets/Characters/HumanTarget_Rigged.fbx` model. Its hierarchy includes a
22-bone armature with hips, spine, chest, neck, head, shoulders, upper/lower
arms, hands, legs, and feet. The imported Avatar is configured as Humanoid and
is assigned to the target Animator. The skinned mesh is rendered by one
`SkinnedMeshRenderer`.

The imported FBX root under `HumanVisual` also contains its import-generated
Animator component, with no RuntimeAnimatorController assigned. The Animator
on `HumanTarget` is the authoritative controller used by the gesture script
and scenario controls; do not assign a second animation controller to the
imported child Animator.

The model was generated locally in Blender for this project. No external
character asset or redistributable third-party character was downloaded. The
rigged FBX contains 4,818 vertices; Unity reports a valid Humanoid Avatar with
16 mapped bones. Its deliberately stylized proportions are useful for
simulation and do not represent a photorealistic human dataset.

The observer, camera, preview, ROS connection, target root, and scenario
controls remain separate. Moving the target through F1-F6 changes only its
scene transform; it does not replace the Animator or the character collider.
`RobotObserver` continues to move from `/cmd_vel` and publish `/robot/pose`.

## Gestures and pointing targets

`HumanTargetGestureController` solves a two-segment arm pose toward a configured
scene target after the Animator evaluates. It restores the imported bind pose,
then aims the upper arm and lower arm while preserving the rest of the skeleton.
Reach is clamped and an elbow bend plane is selected to avoid a straight,
flipped joint solution. `TargetObjectA` and `TargetObjectB` are the environment
references; this is a controlled pose, not a gesture-recognition algorithm.

| Key | Action |
| --- | --- |
| F1 | Place the human in front |
| F2 | Move the human outside the observer's initial horizontal FOV |
| F3 | Place the human behind the observer |
| F4 | Turn the observer toward the current human position |
| F5 | Put the opaque obstacle between observer and human |
| F6 | Restore the scene's initial robot, human, and obstacle transforms |
| F7 | Select the idle pose |
| F8 | Point the left arm toward `TargetObjectA` |
| F9 | Point the right arm toward `TargetObjectB` |

The Animator Controller provides the authored idle state; the pointing poses are
procedural. The F1-F7 behavior was preserved from the original scenario
controls, with F8/F9 added for gesture tests.

## Verification and limits

Unity Play Mode confirmed a valid Humanoid Avatar, 22 bones, one skinned mesh,
and visible human renders from the robot camera. Evidence includes
`docs/evidence/human-target-left-point.png`,
`docs/evidence/human-target-right-point.png`,
`docs/evidence/scenario-f2-out-of-view.png`, and
`docs/evidence/scenario-f4-turned-to-human.png`. ROS perception tests also
identified the human in F1, F4, F6, and the F8 pointing pose.

The model is low-poly and stylized. The rig has a head bone but no separate eye
bones. The pose controller does not recognize pointing intent or infer which
object a person means; it is a deterministic simulation control for future
camera-based experiments.
