#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WS_DIR="$ROOT_DIR/HumanoidSimulation/ros2_ws"
STATE_DIR="$ROOT_DIR/HumanoidSimulation/Logs/start_all"
ENDPOINT_PID_FILE="$STATE_DIR/endpoint.pid"
PERCEPTION_PID_FILE="$STATE_DIR/perception.pid"
TF_RELAY_PID_FILE="$STATE_DIR/tf_static_relay.pid"
UNITY_PID_FILE="$STATE_DIR/unity.pid"
NO_PERCEPTION=false

unset CONDA_PREFIX CONDA_DEFAULT_ENV CONDA_PYTHON_EXE PYTHONHOME PYTHONPATH || true
export PATH="/opt/ros/jazzy/bin:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin"

stop_owned_process() {
    local pid_file="$1"
    local label="$2"
    local expected="$3"
    if [[ ! -f "$pid_file" ]]; then
        return
    fi
    local pid
    pid="$(<"$pid_file")"
    if [[ "$pid" =~ ^[0-9]+$ ]] && kill -0 "$pid" 2>/dev/null \
        && [[ -r "/proc/$pid/cmdline" ]] \
        && tr '\0' ' ' <"/proc/$pid/cmdline" | grep -Fq -- "$expected"; then
        echo "Stopping $label (PID $pid)"
        kill -TERM -- "-$pid" 2>/dev/null || kill -TERM "$pid" 2>/dev/null || true
        for _ in {1..10}; do
            kill -0 "$pid" 2>/dev/null || break
            sleep 1
        done
        if kill -0 "$pid" 2>/dev/null; then
            echo "$label did not stop; leaving it running. Check its log in $STATE_DIR." >&2
        fi
    fi
    rm -f "$pid_file"
}

start_perception_node() {
    mkdir -p "$STATE_DIR"
    if pgrep -af '[h]uman_perception.pose_detector' >/dev/null; then
        echo "Human perception node is already running; leaving it untouched."
    else
        rm -f "$PERCEPTION_PID_FILE"
        setsid "$ROOT_DIR/scripts/start_human_perception.sh" </dev/null >"$STATE_DIR/perception.log" 2>&1 &
        echo "$!" >"$PERCEPTION_PID_FILE"
        echo "Started human perception node (PID $(<"$PERCEPTION_PID_FILE"))."
    fi
}

start_tf_static_relay() {
    mkdir -p "$STATE_DIR"
    if pgrep -af '[s]tatic_tf_relay' >/dev/null; then
        echo "Static TF relay is already running; leaving it untouched."
    else
        rm -f "$TF_RELAY_PID_FILE"
        setsid "$ROOT_DIR/scripts/start_tf_static_relay.sh" </dev/null >"$STATE_DIR/tf_static_relay.log" 2>&1 &
        echo "$!" >"$TF_RELAY_PID_FILE"
        echo "Started static TF relay (PID $(<"$TF_RELAY_PID_FILE"))."
    fi
}

if [[ "${1:-}" == "--stop" ]]; then
    stop_owned_process "$PERCEPTION_PID_FILE" "human perception" "human_perception.pose_detector"
    stop_owned_process "$TF_RELAY_PID_FILE" "static TF relay" "static_tf_relay"
    stop_owned_process "$ENDPOINT_PID_FILE" "ROS TCP endpoint" "default_server_endpoint"
    stop_owned_process "$UNITY_PID_FILE" "Unity Editor" "-projectPath $ROOT_DIR/HumanoidSimulation"
    exit 0
elif [[ "${1:-}" == "--stop-perception" ]]; then
    stop_owned_process "$PERCEPTION_PID_FILE" "human perception" "human_perception.pose_detector"
    exit 0
elif [[ "${1:-}" == "--start-perception" ]]; then
    if [[ ! -f /opt/ros/jazzy/setup.bash || ! -f "$WS_DIR/install/setup.bash" \
        || ! -x "$ROOT_DIR/.venv/bin/python" ]]; then
        echo "Run setup_ros_workspace.sh and setup_perception_venv.sh before enabling perception." >&2
        exit 1
    fi
    set +u
    source /opt/ros/jazzy/setup.bash
    source "$WS_DIR/install/setup.bash"
    set -u
    start_perception_node
    exit 0
