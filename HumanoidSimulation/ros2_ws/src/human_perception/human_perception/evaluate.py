#!/usr/bin/env python3
"""Collect one scenario's camera and perception metrics into JSON."""

import argparse
import json
import time
from pathlib import Path

import cv2
import numpy as np
import rclpy
from human_perception_msgs.msg import HumanDetection2D, HumanPose2D
from rclpy.node import Node
from sensor_msgs.msg import Image


IMPORTANT_KEYPOINTS = (0, 11, 12, 13, 14, 15, 16, 23, 24)
EXPECTED_VISIBLE = {"F1", "F4", "F6", "F7", "F8", "F9"}


def stamp_key(header):
    return header.stamp.sec, header.stamp.nanosec


class EvaluationCapture(Node):
    def __init__(self):
        super().__init__("human_perception_evaluation")
        self.image_arrivals = {}
        self.detections = {}
        self.poses = {}
        self.debug_images = {}
        self.create_subscription(Image, "/camera/image_raw", self.on_image, 20)
        self.create_subscription(
            HumanDetection2D, "/human_perception/detection", self.on_detection, 20
        )
        self.create_subscription(HumanPose2D, "/human_perception/pose", self.on_pose, 20)
        self.create_subscription(Image, "/human_perception/debug/image", self.on_debug_image, 10)

    def on_image(self, message):
        self.image_arrivals[stamp_key(message.header)] = time.monotonic()

    def on_detection(self, message):
        self.detections[stamp_key(message.header)] = message

    def on_pose(self, message):
        self.poses[stamp_key(message.header)] = message

    def on_debug_image(self, message):
        self.debug_images[stamp_key(message.header)] = message


def main(args=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--scenario", required=True, choices=sorted(EXPECTED_VISIBLE | {"F2", "F3", "F5"}),
        help="Scenario label, e.g. F1 or F5",
    )
    parser.add_argument("--frames", type=int, default=30)
    parser.add_argument("--timeout", type=float, default=20.0)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--debug-output", type=Path)
    options = parser.parse_args(args)

    rclpy.init()
    node = EvaluationCapture()
    deadline = time.monotonic() + options.timeout
    try:
        while time.monotonic() < deadline:
            rclpy.spin_once(node, timeout_sec=0.1)
            paired_count = len(node.image_arrivals.keys() & node.detections.keys())
            if paired_count >= options.frames:
                break
        else:
            raise TimeoutError(
                f"Received {len(node.image_arrivals)} images and "
                f"{len(node.detections)} detections before timeout"
            )

        paired_stamps = sorted(node.image_arrivals.keys() & node.detections.keys())[-options.frames :]
        visible_expected = options.scenario.upper() in EXPECTED_VISIBLE
        detected = [node.detections[stamp].detected for stamp in paired_stamps]
        correct = sum(value == visible_expected for value in detected)
        keypoint_scores = []
        for stamp in paired_stamps:
            pose = node.poses.get(stamp)
            if pose is None:
                continue
            keypoint_scores.extend(
                pose.confidences[index]
                for index in IMPORTANT_KEYPOINTS
                if index < len(pose.confidences)
            )

        arrivals = [node.image_arrivals[stamp] for stamp in paired_stamps]
        elapsed = arrivals[-1] - arrivals[0] if len(arrivals) > 1 else 0.0
        image_rate = (len(arrivals) - 1) / elapsed if elapsed > 0 else 0.0
        latency = [node.detections[stamp].inference_latency_ms for stamp in paired_stamps]
        result = {
            "scenario": options.scenario.upper(),
            "expected_visible": visible_expected,
            "frames_evaluated": len(paired_stamps),
            "camera_delivery_hz": round(image_rate, 3),
            "detection_rate": round(sum(detected) / len(detected), 3) if detected else 0.0,
            "correct_visibility_rate": round(correct / len(detected), 3) if detected else 0.0,
            "false_positive_count": sum(detected) if not visible_expected else 0,
            "false_negative_count": len(detected) - sum(detected) if visible_expected else 0,
            "mean_keypoint_confidence": round(sum(keypoint_scores) / len(keypoint_scores), 3)
            if keypoint_scores
            else None,
            "important_keypoints_over_0_5_per_pose": [
                sum(
                    index < len(node.poses[stamp].confidences)
                    and node.poses[stamp].confidences[index] >= 0.5
                    for index in IMPORTANT_KEYPOINTS
                )
                for stamp in paired_stamps
                if stamp in node.poses
            ],
            "mean_inference_latency_ms": round(sum(latency) / len(latency), 2)
            if latency
            else None,
            "predictions_are_model_outputs": True,
        }
        options.output.parent.mkdir(parents=True, exist_ok=True)
        options.output.write_text(json.dumps(result, indent=2) + "\n")
        if options.debug_output:
            debug_stamp = next(
                (stamp for stamp in reversed(paired_stamps) if stamp in node.debug_images), None
            )
            if debug_stamp is None:
                raise RuntimeError("No paired debug image arrived for the evaluated frames")
            debug_message = node.debug_images[debug_stamp]
            debug = np.frombuffer(debug_message.data, dtype=np.uint8).reshape(
                debug_message.height, debug_message.step
            )[:, : debug_message.width * 3].reshape(
                debug_message.height, debug_message.width, 3
            )
            options.debug_output.parent.mkdir(parents=True, exist_ok=True)
            if not cv2.imwrite(str(options.debug_output), debug):
                raise OSError(f"Could not write debug image: {options.debug_output}")
            result["debug_image"] = str(options.debug_output)
            options.output.write_text(json.dumps(result, indent=2) + "\n")
        print(json.dumps(result, indent=2))
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == "__main__":
    main()
