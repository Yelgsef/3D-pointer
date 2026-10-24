#!/usr/bin/env python3
"""Offline-only join of RGB-D predictions and Unity truth for trial metrics."""

import csv
import json
from datetime import datetime

import rclpy
from geometry_msgs.msg import PoseArray, PoseStamped
from human_perception_msgs.msg import HumanDetection2D, HumanPose3D, PointingEstimate
from rclpy.duration import Duration
from rclpy.node import Node
from rclpy.time import Time
from std_msgs.msg import String
from tf2_ros import Buffer, TransformException, TransformListener

from human_perception.evaluation_geometry import (
    GROUND_TRUTH_JOINTS,
    keypoint_error_metrics,
    pointing_angular_error,
)
from human_perception.pointing_geometry import rotate_by_quaternion, transform_point


class PointingTrialLogger(Node):
    """Join inference and simulation truth only after the estimator publishes."""

    def __init__(self):
        super().__init__("pointing_trial_logger")
        self.declare_parameter("output_csv", "pointing_trials.csv")
        self.declare_parameter("trial_id", "trial")
        self.declare_parameter("trial_config_json", "{}")
        self.declare_parameter("map_frame", "map")
        self.declare_parameter("swap_landmark_sides", True)
        self.declare_parameter("max_truth_delta_seconds", 0.5)

        self.map_frame = self.get_parameter("map_frame").value
        self.swap_sides = bool(self.get_parameter("swap_landmark_sides").value)
        self.max_truth_delta_ns = int(
            float(self.get_parameter("max_truth_delta_seconds").value) * 1e9
        )
        self.truth = None
        self.visibility = None
        self.detection = None
        self.pose_3d = None
        self.truth_joints = None
        self.truth_objects = None
        self.truth_ray = None
        self.tf_buffer = Buffer()
        self.tf_listener = TransformListener(self.tf_buffer, self)

        path = self.get_parameter("output_csv").value
        self.file = open(path, "a", newline="", encoding="utf-8")
        self.fields = (
            "trial_id", "trial_config_json", "stamp_sec", "stamp_nanosec",
            "actual_target_id", "actual_arm", "human_visible", "detected",
            "detection_confidence", "predicted_arm", "ranked_ids",
            "angular_errors_deg", "top1_correct", "top2_correct", "abstained",
            "selected_target_id", "selected_score", "latency_ms",
            "camera_position_map_json", "candidate_positions_map_json",
            "pointing_angular_error_deg", "predicted_keypoint_valid_count",
            "predicted_keypoint_valid_rate", "head_reference_error_m",
            "left_shoulder_error_m", "right_shoulder_error_m",
            "left_elbow_error_m", "right_elbow_error_m",
            "left_wrist_error_m", "right_wrist_error_m",
            "mean_keypoint_error_m", "ground_truth_keypoint_count",
        )
        self.writer = csv.DictWriter(self.file, fieldnames=self.fields)
        if self.file.tell() == 0:
            self.writer.writeheader()
            self.file.flush()

        self.create_subscription(String, "/simulation/ground_truth/pointing_label", self.on_truth, 10)
        self.create_subscription(String, "/simulation/ground_truth/camera_visibility", self.on_visibility, 10)
        self.create_subscription(PoseArray, "/simulation/ground_truth/human_joints", self.on_truth_joints, 10)
        self.create_subscription(PoseArray, "/simulation/ground_truth/objects", self.on_truth_objects, 10)
        self.create_subscription(PoseStamped, "/simulation/ground_truth/pointing_ray", self.on_truth_ray, 10)
        self.create_subscription(HumanDetection2D, "/human_perception/detection", self.on_detection, 10)
        self.create_subscription(HumanPose3D, "/human_perception/pose_3d", self.on_pose_3d, 10)
        self.create_subscription(PointingEstimate, "/human_perception/pointing_estimate", self.on_estimate, 10)
        self.get_logger().info(f"Writing post-inference evaluation to {path}")

    def on_truth(self, message):
        try:
            self.truth = json.loads(message.data)
        except json.JSONDecodeError:
            self.get_logger().warning("Ignoring malformed simulation label")

    def on_visibility(self, message):
        try:
            self.visibility = json.loads(message.data)
        except json.JSONDecodeError:
            self.visibility = None

    def on_truth_joints(self, message):
        self.truth_joints = message

    def on_truth_objects(self, message):
        self.truth_objects = message

    def on_truth_ray(self, message):
        self.truth_ray = message

    def on_detection(self, message):
        self.detection = message

    def on_pose_3d(self, message):
        self.pose_3d = message

    @staticmethod
    def stamp_ns(message):
        stamp = message.header.stamp
        return stamp.sec * 1_000_000_000 + stamp.nanosec

    def is_near(self, message, stamp_ns):
        return message is not None and abs(self.stamp_ns(message) - stamp_ns) <= self.max_truth_delta_ns

    def transform(self, target_frame, source_frame, stamp):
        try:
            lookup_time = stamp if isinstance(stamp, Time) else Time.from_msg(stamp)
            return self.tf_buffer.lookup_transform(
                target_frame, source_frame, lookup_time,
                timeout=Duration(seconds=0.0),
            )
        except TransformException:
            return None

    def map_pose_points(self, pose):
        if pose.header.frame_id == self.map_frame:
            return {
                name: (point.x, point.y, point.z)
                for name, point, valid in zip(pose.keypoint_names, pose.keypoints, pose.valid)
                if valid
            }
        transform = self.transform(self.map_frame, pose.header.frame_id, pose.header.stamp)
        if transform is None:
            return {}
        translation = transform.transform.translation
        rotation = transform.transform.rotation
        offset = (translation.x, translation.y, translation.z)
        quaternion = (rotation.x, rotation.y, rotation.z, rotation.w)
        return {
            name: transform_point((point.x, point.y, point.z), offset, quaternion)
            for name, point, valid in zip(pose.keypoint_names, pose.keypoints, pose.valid)
            if valid
        }

    def camera_position(self, stamp):
        transform = self.transform(self.map_frame, "robot_camera_optical_frame", stamp)
        if transform is None:
            transform = self.transform(self.map_frame, "robot_camera_optical_frame", Time())
            requested_ns = stamp.sec * 1_000_000_000 + stamp.nanosec
            if (transform is not None and
                    abs(self.stamp_ns(transform) - requested_ns) > self.max_truth_delta_ns):
                transform = None
        if transform is None:
            return ""
        p = transform.transform.translation
        return json.dumps([p.x, p.y, p.z])

    def keypoint_metrics(self, estimate_stamp):
        pose = self.pose_3d
        if pose is None or abs(self.stamp_ns(pose) - estimate_stamp) > self.max_truth_delta_ns:
            return {}, 0, 0.0, None, 0
        predicted = self.map_pose_points(pose)
        valid_count = sum(bool(value) for value in pose.valid)
        valid_rate = valid_count / len(pose.valid) if pose.valid else 0.0
        if not self.is_near(self.truth_joints, estimate_stamp):
            return {}, valid_count, valid_rate, None, 0

        truth = {
            name: (joint.position.x, joint.position.y, joint.position.z)
            for name, joint in zip(GROUND_TRUTH_JOINTS, self.truth_joints.poses)
        }
        metrics = keypoint_error_metrics(predicted, truth, self.swap_sides)
        return (metrics["errors_m"], valid_count, valid_rate,
                metrics["mean_error_m"], metrics["valid_count"])

    def angular_error(self, estimate):
        if (self.truth is None or self.truth.get("arm", "none") == "none" or
                not estimate.has_ray or
                not self.is_near(self.truth_ray, self.stamp_ns(estimate))):
            return ""
        q = self.truth_ray.pose.orientation
        truth_direction = rotate_by_quaternion((1.0, 0.0, 0.0), (q.x, q.y, q.z, q.w))
        predicted_direction = (estimate.direction.x, estimate.direction.y, estimate.direction.z)
        return round(pointing_angular_error(predicted_direction, truth_direction), 4)

    def object_positions(self, stamp_ns):
        if not self.is_near(self.truth_objects, stamp_ns):
            return ""
        names = ("TargetObjectA", "TargetObjectB")
        return json.dumps({
            name: [pose.position.x, pose.position.y, pose.position.z]
            for name, pose in zip(names, self.truth_objects.poses)
        }, sort_keys=True)

    def on_estimate(self, estimate):
        if self.truth is None:
            return
        try:
            truth_time = datetime.fromisoformat(
                self.truth["stamp_utc"].replace("Z", "+00:00")
            ).timestamp()
        except (KeyError, ValueError):
            return
        estimate_ns = self.stamp_ns(estimate)
        estimate_time = estimate_ns / 1e9
        if abs(estimate_time - truth_time) > float(self.get_parameter("max_truth_delta_seconds").value):
            return

        visibility = self.visibility
        if visibility is not None:
            try:
                visibility_time = datetime.fromisoformat(
                    visibility["stamp_utc"].replace("Z", "+00:00")
                ).timestamp()
            except (KeyError, ValueError):
                visibility = None
            else:
                if abs(estimate_time - visibility_time) > float(
                        self.get_parameter("max_truth_delta_seconds").value):
                    visibility = None

        actual = self.truth.get("intended_target_id", "")
        ranked = list(estimate.candidate_ids)
        top1 = ranked[0] if ranked else ""
        now = self.get_clock().now().nanoseconds / 1e9
        errors, valid_count, valid_rate, mean_error, truth_count = self.keypoint_metrics(estimate_ns)
        confidence = ""
        if self.is_near(self.detection, estimate_ns):
            confidence = self.detection.confidence
        angular_errors = list(estimate.angular_errors_deg)
        row = {
            "trial_id": self.get_parameter("trial_id").value,
            "trial_config_json": self.get_parameter("trial_config_json").value,
            "stamp_sec": estimate.header.stamp.sec,
            "stamp_nanosec": estimate.header.stamp.nanosec,
            "actual_target_id": actual,
            "actual_arm": self.truth.get("arm", "none"),
            "human_visible": visibility.get("human_visible", "") if visibility else "",
            "detected": self.detection.detected if self.is_near(self.detection, estimate_ns) else "",
            "detection_confidence": confidence,
            "predicted_arm": estimate.predicted_arm,
            "ranked_ids": json.dumps(ranked),
            "angular_errors_deg": json.dumps(angular_errors),
            "top1_correct": bool(actual and top1 == actual),
            "top2_correct": bool(actual and actual in ranked[:2]),
            "abstained": estimate.abstained,
            "selected_target_id": estimate.selected_target_id,
            "selected_score": (estimate.scores[0] if estimate.scores and estimate.selected_target_id else ""),
            "latency_ms": round(max(0.0, now - estimate_time) * 1000.0, 2),
            "camera_position_map_json": self.camera_position(estimate.header.stamp),
            "candidate_positions_map_json": self.object_positions(estimate_ns),
            "pointing_angular_error_deg": self.angular_error(estimate),
            "predicted_keypoint_valid_count": valid_count,
            "predicted_keypoint_valid_rate": round(valid_rate, 4),
            "head_reference_error_m": errors.get("head_reference", ""),
            "left_shoulder_error_m": errors.get("left_shoulder", ""),
            "right_shoulder_error_m": errors.get("right_shoulder", ""),
            "left_elbow_error_m": errors.get("left_elbow", ""),
            "right_elbow_error_m": errors.get("right_elbow", ""),
            "left_wrist_error_m": errors.get("left_wrist", ""),
            "right_wrist_error_m": errors.get("right_wrist", ""),
            "mean_keypoint_error_m": mean_error if mean_error is not None else "",
            "ground_truth_keypoint_count": truth_count,
        }
        self.writer.writerow(row)
        self.file.flush()

    def destroy_node(self):
        self.file.close()
        super().destroy_node()


def main(args=None):
    rclpy.init(args=args)
    node = PointingTrialLogger()
    try:
        rclpy.spin(node)
    except KeyboardInterrupt:
        pass
    finally:
        node.destroy_node()
        if rclpy.ok():
            rclpy.shutdown()


if __name__ == "__main__":
    main()
