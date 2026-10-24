from setuptools import setup

package_name = "unity_tf_bridge"

setup(
    name=package_name,
    version="0.1.0",
    packages=[package_name],
    data_files=[
        ("share/ament_index/resource_index/packages", ["resource/" + package_name]),
        ("share/" + package_name, ["package.xml"]),
    ],
    install_requires=["setuptools"],
    zip_safe=True,
    maintainer="3D-pointer maintainers",
    maintainer_email="robotics@example.com",
    description="Relays Unity camera static TF with ROS 2 transient-local durability.",
    license="Apache-2.0",
    entry_points={
        "console_scripts": [
            "static_tf_relay = unity_tf_bridge.static_tf_relay:main",
        ],
    },
)
