#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WS_DIR="$ROOT_DIR/HumanoidSimulation/ros2_ws"
unset CONDA_PREFIX CONDA_DEFAULT_ENV CONDA_PYTHON_EXE PYTHONHOME PYTHONPATH || true
export PATH="/opt/ros/jazzy/bin:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin"

if [[ ! -f /opt/ros/jazzy/setup.bash ]]; then
    echo "ROS 2 Jazzy is missing at /opt/ros/jazzy." >&2
    exit 1
fi
if [[ ! -f "$WS_DIR/install/setup.bash" ]]; then
    echo "ROS workspace is not built. Run scripts/setup_ros_workspace.sh first." >&2
    exit 1
fi

set +u
source /opt/ros/jazzy/setup.bash
source "$WS_DIR/install/setup.bash"
set -u

if ! ros2 pkg executables humanoid_navigation | grep -q '^humanoid_navigation navigate_to_goal$'; then
    echo "Build the navigation package with scripts/setup_ros_workspace.sh first." >&2
    exit 1
fi
if pgrep -af '[n]avigate_to_goal' >/dev/null; then
    echo "A humanoid_navigation node is already running:" >&2
    pgrep -af '[n]avigate_to_goal' >&2
    exit 1
fi

exec ros2 run humanoid_navigation navigate_to_goal
