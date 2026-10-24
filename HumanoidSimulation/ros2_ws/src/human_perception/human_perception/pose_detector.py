#!/usr/bin/env python3
"""Detect one person and estimate 2D pose from the robot's RGB camera stream."""

import time

import cv2
import mediapipe as mp
import numpy as np
import rclpy
from geometry_msgs.msg import Point32
from human_perception_msgs.msg import HumanDetection2D, HumanPose2D
from rclpy.node import Node
from sensor_msgs.msg import Image


KEYPOINT_NAMES = (
    "nose", "left_eye_inner", "left_eye", "left_eye_outer", "right_eye_inner",
    "right_eye", "right_eye_outer", "left_ear", "right_ear", "mouth_left",
    "mouth_right", "left_shoulder", "right_shoulder", "left_elbow", "right_elbow",
    "left_wrist", "right_wrist", "left_pinky", "right_pinky", "left_index",
    "right_index", "left_thumb", "right_thumb", "left_hip", "right_hip",
    "left_knee", "right_knee", "left_ankle", "right_ankle", "left_heel",
    "right_heel", "left_foot_index", "right_foot_index",
)

BOX_VISIBILITY_THRESHOLD = 0.20
CONFIDENCE_LANDMARKS = (11, 12, 13, 14, 15, 16, 23, 24, 25, 26, 27, 28)


