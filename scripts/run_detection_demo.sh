#!/usr/bin/env bash
set -euo pipefail

ENV_NAME="${CONDA_ENV_NAME:-Object_detection_system}"

export TMPDIR="${TMPDIR:-/private/tmp}"
export MPLCONFIGDIR="${MPLCONFIGDIR:-$PWD/.cache/matplotlib}"
export YOLO_CONFIG_DIR="${YOLO_CONFIG_DIR:-$PWD/.ultralytics}"
mkdir -p "$MPLCONFIGDIR" "$YOLO_CONFIG_DIR"

conda run -n "$ENV_NAME" python app/detection_demo.py "$@"
