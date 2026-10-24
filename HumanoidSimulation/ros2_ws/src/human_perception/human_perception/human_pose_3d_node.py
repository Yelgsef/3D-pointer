#!/usr/bin/env python3
"""Reconstruct MediaPipe pixels with synchronized metric RGB-D observations."""

import numpy as np
import rclpy
from geometry_msgs.msg import Point
from human_perception_msgs.msg import HumanPose2D, HumanPose3D
from message_filters import ApproximateTimeSynchronizer, Subscriber
from rclpy.executors import ExternalShutdownException
from rclpy.node import Node
from sensor_msgs.msg import CameraInfo, Image

from human_perception.rgbd_reconstruction import back_project, sample_depth


class HumanPose3DNode(Node):
    def __init__(self):
        super().__init__("human_pose_3d")
        self.declare_parameter("pose_topic", "/human_perception/pose")
        self.declare_parameter("depth_topic", "/camera/depth/image_raw")
        self.declare_parameter("camera_info_topic", "/camera/camera_info")
        self.declare_parameter("output_topic", "/human_perception/pose_3d")
        self.declare_parameter("optical_frame", "robot_camera_optical_frame")
        self.declare_parameter("confidence_threshold", 0.50)
        self.declare_parameter("sync_slop_seconds", 0.10)

        pose_topic = self.get_parameter("pose_topic").value
        depth_topic = self.get_parameter("depth_topic").value
        info_topic = self.get_parameter("camera_info_topic").value
        self.optical_frame = self.get_parameter("optical_frame").value
        self.confidence_threshold = float(self.get_parameter("confidence_threshold").value)

        self.publisher = self.create_publisher(
            HumanPose3D, self.get_parameter("output_topic").value, 10
        )
        self.pose_sub = Subscriber(self, HumanPose2D, pose_topic)
        self.depth_sub = Subscriber(self, Image, depth_topic)
        self.info_sub = Subscriber(self, CameraInfo, info_topic)
        self.synchronizer = ApproximateTimeSynchronizer(
            [self.pose_sub, self.depth_sub, self.info_sub], queue_size=20,
            slop=float(self.get_parameter("sync_slop_seconds").value),
        )
        self.synchronizer.registerCallback(self.on_observation)
        self.get_logger().info(
            f"Synchronizing {pose_topic}, {depth_topic}, and {info_topic}; "
            f"publishing optical-frame keypoints to {self.get_parameter('output_topic').value}"
        )

    def on_observation(self, pose, depth_image, camera_info):
        output = HumanPose3D()
        output.header = pose.header
        output.header.frame_id = self.optical_frame
        output.keypoint_names = list(pose.keypoint_names)
        output.confidences = list(pose.confidences)

        try:
            depth = self.decode_depth(depth_image)
        except ValueError as error:
            self.get_logger().warning(str(error))
            self.publish_invalid(output, len(pose.keypoints))
            return

        if (depth_image.width != pose.image_width or depth_image.height != pose.image_height or
                camera_info.width != depth_image.width or camera_info.height != depth_image.height):
            self.get_logger().warning("Synchronized RGB, depth, and CameraInfo dimensions differ")
            self.publish_invalid(output, len(pose.keypoints))
            return
        if camera_info.header.frame_id != depth_image.header.frame_id:
            self.get_logger().warning("Depth and CameraInfo frame IDs differ")
            self.publish_invalid(output, len(pose.keypoints))
            return

        fx, fy = camera_info.k[0], camera_info.k[4]
        cx, cy = camera_info.k[2], camera_info.k[5]
        for index, pixel in enumerate(pose.keypoints):
            confidence = pose.confidences[index] if index < len(pose.confidences) else 0.0
            if confidence < self.confidence_threshold:
                self.append_invalid(output)
                continue
            z = sample_depth(depth, pixel.x, pixel.y)
            if z is None or pixel.x < 0 or pixel.y < 0 or pixel.x >= depth.shape[1] or pixel.y >= depth.shape[0]:
                self.append_invalid(output)
                continue
            x, y, z = back_project(pixel.x, pixel.y, z, fx, fy, cx, cy)
            point = Point(x=x, y=y, z=z)
            output.keypoints.append(point)
            output.valid.append(True)
        self.publisher.publish(output)

    @staticmethod
    def decode_depth(message):
        if message.encoding != "32FC1":
            raise ValueError(f"Expected 32FC1 depth, received {message.encoding}")
        if message.step < message.width * 4 or len(message.data) < message.step * message.height:
            raise ValueError("Received malformed 32FC1 depth buffer")
        dtype = np.dtype(">f4" if message.is_bigendian else "<f4")
        return np.ndarray(
            shape=(message.height, message.width), dtype=dtype,
            buffer=memoryview(message.data), strides=(message.step, 4),
        ).astype(np.float32, copy=False)

    def publish_invalid(self, output, count):
        output.keypoints = [Point() for _ in range(count)]
        output.valid = [False] * count
        self.publisher.publish(output)

    @staticmethod
    def append_invalid(output):
        output.keypoints.append(Point())
        output.valid.append(False)


def main(args=None):
    rclpy.init(args=args)
    node = HumanPose3DNode()
    try:
        rclpy.spin(node)
    except (KeyboardInterrupt, ExternalShutdownException):
        pass
    finally:
        node.destroy_node()
        if rclpy.ok():
            rclpy.shutdown()


if __name__ == "__main__":
    main()
