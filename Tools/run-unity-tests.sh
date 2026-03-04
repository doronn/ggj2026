#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
UNITY_BIN="${UNITY_BIN:-/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity}"
TEST_PLATFORM="editmode"
TEST_FILTER=""
RESULTS_DIR="${PROJECT_ROOT}/Logs/TestResults"
TIMESTAMP="$(date +"%Y%m%d-%H%M%S")"

usage() {
  cat <<EOF
Usage: $(basename "$0") [--platform editmode|playmode] [--filter TEST_NAME]

Runs Unity Test Framework suites in batch mode and writes:
  ${RESULTS_DIR}/<platform>-results.xml
  ${RESULTS_DIR}/<platform>-batch.log

Notes:
  - Do not use -quit; Unity Test Framework command-line runs are skipped when -quit is present.
  - The Unity editor must be closed for this project before running.
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --platform)
      TEST_PLATFORM="$2"
      shift 2
      ;;
    --filter)
      TEST_FILTER="$2"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage >&2
      exit 64
      ;;
  esac
done

if [[ "${TEST_PLATFORM}" != "editmode" && "${TEST_PLATFORM}" != "playmode" ]]; then
  echo "Unsupported platform: ${TEST_PLATFORM}" >&2
  exit 64
fi

if [[ ! -x "${UNITY_BIN}" ]]; then
  echo "Unity binary not found or not executable: ${UNITY_BIN}" >&2
  exit 66
fi

mkdir -p "${RESULTS_DIR}"

LOCKFILE="${PROJECT_ROOT}/Temp/UnityLockfile"
if python3 - "${PROJECT_ROOT}" "${UNITY_BIN}" <<'PY'
import subprocess
import sys

project_root = sys.argv[1]
unity_bin = sys.argv[2]
output = subprocess.check_output(["ps", "-ax", "-o", "pid=,command="], text=True)
for raw_line in output.splitlines():
    line = raw_line.strip()
    if not line:
        continue
    parts = line.split(None, 1)
    command = parts[1] if len(parts) == 2 else ""
    if not command.startswith(unity_bin) or project_root not in command:
        continue
    sys.exit(0)
sys.exit(1)
PY
then
  echo "Unity is already running for this project. Close the editor before starting a batch test run." >&2
  exit 69
fi

if [[ -f "${LOCKFILE}" ]]; then
  rm -f "${LOCKFILE}"
fi

RESULTS_FILE="${RESULTS_DIR}/${TEST_PLATFORM}-results.xml"
LOG_FILE="${RESULTS_DIR}/${TEST_PLATFORM}-batch.log"
ARCHIVE_RESULTS_FILE="${RESULTS_DIR}/${TEST_PLATFORM}-results-${TIMESTAMP}.xml"
ARCHIVE_LOG_FILE="${RESULTS_DIR}/${TEST_PLATFORM}-batch-${TIMESTAMP}.log"

CMD=(
  "${UNITY_BIN}"
  -batchmode
  -nographics
  -skipMissingProjectID
  -skipMissingUPID
  -projectPath "${PROJECT_ROOT}"
  -runTests
  -testPlatform "${TEST_PLATFORM}"
  -testResults "${RESULTS_FILE}"
  -logFile "${LOG_FILE}"
)

if [[ -n "${TEST_FILTER}" ]]; then
  CMD+=(-testFilter "${TEST_FILTER}")
fi

echo "Running ${TEST_PLATFORM} tests..."
"${CMD[@]}"
UNITY_EXIT=$?

if [[ -f "${RESULTS_FILE}" ]]; then
  cp "${RESULTS_FILE}" "${ARCHIVE_RESULTS_FILE}"
fi

if [[ -f "${LOG_FILE}" ]]; then
  cp "${LOG_FILE}" "${ARCHIVE_LOG_FILE}"
fi

if [[ -f "${RESULTS_FILE}" ]]; then
  python3 - "${RESULTS_FILE}" <<'PY'
import sys
import xml.etree.ElementTree as ET

path = sys.argv[1]
root = ET.parse(path).getroot()
platform = path.split("/")[-1].replace("-results.xml", "")
print(f"{platform} results: total={root.attrib.get('total')} passed={root.attrib.get('passed')} failed={root.attrib.get('failed')} skipped={root.attrib.get('skipped')} result={root.attrib.get('result')}")
for case in root.iter("test-case"):
    if case.attrib.get("result") != "Failed":
        continue
    print(f"FAILED {case.attrib.get('fullname')}")
    failure = case.find("failure")
    if failure is not None:
        message = (failure.findtext("message") or "").strip()
        if message:
            print(message)
PY
else
  echo "Unity did not produce a results file: ${RESULTS_FILE}" >&2
fi

echo "Results: ${RESULTS_FILE}"
echo "Log: ${LOG_FILE}"
exit "${UNITY_EXIT}"
