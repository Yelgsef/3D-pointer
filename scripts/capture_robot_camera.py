#!/usr/bin/env python3
"""Validate one Unity RGB/CameraInfo stream and optionally save a PNG sample."""

import argparse
import json
import time
from pathlib import Path

import cv2
import numpy as np
import rclpy
from rclpy.node import Node
from sensor_msgs.msg import CameraInfo, Image


def stamp_key(header):
    return header.stamp.sec, header.stamp.nanosec


class CameraCapture(Node):
    def __init__(self, image_topic, camera_info_topic):
        super().__init__("robot_camera_capture")
        self.images = []
        self.camera_info = {}
        self.image_arrival_times = []
        self.create_subscription(Image, image_topic, self.on_image, 10)
        self.create_subscription(CameraInfo, camera_info_topic, self.on_camera_info, 10)

    def on_image(self, message):
        self.images.append(message)
        self.image_arrival_times.append(time.monotonic())

    def on_camera_info(self, message):
        self.camera_info[stamp_key(message.header)] = message


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--image-topic", default="/camera/image_raw")
    parser.add_argument("--camera-info-topic", default="/camera/camera_info")
    parser.add_argument("--frames", type=int, default=12)
    parser.add_argument("--timeout", type=float, default=20.0)
    parser.add_argument("--output", type=Path, help="Optional PNG destination")
    args = parser.parse_args()

    rclpy.init()
    node = CameraCapture(args.image_topic, args.camera_info_topic)
    deadline = time.monotonic() + args.timeout
    try:
        while time.monotonic() < deadline:
            rclpy.spin_once(node, timeout_sec=0.2)
            if len(node.images) >= args.frames:
                image = node.images[-1]
                if stamp_key(image.header) in node.camera_info:
                    break
        else:
            raise TimeoutError("Timed out before paired image and CameraInfo samples arrived")

        image = node.images[-1]
        camera_info = node.camera_info[stamp_key(image.header)]
        expected_step = image.width * 3
        expected_bytes = image.step * image.height
        if image.encoding != "rgb8":
            raise ValueError("Expected rgb8, received " + image.encoding)
        if image.step != expected_step or len(image.data) != expected_bytes:
            raise ValueError(
                f"Malformed image: width={image.width}, height={image.height}, "
                f"step={image.step}, data={len(image.data)}"
            )
        if image.header.frame_id != camera_info.header.frame_id:
            raise ValueError("Image and CameraInfo frame IDs differ")
        if stamp_key(image.header) != stamp_key(camera_info.header):
            raise ValueError("Image and CameraInfo timestamps differ")
        if camera_info.width != image.width or camera_info.height != image.height:
            raise ValueError("Image and CameraInfo dimensions differ")

        rgb = np.frombuffer(image.data, dtype=np.uint8).reshape(image.height, image.width, 3)
        if args.output:
            args.output.parent.mkdir(parents=True, exist_ok=True)
            if not cv2.imwrite(str(args.output), cv2.cvtColor(rgb, cv2.COLOR_RGB2BGR)):
                raise OSError("OpenCV could not write " + str(args.output))

        elapsed = node.image_arrival_times[-1] - node.image_arrival_times[0]
        measured_rate = (len(node.image_arrival_times) - 1) / elapsed if elapsed > 0 else 0.0
        report = {
            "images_received": len(node.images),
            "measured_rate_hz": round(measured_rate, 3),
            "encoding": image.encoding,
            "width": image.width,
            "height": image.height,
            "step": image.step,
            "data_bytes": len(image.data),
            "frame_id": image.header.frame_id,
            "stamp": {
                "sec": image.header.stamp.sec,
                "nanosec": image.header.stamp.nanosec,
            },
            "camera_info": {
                "distortion_model": camera_info.distortion_model,
                "d": list(camera_info.d),
                "fx": camera_info.k[0],
                "fy": camera_info.k[4],
                "cx": camera_info.k[2],
                "cy": camera_info.k[5],
            },
            "png": str(args.output) if args.output else None,
        }
        print(json.dumps(report, indent=2))
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == "__main__":
    main()
