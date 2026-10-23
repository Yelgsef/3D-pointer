#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
unset CONDA_PREFIX CONDA_DEFAULT_ENV CONDA_PYTHON_EXE PYTHONHOME PYTHONPATH || true
export PATH="/opt/ros/jazzy/bin:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin"
set +u
source /opt/ros/jazzy/setup.bash
if [[ -f "$ROOT_DIR/HumanoidSimulation/ros2_ws/install/setup.bash" ]]; then
    source "$ROOT_DIR/HumanoidSimulation/ros2_ws/install/setup.bash"
fi
set -u

ros2 topic list -t
for topic in /cmd_vel /robot/pose; do
    if ros2 topic list | grep -Fxq "$topic"; then
        echo
        ros2 topic info "$topic"
    else
        echo "Topic $topic is not currently registered." >&2
    fi
done
