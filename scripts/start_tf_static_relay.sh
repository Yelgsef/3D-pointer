#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WS_DIR="$ROOT_DIR/HumanoidSimulation/ros2_ws"

unset CONDA_PREFIX CONDA_DEFAULT_ENV CONDA_PYTHON_EXE PYTHONHOME PYTHONPATH || true
export PATH="/opt/ros/jazzy/bin:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin"

if [[ ! -f /opt/ros/jazzy/setup.bash || ! -f "$WS_DIR/install/setup.bash" ]]; then
    echo "Build the ROS workspace with ./scripts/setup_ros_workspace.sh first." >&2
    exit 1
fi

set +u
source /opt/ros/jazzy/setup.bash
source "$WS_DIR/install/setup.bash"
set -u

exec ros2 run unity_tf_bridge static_tf_relay
