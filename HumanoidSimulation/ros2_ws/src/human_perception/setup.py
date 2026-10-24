from setuptools import find_packages, setup

package_name = "human_perception"

setup(
    name=package_name,
    version="0.1.0",
    packages=find_packages(exclude=("test",)),
    data_files=[
        ("share/ament_index/resource_index/packages", ["resource/" + package_name]),
        ("share/" + package_name, ["package.xml"]),
    ],
    install_requires=["setuptools"],
    zip_safe=True,
    maintainer="Unity ROS Simulation",
    maintainer_email="maintainer@example.com",
    description="MediaPipe RGB human detection and 2D pose estimation node.",
    license="Apache-2.0",
    entry_points={
        "console_scripts": [
            "human_pose_detector = human_perception.pose_detector:main",
            "human_pose_3d = human_perception.human_pose_3d_node:main",
            "pointing_estimator = human_perception.pointing_estimator:main",
            "evaluate_pointing = human_perception.evaluate_pointing:main",
            "evaluate_perception = human_perception.evaluate:main",
        ],
    },
)
