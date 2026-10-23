from setuptools import find_packages, setup

package_name = "humanoid_navigation"

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
    description="Basic proportional waypoint control for the Unity humanoid.",
    license="Apache-2.0",
    entry_points={
        "console_scripts": [
            "navigate_to_goal = humanoid_navigation.controller:main",
        ],
    },
)
