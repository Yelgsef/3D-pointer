#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VENV_DIR="$ROOT_DIR/.venv"
unset CONDA_PREFIX CONDA_DEFAULT_ENV CONDA_PYTHON_EXE PYTHONHOME PYTHONPATH || true

if [[ ! -f /opt/ros/jazzy/setup.bash ]]; then
    echo "ROS 2 Jazzy is required to expose rclpy and sensor messages." >&2
    exit 1
fi

python3 -m venv --system-site-packages "$VENV_DIR"
set +u
source /opt/ros/jazzy/setup.bash
set -u
export PYTHONNOUSERSITE=1

"$VENV_DIR/bin/python" -m pip install -r "$ROOT_DIR/scripts/perception-requirements.txt"
# MediaPipe's published dependency set would replace ROS-compatible system
# NumPy/OpenCV with pip wheels. The Pose solution only needs the pinned runtime
# dependencies above and the system OpenCV/NumPy, so install its wheel alone.
"$VENV_DIR/bin/python" -m pip install --no-deps "mediapipe==0.10.21"

echo "Perception environment ready: $VENV_DIR"
