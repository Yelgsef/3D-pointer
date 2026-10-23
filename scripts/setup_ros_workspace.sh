#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WS_DIR="$ROOT_DIR/HumanoidSimulation/ros2_ws"
ENDPOINT_DIR="$WS_DIR/src/ros_tcp_endpoint"

unset CONDA_PREFIX CONDA_DEFAULT_ENV CONDA_PYTHON_EXE PYTHONHOME PYTHONPATH || true
export PATH="/opt/ros/jazzy/bin:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin"

if [[ ! -f /opt/ros/jazzy/setup.bash ]]; then
    echo "ROS 2 Jazzy was not found at /opt/ros/jazzy." >&2
    exit 1
fi
set +u
source /opt/ros/jazzy/setup.bash
set -u
mkdir -p "$WS_DIR/src"

if [[ ! -f "$ENDPOINT_DIR/package.xml" ]]; then
    if [[ -e "$ENDPOINT_DIR" ]]; then
        echo "Endpoint path exists but is not a ROS package: $ENDPOINT_DIR" >&2
        exit 1
    fi
    git clone --depth 1 --branch main-ros2 https://github.com/Unity-Technologies/ROS-TCP-Endpoint.git "$ENDPOINT_DIR"
fi

# ament_python is a ROS build type, not a rosdep system package key here.
rosdep install --from-paths "$WS_DIR/src" --ignore-src -r -y --skip-keys ament_python
cd "$WS_DIR"
colcon build --symlink-install
