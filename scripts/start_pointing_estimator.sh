#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WS_DIR="$ROOT_DIR/HumanoidSimulation/ros2_ws"

unset CONDA_PREFIX CONDA_DEFAULT_ENV CONDA_PYTHON_EXE PYTHONHOME PYTHONPATH || true
set +u
source /opt/ros/jazzy/setup.bash
source "$WS_DIR/install/setup.bash"
set -u
export PYTHONNOUSERSITE=1

exec "$ROOT_DIR/.venv/bin/python" -m human_perception.pointing_estimator "$@"
