#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WS_DIR="$ROOT_DIR/HumanoidSimulation/ros2_ws"
unset CONDA_PREFIX CONDA_DEFAULT_ENV CONDA_PYTHON_EXE PYTHONHOME PYTHONPATH || true
export PATH="/opt/ros/jazzy/bin:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin"

if [[ ! -f /opt/ros/jazzy/setup.bash ]]; then
    echo "ROS 2 Jazzy is missing. Run scripts/setup_ros_workspace.sh first." >&2
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

if pgrep -af '[d]efault_server_endpoint' >/dev/null; then
    echo "A ROS-TCP-Endpoint process is already running:" >&2
    pgrep -af '[d]efault_server_endpoint' >&2
    exit 1
fi
if ss -ltnH 'sport = :10000' | grep -q .; then
    echo "TCP port 10000 is already listening; refusing to start a duplicate endpoint." >&2
    ss -ltnp 'sport = :10000' >&2 || true
    exit 1
fi

exec ros2 run ros_tcp_endpoint default_server_endpoint --ros-args -p ROS_IP:=127.0.0.1 -p ROS_TCP_PORT:=10000
