#!/usr/bin/env python3
"""Rank known candidate objects from predicted RGB-D pose only."""

import json

import rclpy
from geometry_msgs.msg import Point, Vector3
from human_perception_msgs.msg import HumanPose3D, PointingEstimate
from rcl_interfaces.msg import SetParametersResult
from rclpy.duration import Duration
from rclpy.node import Node
from rclpy.time import Time
from tf2_ros import Buffer, TransformException, TransformListener
from visualization_msgs.msg import Marker, MarkerArray

from human_perception.pointing_geometry import (
    DEFAULT_CANDIDATES, normalize, parse_candidates, rank_candidates,
    semantic_arm_label, transform_point,
)


class PointingEstimator(Node):
    def __init__(self):
        super().__init__("pointing_estimator")
        self.declare_parameter("input_topic", "/human_perception/pose_3d")
        self.declare_parameter("output_topic", "/human_perception/pointing_estimate")
        self.declare_parameter("marker_topic", "/human_perception/pointing_markers")
        self.declare_parameter("map_frame", "map")
        self.declare_parameter("candidate_map_json", json.dumps(DEFAULT_CANDIDATES, separators=(",", ":")))
        self.declare_parameter("minimum_margin_deg", 8.0)
        self.declare_parameter("maximum_angle_deg", 45.0)
        self.declare_parameter("swap_predicted_arm_labels", True)

        self.map_frame = self.get_parameter("map_frame").value
        self.candidates = parse_candidates(self.get_parameter("candidate_map_json").value)
        self.minimum_margin = float(self.get_parameter("minimum_margin_deg").value)
        self.maximum_angle = float(self.get_parameter("maximum_angle_deg").value)
        self.swap_arm_labels = bool(self.get_parameter("swap_predicted_arm_labels").value)
        self.publisher = self.create_publisher(
            PointingEstimate, self.get_parameter("output_topic").value, 10
        )
        self.marker_publisher = self.create_publisher(
            MarkerArray, self.get_parameter("marker_topic").value, 10
        )
        self.tf_buffer = Buffer()
        self.tf_listener = TransformListener(self.tf_buffer, self)
        self.subscription = self.create_subscription(
            HumanPose3D, self.get_parameter("input_topic").value, self.on_pose, 10
        )
        self.add_on_set_parameters_callback(self.on_parameters)

    def on_parameters(self, parameters):
        for parameter in parameters:
            if parameter.name != "candidate_map_json":
                continue
            try:
                self.candidates = parse_candidates(parameter.value)
            except (TypeError, ValueError, json.JSONDecodeError) as error:
                return SetParametersResult(successful=False, reason=str(error))
        return SetParametersResult(successful=True)

    def on_pose(self, pose):
        result = PointingEstimate()
        result.header.stamp = pose.header.stamp
        result.header.frame_id = self.map_frame
        try:
            transform = self.tf_buffer.lookup_transform(
                self.map_frame, pose.header.frame_id,
                Time.from_msg(pose.header.stamp), timeout=Duration(seconds=0.0),
            )
        except TransformException as error:
            try:
                transform = self.tf_buffer.lookup_transform(
                    self.map_frame, pose.header.frame_id, Time(),
                    timeout=Duration(seconds=0.0),
                )
                source_ns = pose.header.stamp.sec * 1_000_000_000 + pose.header.stamp.nanosec
                transform_ns = transform.header.stamp.sec * 1_000_000_000 + transform.header.stamp.nanosec
                if abs(source_ns - transform_ns) > 250_000_000:
                    raise TransformException("latest transform is more than 250 ms from the image stamp")
            except TransformException:
                self.get_logger().warning(f"No suitably timed transform for 3D pose: {error}")
                result.abstained = True
                self.publisher.publish(result)
                return

        translation = (transform.transform.translation.x,
                      transform.transform.translation.y,
                      transform.transform.translation.z)
        q = transform.transform.rotation
        quaternion = (q.x, q.y, q.z, q.w)
        points = {}
        for i, name in enumerate(pose.keypoint_names):
            if i < len(pose.valid) and pose.valid[i] and i < len(pose.keypoints):
                p = pose.keypoints[i]
                points[name] = transform_point((p.x, p.y, p.z), translation, quaternion)

        eye = self.average(points, ("left_eye", "right_eye"))
        if eye is None:
            eye = points.get("nose")
        if eye is None:
            result.abstained = True
            self.publisher.publish(result)
            self.publish_markers(pose, {}, None, "")
            return

        arm_trials = []
        for arm in ("left", "right"):
            elbow = points.get(f"{arm}_elbow")
            wrist = points.get(f"{arm}_wrist")
            if elbow is None or wrist is None:
                continue
            try:
                eye_direction = normalize(tuple(wrist[i] - eye[i] for i in range(3)))
                arm_direction = normalize(tuple(wrist[i] - elbow[i] for i in range(3)))
            except ValueError:
                continue
            ranked_a = rank_candidates(eye, eye_direction, self.candidates)
            ranked_b = rank_candidates(elbow, arm_direction, self.candidates)
            arm_trials.append((ranked_a[0][1], arm, eye_direction, ranked_a, ranked_b, eye))

        if not arm_trials:
            result.abstained = True
            self.publisher.publish(result)
            self.publish_markers(pose, points, eye, "")
            return

        _, arm, ray, ranked_a, ranked_b, ray_origin = min(arm_trials, key=lambda item: (item[0], item[1]))
        result.has_ray = True
        result.predicted_arm = semantic_arm_label(arm, self.swap_arm_labels)
        result.ray_origin = Point(x=ray_origin[0], y=ray_origin[1], z=ray_origin[2])
        result.direction = Vector3(x=ray[0], y=ray[1], z=ray[2])
        result.candidate_ids = [item[0] for item in ranked_a]
        result.angular_errors_deg = [float(item[1]) for item in ranked_a]
        result.scores = [float(1.0 - item[1] / 180.0) for item in ranked_a]
        result.method_a_target_id = ranked_a[0][0]
        result.method_b_target_id = ranked_b[0][0]
        margin = ranked_a[1][1] - ranked_a[0][1] if len(ranked_a) > 1 else 180.0
        result.ambiguous = margin < self.minimum_margin
        result.abstained = ranked_a[0][1] > self.maximum_angle or result.ambiguous
        result.selected_target_id = "" if result.abstained else ranked_a[0][0]
        self.publisher.publish(result)
        self.publish_markers(pose, points, ray_origin, result.selected_target_id, ray)

    @staticmethod
    def average(points, names):
        available = [points[name] for name in names if name in points]
        if not available:
            return None
        return tuple(sum(p[i] for p in available) / len(available) for i in range(3))

    def publish_markers(self, pose, points, origin, selected, direction=None):
        markers = MarkerArray()
        for marker_id, (candidate_id, position) in enumerate(self.candidates.items()):
            marker = Marker()
            marker.header.stamp = pose.header.stamp
            marker.header.frame_id = self.map_frame
            marker.ns, marker.id = "candidate_objects", marker_id
            marker.type, marker.action = Marker.SPHERE, Marker.ADD
            marker.pose.position = Point(x=position[0], y=position[1], z=position[2])
            marker.pose.orientation.w = 1.0
            marker.scale.x = marker.scale.y = marker.scale.z = 0.25
            marker.color.r = 0.1 if candidate_id == selected else 0.8
            marker.color.g = 0.9 if candidate_id == selected else 0.5
            marker.color.b, marker.color.a = 0.1, 1.0
            markers.markers.append(marker)

            label = Marker()
            label.header = marker.header
            label.ns, label.id = "candidate_labels", marker_id
            label.type, label.action = Marker.TEXT_VIEW_FACING, Marker.ADD
            label.pose.position = Point(x=position[0], y=position[1], z=position[2] + 0.25)
            label.pose.orientation.w = 1.0
            label.scale.z = 0.18
            label.color.r = label.color.g = label.color.b = label.color.a = 1.0
            label.text = candidate_id
            markers.markers.append(label)

        skeleton = Marker()
        skeleton.header.stamp = pose.header.stamp
        skeleton.header.frame_id = self.map_frame
        skeleton.ns, skeleton.id = "estimated_skeleton", 0
        skeleton.type, skeleton.action = Marker.LINE_LIST, Marker.ADD
        skeleton.scale.x = 0.025
        skeleton.color.r, skeleton.color.g, skeleton.color.b, skeleton.color.a = 0.1, 0.8, 1.0, 1.0
        for arm in ("left", "right"):
            for start, end in ((f"{arm}_shoulder", f"{arm}_elbow"),
                               (f"{arm}_elbow", f"{arm}_wrist")):
                if start in points and end in points:
                    skeleton.points.extend((self.to_point(points[start]), self.to_point(points[end])))
        markers.markers.append(skeleton)

        if origin is not None and direction is not None:
            ray = Marker()
            ray.header.stamp = pose.header.stamp
            ray.header.frame_id = self.map_frame
            ray.ns, ray.id = "estimated_pointing_ray", 0
            ray.type, ray.action = Marker.ARROW, Marker.ADD
            ray_end = tuple(origin[i] + direction[i] * 4.0 for i in range(3))
            ray.points = [self.to_point(origin), self.to_point(ray_end)]
            ray.scale.x, ray.scale.y, ray.scale.z = 0.04, 0.08, 0.12
            ray.color.r, ray.color.g, ray.color.b, ray.color.a = 1.0, 0.6, 0.1, 1.0
            markers.markers.append(ray)
        self.marker_publisher.publish(markers)

    @staticmethod
    def to_point(vector):
        return Point(x=vector[0], y=vector[1], z=vector[2])


def main(args=None):
    rclpy.init(args=args)
    node = PointingEstimator()
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