elif [[ "${1:-}" == "--no-perception" ]]; then
    NO_PERCEPTION=true
elif [[ $# -gt 0 ]]; then
    echo "Usage: $0 [--no-perception|--start-perception|--stop-perception|--stop]" >&2
    exit 2
fi

if [[ ! -f /opt/ros/jazzy/setup.bash ]]; then
    echo "ROS 2 Jazzy is required at /opt/ros/jazzy." >&2
    exit 1
fi
if [[ ! -f "$WS_DIR/install/setup.bash" ]]; then
    echo "ROS workspace is not built. Run ./scripts/setup_ros_workspace.sh first." >&2
    exit 1
fi
if [[ "$NO_PERCEPTION" != true && ! -x "$ROOT_DIR/.venv/bin/python" ]]; then
    echo "Perception environment is missing. Run ./scripts/setup_perception_venv.sh first." >&2
    exit 1
fi

mkdir -p "$STATE_DIR"
set +u
source /opt/ros/jazzy/setup.bash
source "$WS_DIR/install/setup.bash"
set -u

if [[ ! -f "$ENDPOINT_PID_FILE" ]] || ! kill -0 "$(<"$ENDPOINT_PID_FILE")" 2>/dev/null; then
    rm -f "$ENDPOINT_PID_FILE"
    if ss -ltnH 'sport = :10000' | grep -q .; then
        echo "Port 10000 is already listening; using the existing ROS TCP endpoint."
    else
        setsid "$ROOT_DIR/scripts/start_ros_endpoint.sh" </dev/null >"$STATE_DIR/endpoint.log" 2>&1 &
        echo "$!" >"$ENDPOINT_PID_FILE"
        echo "Started ROS TCP endpoint (PID $(<"$ENDPOINT_PID_FILE"))."
    fi
fi

endpoint_ready=false
for _ in {1..30}; do
    if ss -ltnH 'sport = :10000' | grep -q .; then
        endpoint_ready=true
        break
    fi
    sleep 1
done
if [[ "$endpoint_ready" != true ]]; then
    echo "ROS TCP endpoint did not open port 10000. See $STATE_DIR/endpoint.log." >&2
    exit 1
fi

if ! ros2 pkg executables unity_tf_bridge | grep -q '^unity_tf_bridge static_tf_relay$'; then
    echo "Build unity_tf_bridge with ./scripts/setup_ros_workspace.sh before starting." >&2
    exit 1
fi
start_tf_static_relay

if [[ "$NO_PERCEPTION" != true ]]; then
    start_perception_node
else
    echo "Human perception is disabled for this startup."
fi

unity_editor="${UNITY_EDITOR:-}"
if [[ -z "$unity_editor" ]]; then
    for candidate in "$HOME"/Unity/Hub/Editor/*/Editor/Unity; do
        if [[ -x "$candidate" ]]; then
            unity_editor="$candidate"
        fi
    done
fi

if pgrep -af '[U]nity.*-projectpath .*HumanoidSimulation' >/dev/null; then
    echo "Unity Editor is already open for this project."
elif [[ -n "$unity_editor" && -x "$unity_editor" ]]; then
    setsid "$unity_editor" -projectPath "$ROOT_DIR/HumanoidSimulation" </dev/null >"$STATE_DIR/unity.log" 2>&1 &
    echo "$!" >"$UNITY_PID_FILE"
    echo "Opened the Unity project (PID $(<"$UNITY_PID_FILE")). Enter Play Mode in the Editor."
else
    echo "Unity Editor was not found. Open HumanoidSimulation/ in Unity Hub, then enter Play Mode."
    echo "Set UNITY_EDITOR=/path/to/Editor/Unity to let this script open it next time."
fi

echo "Startup logs and process IDs: $STATE_DIR"
echo "Perception controls: ./scripts/start_all.sh --stop-perception / --start-perception"
echo "Static TF relay log: $STATE_DIR/tf_static_relay.log"
echo "Stop all processes started by this script with: ./scripts/start_all.sh --stop"
