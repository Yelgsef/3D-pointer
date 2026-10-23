#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WS_SETUP="$ROOT_DIR/HumanoidSimulation/ros2_ws/install/setup.bash"
LINEAR="${1:-0.8}"
YAW="${2:-0.0}"
DURATION="${3:-1.0}"

unset CONDA_PREFIX CONDA_DEFAULT_ENV CONDA_PYTHON_EXE PYTHONHOME PYTHONPATH || true
export PATH="/opt/ros/jazzy/bin:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin"
set +u
source /opt/ros/jazzy/setup.bash
if [[ -f "$WS_SETUP" ]]; then
    source "$WS_SETUP"
fi
set -u

echo "Publishing linear.x=$LINEAR, angular.z=$YAW for ${DURATION}s, then stop."
set +e
timeout --signal=INT "$DURATION" ros2 topic pub /cmd_vel geometry_msgs/msg/Twist "{linear: {x: $LINEAR, y: 0.0, z: 0.0}, angular: {x: 0.0, y: 0.0, z: $YAW}}" -r 10
result=$?
set -e
if [[ $result -ne 0 && $result -ne 124 && $result -ne 130 ]]; then
    exit "$result"
fi
ros2 topic pub --once /cmd_vel geometry_msgs/msg/Twist "{linear: {x: 0.0, y: 0.0, z: 0.0}, angular: {x: 0.0, y: 0.0, z: 0.0}}"
