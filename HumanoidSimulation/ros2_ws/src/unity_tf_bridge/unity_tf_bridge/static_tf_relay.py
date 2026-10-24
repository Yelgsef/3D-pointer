#!/usr/bin/env python3
"""Retain Unity static transforms for late ROS 2 TF subscribers."""

import rclpy
from rclpy.node import Node
from rclpy.qos import DurabilityPolicy, QoSProfile, ReliabilityPolicy
from tf2_msgs.msg import TFMessage


class StaticTfRelay(Node):
    def __init__(self):
        super().__init__("unity_static_tf_relay")
        self.transforms = {}
        self.publisher = self.create_publisher(
            TFMessage,
            "/tf_static",
            QoSProfile(
                depth=1,
                reliability=ReliabilityPolicy.RELIABLE,
                durability=DurabilityPolicy.TRANSIENT_LOCAL,
            ),
        )
        self.subscription = self.create_subscription(
            TFMessage, "/unity/tf_static", self.receive, 10
        )

    def receive(self, message):
        for transform in message.transforms:
            if not transform.header.frame_id or not transform.child_frame_id:
                self.get_logger().warning("Ignoring a static transform with an empty frame ID")
                continue
            if transform.header.frame_id == transform.child_frame_id:
                self.get_logger().warning("Ignoring a static transform whose parent equals its child")
                continue
            self.transforms[transform.child_frame_id] = transform

        if self.transforms:
            self.publisher.publish(TFMessage(transforms=list(self.transforms.values())))


def main(args=None):
    rclpy.init(args=args)
    node = StaticTfRelay()
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
