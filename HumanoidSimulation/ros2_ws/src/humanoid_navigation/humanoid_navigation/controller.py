#!/usr/bin/env python3
import math
import time

import rclpy
from geometry_msgs.msg import Pose, PoseStamped, Twist
from rclpy.node import Node
from std_msgs.msg import Bool


def normalize_angle(angle):
    return math.atan2(math.sin(angle), math.cos(angle))


def compute_command(
    robot_x,
    robot_y,
    robot_yaw,
    goal_x,
    goal_y,
    tolerance=0.15,
    linear_gain=0.8,
    angular_gain=2.0,
    max_linear=1.2,
    max_angular=1.2,
    drive_heading_limit=1.0,
):
    """Return linear speed, yaw rate, and whether the goal is reached."""
    delta_x = goal_x - robot_x
    delta_y = goal_y - robot_y
    distance = math.hypot(delta_x, delta_y)
    if distance <= tolerance:
        return 0.0, 0.0, True

    desired_yaw = math.atan2(delta_y, delta_x)
    yaw_error = normalize_angle(desired_yaw - robot_yaw)
    angular = max(-max_angular, min(max_angular, angular_gain * yaw_error))
    if abs(yaw_error) > drive_heading_limit:
        linear = 0.0
    else:
        linear = min(max_linear, linear_gain * distance) * max(0.0, math.cos(yaw_error))
    return linear, angular, False


class NavigationController(Node):
    def __init__(self):
        super().__init__("humanoid_navigation")
        self.declare_parameter("goal_topic", "/goal_pose")
        self.declare_parameter("pose_topic", "/robot/pose")
        self.declare_parameter("command_topic", "/cmd_vel")
        self.declare_parameter("cancel_topic", "/goal_cancel")
        self.declare_parameter("frame_id", "map")
        self.declare_parameter("position_tolerance", 0.15)
        self.declare_parameter("pose_timeout", 1.0)
        self.declare_parameter("linear_gain", 0.8)
        self.declare_parameter("angular_gain", 2.0)
        self.declare_parameter("max_linear_speed", 1.2)
        self.declare_parameter("max_angular_speed", 1.2)
        self.declare_parameter("drive_heading_limit", 1.0)
        self.declare_parameter("control_rate", 10.0)

        self.frame_id = self.get_parameter("frame_id").value
        self.tolerance = self.get_parameter("position_tolerance").value
        self.pose_timeout = self.get_parameter("pose_timeout").value
        self.linear_gain = self.get_parameter("linear_gain").value
        self.angular_gain = self.get_parameter("angular_gain").value
        self.max_linear = self.get_parameter("max_linear_speed").value
        self.max_angular = self.get_parameter("max_angular_speed").value
        self.drive_heading_limit = self.get_parameter("drive_heading_limit").value

        self.command_publisher = self.create_publisher(
            Twist, self.get_parameter("command_topic").value, 10
        )
        self.create_subscription(
            PoseStamped, self.get_parameter("goal_topic").value, self.on_goal, 10
        )
        self.create_subscription(
            Pose, self.get_parameter("pose_topic").value, self.on_pose, 10
        )
        self.create_subscription(
            Bool, self.get_parameter("cancel_topic").value, self.on_cancel, 10
        )

        self.goal = None
        self.pose = None
        self.pose_received_at = None
        self.create_timer(1.0 / self.get_parameter("control_rate").value, self.control_tick)
        self.publish_stop()
        self.get_logger().info(
            f"Ready for {self.get_parameter('goal_topic').value} goals in {self.frame_id}."
        )

    def on_goal(self, message):
        if message.header.frame_id and message.header.frame_id != self.frame_id:
            self.get_logger().warning(
                f"Ignoring goal in frame '{message.header.frame_id}'; expected '{self.frame_id}'."
            )
            return

        x = message.pose.position.x
        y = message.pose.position.y
        if not math.isfinite(x) or not math.isfinite(y):
            self.get_logger().warning("Ignoring goal with non-finite coordinates.")
            return

        self.goal = (x, y)
        self.get_logger().info(f"New goal accepted at ({x:.2f}, {y:.2f}).")

    def on_pose(self, message):
        position = message.position
        orientation = message.orientation
        values = (position.x, position.y, orientation.x, orientation.y, orientation.z, orientation.w)
        if not all(math.isfinite(value) for value in values):
            return

        norm = math.sqrt(sum(value * value for value in values[2:]))
        if norm < 1e-6:
            return

        x, y, qx, qy, qz, qw = values
        yaw = math.atan2(2.0 * (qw * qz + qx * qy), 1.0 - 2.0 * (qy * qy + qz * qz))
        self.pose = (x, y, yaw)
        self.pose_received_at = time.monotonic()

    def on_cancel(self, message):
        if message.data:
            self.goal = None
            self.publish_stop()
            self.get_logger().info("Navigation goal cancelled; published stop command.")

    def control_tick(self):
        if self.goal is None:
            return

        if self.pose is None or self.pose_received_at is None:
            self.goal = None
            self.publish_stop()
            self.get_logger().warning("No pose feedback; cancelled the requested goal.")
            return

        if time.monotonic() - self.pose_received_at > self.pose_timeout:
            self.goal = None
            self.publish_stop()
            self.get_logger().warning("Pose feedback is stale; cancelled the requested goal.")
            return

        linear, angular, reached = compute_command(
            *self.pose,
            *self.goal,
            tolerance=self.tolerance,
            linear_gain=self.linear_gain,
            angular_gain=self.angular_gain,
            max_linear=self.max_linear,
            max_angular=self.max_angular,
            drive_heading_limit=self.drive_heading_limit,
        )
        if reached:
            self.goal = None
            self.publish_stop()
            self.get_logger().info("Goal reached within the configured position tolerance.")
            return

        command = Twist()
        command.linear.x = linear
        command.angular.z = angular
        self.command_publisher.publish(command)

    def publish_stop(self):
        self.command_publisher.publish(Twist())


def main(args=None):
    rclpy.init(args=args)
    node = NavigationController()
    try:
        rclpy.spin(node)
    except KeyboardInterrupt:
        pass
    finally:
        if rclpy.ok():
            node.publish_stop()
        node.destroy_node()
        if rclpy.ok():
            rclpy.shutdown()


if __name__ == "__main__":
    main()