class HumanPoseDetector(Node):
    def __init__(self):
        super().__init__("human_pose_detector")
        self.declare_parameter("image_topic", "/camera/image_raw")
        self.declare_parameter("detection_topic", "/human_perception/detection")
        self.declare_parameter("pose_topic", "/human_perception/pose")
        self.declare_parameter("debug_image_topic", "/human_perception/debug/image")
        self.declare_parameter("confidence_threshold", 0.50)
        self.declare_parameter("model_complexity", 1)
        self.declare_parameter("min_detection_confidence", 0.40)
        self.declare_parameter("min_tracking_confidence", 0.40)

        image_topic = self.get_parameter("image_topic").value
        self.confidence_threshold = float(self.get_parameter("confidence_threshold").value)
        model_complexity = int(self.get_parameter("model_complexity").value)
        detection_threshold = float(self.get_parameter("min_detection_confidence").value)
        tracking_threshold = float(self.get_parameter("min_tracking_confidence").value)

        self.detection_pub = self.create_publisher(
            HumanDetection2D, self.get_parameter("detection_topic").value, 10
        )
        self.pose_pub = self.create_publisher(HumanPose2D, self.get_parameter("pose_topic").value, 10)
        self.debug_pub = self.create_publisher(
            Image, self.get_parameter("debug_image_topic").value, 3
        )
        self.pose_estimator = mp.solutions.pose.Pose(
            static_image_mode=False,
            model_complexity=model_complexity,
            smooth_landmarks=True,
            enable_segmentation=False,
            min_detection_confidence=detection_threshold,
            min_tracking_confidence=tracking_threshold,
        )
        self.subscription = self.create_subscription(Image, image_topic, self.on_image, 3)
        self.get_logger().info(
            f"Listening to {image_topic}; MediaPipe Pose complexity={model_complexity}, "
            f"confidence threshold={self.confidence_threshold:.2f}"
        )

    def on_image(self, image):
        started = time.perf_counter()
        try:
            rgb = self.image_to_rgb(image)
        except ValueError as error:
            self.get_logger().warning(str(error))
            return

        results = self.pose_estimator.process(rgb)
        landmarks = results.pose_landmarks.landmark if results.pose_landmarks else None
        confidence = self.detection_confidence(landmarks) if landmarks else 0.0
        detected = landmarks is not None and confidence >= self.confidence_threshold
        latency_ms = (time.perf_counter() - started) * 1000.0

        detection = HumanDetection2D()
        detection.header = image.header
        detection.image_width = image.width
        detection.image_height = image.height
        detection.detected = detected
        detection.confidence = confidence
        detection.inference_latency_ms = latency_ms
        if detected:
            detection.bounding_box = self.bounding_box(landmarks, image.width, image.height)
        self.detection_pub.publish(detection)

        pose_message = HumanPose2D()
        pose_message.header = image.header
        pose_message.image_width = image.width
        pose_message.image_height = image.height
        pose_message.keypoint_names = list(KEYPOINT_NAMES)
        if landmarks is not None:
            for point in landmarks:
                output = Point32()
                output.x = float(point.x * image.width)
                output.y = float(point.y * image.height)
                output.z = float(point.z)
                pose_message.keypoints.append(output)
                pose_message.confidences.append(float(point.visibility))
        else:
            pose_message.keypoints = [Point32() for _ in KEYPOINT_NAMES]
            pose_message.confidences = [0.0] * len(KEYPOINT_NAMES)
        self.pose_pub.publish(pose_message)

        self.publish_debug_image(image, rgb, landmarks, detection)

    @staticmethod
    def image_to_rgb(image):
        if image.encoding != "rgb8":
            raise ValueError(f"Expected rgb8 on /camera/image_raw, received {image.encoding}")
        minimum_size = image.step * image.height
        if image.step < image.width * 3 or len(image.data) < minimum_size:
            raise ValueError("Received malformed RGB image buffer")
        return np.ndarray(
            shape=(image.height, image.width, 3),
            dtype=np.uint8,
            buffer=memoryview(image.data),
            strides=(image.step, 3, 1),
        )

    @staticmethod
    def detection_confidence(landmarks):
        return float(np.mean([landmarks[index].visibility for index in CONFIDENCE_LANDMARKS]))

    @staticmethod
    def bounding_box(landmarks, width, height):
        visible = [point for point in landmarks if point.visibility >= BOX_VISIBILITY_THRESHOLD]
        if not visible:
            visible = landmarks
        left = min(point.x for point in visible) * width
        right = max(point.x for point in visible) * width
        top = min(point.y for point in visible) * height
        bottom = max(point.y for point in visible) * height
        margin_x = (right - left) * 0.08
        margin_y = (bottom - top) * 0.08
        x0 = max(0, int(np.floor(left - margin_x)))
        y0 = max(0, int(np.floor(top - margin_y)))
        x1 = min(width, int(np.ceil(right + margin_x)))
        y1 = min(height, int(np.ceil(bottom + margin_y)))
        from sensor_msgs.msg import RegionOfInterest

        return RegionOfInterest(
            x_offset=x0,
            y_offset=y0,
            width=max(0, x1 - x0),
            height=max(0, y1 - y0),
            do_rectify=False,
        )

    def publish_debug_image(self, source, rgb, landmarks, detection):
        debug = cv2.cvtColor(rgb, cv2.COLOR_RGB2BGR)
        height, width = debug.shape[:2]
        if detection.detected:
            roi = detection.bounding_box
            cv2.rectangle(
                debug,
                (roi.x_offset, roi.y_offset),
                (roi.x_offset + roi.width, roi.y_offset + roi.height),
                (30, 220, 40),
                2,
            )
            cv2.putText(
                debug,
                f"human {detection.confidence:.2f}",
                (roi.x_offset, max(18, roi.y_offset - 6)),
                cv2.FONT_HERSHEY_SIMPLEX,
                0.5,
                (30, 220, 40),
                1,
                cv2.LINE_AA,
            )
        if landmarks is not None:
            for start, end in mp.solutions.pose.POSE_CONNECTIONS:
                a, b = landmarks[start], landmarks[end]
                if min(a.visibility, b.visibility) < BOX_VISIBILITY_THRESHOLD:
                    continue
                pa = (int(a.x * width), int(a.y * height))
                pb = (int(b.x * width), int(b.y * height))
                cv2.line(debug, pa, pb, (255, 190, 30), 2, cv2.LINE_AA)
            for point in landmarks:
                if point.visibility >= BOX_VISIBILITY_THRESHOLD:
                    cv2.circle(
                        debug,
                        (int(point.x * width), int(point.y * height)),
                        3,
                        (30, 40, 255),
                        -1,
                    )

        output = Image()
        output.header = source.header
        output.height = source.height
        output.width = source.width
        output.encoding = "bgr8"
        output.is_bigendian = 0
        output.step = source.width * 3
        output.data = debug.tobytes()
        self.debug_pub.publish(output)

    def destroy_node(self):
        self.pose_estimator.close()
        super().destroy_node()


def main(args=None):
    rclpy.init(args=args)
    node = HumanPoseDetector()
    try:
        rclpy.spin(node)
    except KeyboardInterrupt:
        pass
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == "__main__":
    main()
