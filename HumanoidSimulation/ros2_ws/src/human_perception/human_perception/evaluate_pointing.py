#!/usr/bin/env python3
"""Offline-only join of predictions and Unity truth for trial CSV logging."""

import csv
import json
from datetime import datetime

import rclpy
from human_perception_msgs.msg import HumanDetection2D, PointingEstimate
from rclpy.node import Node
from std_msgs.msg import String


class PointingTrialLogger(Node):
    def __init__(self):
        super().__init__("pointing_trial_logger")
        self.declare_parameter("output_csv", "pointing_trials.csv")
        self.declare_parameter("trial_id", "trial")
        self.declare_parameter("trial_config_json", "{}")
        self.truth = None
        self.visibility = None
        self.detection = None
        path = self.get_parameter("output_csv").value
        self.file = open(path, "a", newline="", encoding="utf-8")
        self.writer = csv.DictWriter(self.file, fieldnames=(
            "trial_id", "trial_config_json", "stamp_sec", "stamp_nanosec",
            "actual_target_id", "actual_arm", "human_visible", "detected",
            "predicted_arm", "ranked_ids", "angular_errors_deg", "top1_correct",
            "top2_correct", "abstained", "selected_target_id", "latency_ms",
        ))
        if self.file.tell() == 0:
            self.writer.writeheader()
            self.file.flush()

        self.create_subscription(String, "/simulation/ground_truth/pointing_label", self.on_truth, 10)
        self.create_subscription(String, "/simulation/ground_truth/camera_visibility", self.on_visibility, 10)
        self.create_subscription(HumanDetection2D, "/human_perception/detection", self.on_detection, 10)
        self.create_subscription(PointingEstimate, "/human_perception/pointing_estimate", self.on_estimate, 10)
        self.get_logger().info(f"Writing independent evaluation trials to {path}")

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

    def on_detection(self, message):
        self.detection = message

    def on_estimate(self, estimate):
        if self.truth is None:
            return
        try:
            truth_time = datetime.fromisoformat(self.truth["stamp_utc"].replace("Z", "+00:00")).timestamp()
        except (KeyError, ValueError):
            return
        estimate_time = estimate.header.stamp.sec + estimate.header.stamp.nanosec / 1e9
        delta = abs(estimate_time - truth_time)
        if delta > 0.5:
            return

        actual = self.truth.get("intended_target_id", "")
        ranked = list(estimate.candidate_ids)
        top1 = ranked[0] if ranked else ""
        actual_arm = self.truth.get("arm", "none")
        now = self.get_clock().now().nanoseconds / 1e9
        row = {
            "trial_id": self.get_parameter("trial_id").value,
            "trial_config_json": self.get_parameter("trial_config_json").value,
            "stamp_sec": estimate.header.stamp.sec,
            "stamp_nanosec": estimate.header.stamp.nanosec,
            "actual_target_id": actual,
            "actual_arm": actual_arm,
            "human_visible": self.visibility.get("human_visible", "") if self.visibility else "",
            "detected": self.detection.detected if self.detection else "",
            "predicted_arm": estimate.predicted_arm,
            "ranked_ids": json.dumps(ranked),
            "angular_errors_deg": json.dumps(list(estimate.angular_errors_deg)),
            "top1_correct": bool(actual and top1 == actual),
            "top2_correct": bool(actual and actual in ranked[:2]),
            "abstained": estimate.abstained,
            "selected_target_id": estimate.selected_target_id,
            "latency_ms": round(max(0.0, now - estimate_time) * 1000.0, 2),
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
