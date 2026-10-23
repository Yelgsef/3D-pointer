#!/usr/bin/env bash
set -euo pipefail

unset CONDA_PREFIX CONDA_DEFAULT_ENV CONDA_PYTHON_EXE PYTHONHOME PYTHONPATH || true
export PATH="/opt/ros/jazzy/bin:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin"

if [[ ! -f /opt/ros/jazzy/setup.bash ]]; then
    echo "ROS 2 Jazzy is missing at /opt/ros/jazzy." >&2
    exit 1
fi
set +u
source /opt/ros/jazzy/setup.bash
set -u

if ! ros2 pkg executables teleop_twist_keyboard | grep -q '^teleop_twist_keyboard teleop_twist_keyboard$'; then
    echo "Install teleop with: sudo apt install ros-jazzy-teleop-twist-keyboard" >&2
    exit 1
fi
exec ros2 run teleop_twist_keyboard teleop_twist_keyboard --ros-args -r cmd_vel:=/cmd_vel
