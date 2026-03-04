#!/usr/bin/env python3
from __future__ import annotations

import json
import os
import subprocess
import sys
import tempfile
import time
import traceback
import uuid
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Callable


PROJECT_ROOT = Path(__file__).resolve().parents[2]
UNITY_BIN = Path(
    os.environ.get(
        "UNITY_BIN",
        "/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity",
    )
)


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def parse_timestamp(value: str) -> datetime:
    normalized = value.strip()
    if normalized.endswith("Z"):
        normalized = normalized[:-1] + "+00:00"

    if "T" in normalized:
        if "+" in normalized[10:]:
            main, offset = normalized.rsplit("+", 1)
            sign = "+"
        elif "-" in normalized[10:]:
            main, offset = normalized.rsplit("-", 1)
            sign = "-"
        else:
            main, offset = normalized, ""
            sign = ""

        if "." in main:
            prefix, frac = main.split(".", 1)
            if len(frac) > 6:
                main = f"{prefix}.{frac[:6]}"

        normalized = main if not offset else f"{main}{sign}{offset}"

    return datetime.fromisoformat(normalized)


def parse_json_file(path: Path) -> dict[str, Any] | None:
    if not path.exists():
        return None
    return json.loads(path.read_text(encoding="utf-8"))


def process_exists(process_id: int) -> bool:
    if process_id <= 0:
        return False

    try:
        os.kill(process_id, 0)
    except PermissionError:
        return True
    except OSError:
        return False

    return True


def parse_test_results(path: Path) -> dict[str, Any]:
    if not path.exists():
        return {"exists": False, "path": str(path)}

    root = ET.parse(path).getroot()
    failed: list[dict[str, str]] = []
    for case in root.iter("test-case"):
        if case.attrib.get("result") != "Failed":
            continue
        failure = case.find("failure")
        failed.append(
            {
                "fullname": case.attrib.get("fullname", ""),
                "message": (failure.findtext("message") if failure is not None else "") or "",
            }
        )

    return {
        "exists": True,
        "path": str(path),
        "total": int(root.attrib.get("total", "0")),
        "passed": int(root.attrib.get("passed", "0")),
        "failed": int(root.attrib.get("failed", "0")),
        "skipped": int(root.attrib.get("skipped", "0")),
        "result": root.attrib.get("result", "Unknown"),
        "failures": failed,
    }


def parse_compile_report(path: Path) -> dict[str, Any]:
    if not path.exists():
        return {"exists": False, "path": str(path)}

    lines = path.read_text(encoding="utf-8").splitlines()
    summary: dict[str, Any] = {"exists": True, "path": str(path), "errors": None, "warnings": None, "finished_at": None}
    for line in lines:
        if "[SUMMARY] FinishedAt=" in line:
            summary["finished_at"] = line.split("FinishedAt=", 1)[1]
        elif "[SUMMARY] errors=" in line:
            payload = line.split("[SUMMARY] ", 1)[1]
            for token in payload.split():
                if token.startswith("errors="):
                    summary["errors"] = int(token.split("=", 1)[1])
                elif token.startswith("warnings="):
                    summary["warnings"] = int(token.split("=", 1)[1])

    summary["tail"] = lines[-20:]
    return summary


def read_text_file(path: Path, *, max_chars: int = 16000) -> str:
    if not path.exists():
        return ""
    text = path.read_text(encoding="utf-8", errors="replace")
    return text[-max_chars:]


@dataclass
class CommandResult:
    ok: bool
    message: str
    data: dict[str, Any]


class UnityProjectController:
    def __init__(self, project_root: Path) -> None:
        self.project_root = project_root
        self.logs_dir = project_root / "Logs"
        self.diagnostics_dir = self.logs_dir / "Diagnostics"
        self.telemetry_dir = self.logs_dir / "Telemetry"
        self.persistent_logs_dir = self.logs_dir / "PersistentWorld"
        self.test_results_dir = self.logs_dir / "TestResults"
        self.bridge_dir = self.logs_dir / "McpBridge"
        self.bridge_requests_dir = self.bridge_dir / "requests"
        self.bridge_responses_dir = self.bridge_dir / "responses"
        self.bridge_status_path = self.bridge_dir / "status.json"
        self.editmode_results = self.test_results_dir / "editmode-results.xml"
        self.playmode_results = self.test_results_dir / "playmode-results.xml"
        self.compile_report = self.diagnostics_dir / "compile-latest.log"
        self.runtime_log = self.persistent_logs_dir / "runtime-latest.log"
        self.telemetry_latest_dir = self.telemetry_dir / "session-latest"

    def get_bridge_status(self) -> dict[str, Any]:
        status = parse_json_file(self.bridge_status_path)
        if not status:
            return {"available": False, "reason": "Bridge status file not found.", "path": str(self.bridge_status_path)}

        heartbeat = status.get("lastHeartbeatUtc")
        available = bool(status.get("isAvailable"))
        fresh = False
        process_alive = process_exists(int(status.get("editorProcessId", 0) or 0))
        if heartbeat:
            try:
                fresh = (datetime.now(timezone.utc) - parse_timestamp(heartbeat)).total_seconds() <= 5.0
            except ValueError:
                fresh = False

        return {
            "available": available and status.get("projectPath") == str(self.project_root),
            "fresh": fresh,
            "processAlive": process_alive,
            "stale": not fresh,
            "status": status,
            "path": str(self.bridge_status_path),
        }

    def send_bridge_command(self, command: str, payload: dict[str, Any] | None = None, *, timeout_seconds: float = 30.0) -> CommandResult:
        bridge = self.get_bridge_status()
        if not bridge.get("available"):
            return CommandResult(False, "Unity editor bridge is not available for this project.", {"bridge": bridge})

        self.bridge_requests_dir.mkdir(parents=True, exist_ok=True)
        self.bridge_responses_dir.mkdir(parents=True, exist_ok=True)

        request_id = uuid.uuid4().hex
        request = {
            "requestId": request_id,
            "command": command,
            "payloadJson": json.dumps(payload or {}, ensure_ascii=True),
            "createdAtUtc": utc_now(),
        }

        request_path = self.bridge_requests_dir / f"{request_id}.json"
        response_path = self.bridge_responses_dir / f"{request_id}.json"

        with tempfile.NamedTemporaryFile("w", delete=False, dir=self.bridge_requests_dir, encoding="utf-8") as temp_file:
            json.dump(request, temp_file, indent=2)
            temp_name = temp_file.name
        os.replace(temp_name, request_path)

        deadline = time.time() + timeout_seconds
        while time.time() < deadline:
            if response_path.exists():
                response = json.loads(response_path.read_text(encoding="utf-8"))
                response_path.unlink(missing_ok=True)
                if response.get("success"):
                    payload_json = response.get("payloadJson") or "{}"
                    try:
                        data = json.loads(payload_json)
                    except json.JSONDecodeError:
                        data = {"rawPayloadJson": payload_json}
                    return CommandResult(True, response.get("message", ""), data)

                return CommandResult(False, response.get("error", "Bridge command failed."), {"response": response})

            time.sleep(0.1)

        return CommandResult(False, f"Timed out waiting for bridge response to '{command}'.", {"requestId": request_id})

    def project_status(self) -> CommandResult:
        telemetry_session = parse_json_file(self.telemetry_latest_dir / "session.json") or {}
        return CommandResult(
            True,
            "Collected project status.",
            {
                "projectRoot": str(self.project_root),
                "unityBinary": str(UNITY_BIN),
                "bridge": self.get_bridge_status(),
                "compile": parse_compile_report(self.compile_report),
                "runtimeLogExists": self.runtime_log.exists(),
                "editmodeTests": parse_test_results(self.editmode_results),
                "playmodeTests": parse_test_results(self.playmode_results),
                "telemetrySession": telemetry_session,
                "paths": {
                    "compileReport": str(self.compile_report),
                    "runtimeLog": str(self.runtime_log),
                    "telemetryLatest": str(self.telemetry_latest_dir),
                    "bridgeStatus": str(self.bridge_status_path),
                },
            },
        )

    def run_compile(self, mode: str = "auto") -> CommandResult:
        if mode not in {"auto", "bridge", "batch"}:
            return CommandResult(False, f"Unsupported compile mode: {mode}", {})

        if mode in {"auto", "bridge"}:
            bridge = self.get_bridge_status()
            if bridge.get("available"):
                result = self.send_bridge_command("refresh_recompile")
                if mode == "bridge" or result.ok:
                    return result
            elif mode == "bridge":
                return CommandResult(False, "Bridge compile requested, but the live editor bridge is unavailable.", {"bridge": bridge})

        log_path = self.diagnostics_dir / "mcp-compile-batch.log"
        command = [
            str(UNITY_BIN),
            "-batchmode",
            "-nographics",
            "-skipMissingProjectID",
            "-skipMissingUPID",
            "-projectPath",
            str(self.project_root),
            "-executeMethod",
            "Ggj2026.Game.Authoring.Editor.ProjectCompilationTools.RefreshAssetsAndRecompileFromCommandLine",
            "-logFile",
            str(log_path),
        ]
        return self._run_command(command, success_message="Batch compile finished.", extra={"logPath": str(log_path), "compile": parse_compile_report(self.compile_report)})

    def run_tests(self, platform: str, test_filter: str | None = None) -> CommandResult:
        if platform not in {"editmode", "playmode"}:
            return CommandResult(False, f"Unsupported test platform: {platform}", {})

        bridge = self.get_bridge_status()
        if bridge.get("available"):
            result = self.send_bridge_command(
                "run_tests",
                {"platform": platform, "filter": test_filter or ""},
                timeout_seconds=60.0 * 60.0,
            )
            data = {
                "mode": "bridge",
                "bridge": bridge,
            }
            if result.data:
                data["tests"] = result.data
                if "resultsPath" in result.data:
                    data["resultsPath"] = result.data["resultsPath"]
            return CommandResult(result.ok, result.message, data)

        command = [str(self.project_root / "Tools" / "run-unity-tests.sh"), "--platform", platform]
        if test_filter:
            command.extend(["--filter", test_filter])
        results_path = self.editmode_results if platform == "editmode" else self.playmode_results
        log_path = self.test_results_dir / f"{platform}-batch.log"
        result = self._run_command(
            command,
            success_message=f"{platform} tests finished.",
            extra={"mode": "batch", "resultsPath": str(results_path), "logPath": str(log_path)},
        )
        result.data["tests"] = parse_test_results(results_path)
        return result

    def cancel_tests(self) -> CommandResult:
        bridge = self.get_bridge_status()
        if not bridge.get("available"):
            return CommandResult(False, "Live Unity editor bridge is unavailable; there is no live test run to cancel.", {"bridge": bridge})
        return self.send_bridge_command("cancel_tests", {})

    def apply_ui_plan(self, dry_run: bool = False, mode: str = "auto") -> CommandResult:
        if mode not in {"auto", "bridge", "batch"}:
            return CommandResult(False, f"Unsupported UI apply mode: {mode}", {})

        if mode in {"auto", "bridge"}:
            bridge = self.get_bridge_status()
            if bridge.get("available"):
                result = self.send_bridge_command("apply_ui_plan", {"dryRun": dry_run})
                if mode == "bridge" or result.ok:
                    return result
            elif mode == "bridge":
                return CommandResult(False, "Bridge UI-plan apply requested, but the live editor bridge is unavailable.", {"bridge": bridge})

        log_path = self.diagnostics_dir / "mcp-ui-plan-batch.log"
        command = [
            str(UNITY_BIN),
            "-batchmode",
            "-nographics",
            "-skipMissingProjectID",
            "-skipMissingUPID",
            "-projectPath",
            str(self.project_root),
            "-executeMethod",
            "Ggj2026.Game.Authoring.Editor.Mcp.UnityEditorMcpCommandLine.ApplyUiPlanFromCommandLine",
            "-logFile",
            str(log_path),
        ]
        if dry_run:
            command.append("-mcpDryRun")
        result = self._run_command(command, success_message="UI plan operation finished.", extra={"logPath": str(log_path), "reportPath": str(self.diagnostics_dir / "ui-install-latest.json")})
        report = parse_json_file(self.diagnostics_dir / "ui-install-latest.json")
        if report is not None:
            result.data["report"] = report
        return result

    def open_scene(self, scene_path: str) -> CommandResult:
        return self.send_bridge_command("open_scene", {"scenePath": scene_path})

    def play_mode(self, action: str, scene_path: str | None = None) -> CommandResult:
        if action == "enter":
            return self.send_bridge_command("enter_play_mode", {"scenePath": scene_path or ""})
        if action == "exit":
            return self.send_bridge_command("exit_play_mode", {})
        return CommandResult(False, f"Unsupported play mode action: {action}", {})

    def list_manual_tests(self) -> CommandResult:
        return self.send_bridge_command("list_manual_tests", {})

    def manual_test_status(self) -> CommandResult:
        return self.send_bridge_command("manual_test_status", {})

    def start_manual_test(self, scenario_id: str) -> CommandResult:
        return self.send_bridge_command("start_manual_test", {"scenarioId": scenario_id})

    def record_manual_test_step(self, result: str, note: str = "") -> CommandResult:
        return self.send_bridge_command("record_manual_test_step", {"result": result, "note": note})

    def finish_manual_test(self) -> CommandResult:
        return self.send_bridge_command("finish_manual_test", {})

    def game_hud_state(self) -> CommandResult:
        return self.send_bridge_command("game_hud_state", {})

    def game_world_summary(self) -> CommandResult:
        return self.send_bridge_command("game_world_summary", {})

    def game_selection(self) -> CommandResult:
        return self.send_bridge_command("game_selection", {})

    def game_hover(self) -> CommandResult:
        return self.send_bridge_command("game_hover", {})

    def game_list_entities(self) -> CommandResult:
        return self.send_bridge_command("game_list_entities", {})

    def game_list_actions(self) -> CommandResult:
        return self.send_bridge_command("game_list_actions", {})

    def game_action_schema(self, action_id: str) -> CommandResult:
        return self.send_bridge_command("game_action_schema", {"actionId": action_id})

    def game_blockers(self) -> CommandResult:
        return self.send_bridge_command("game_blockers", {})

    def game_recent_events(self) -> CommandResult:
        return self.send_bridge_command("game_recent_events", {})

    def game_execute_action(self, payload: dict[str, Any]) -> CommandResult:
        return self.send_bridge_command("game_execute_action", payload)

    def tail_log(self, kind: str, lines: int = 80) -> CommandResult:
        path_map = {
            "compile": self.compile_report,
            "runtime": self.runtime_log,
            "editmode": self.test_results_dir / "editmode-batch.log",
            "playmode": self.test_results_dir / "playmode-batch.log",
            "telemetry-actions": self.telemetry_latest_dir / "actions.jsonl",
            "telemetry-ui-actions": self.telemetry_latest_dir / "ui-actions.jsonl",
        }
        path = path_map.get(kind)
        if path is None:
            return CommandResult(False, f"Unsupported log kind: {kind}", {"supportedKinds": sorted(path_map.keys())})

        if not path.exists():
            return CommandResult(False, f"Log does not exist: {path}", {"path": str(path)})

        data = path.read_text(encoding="utf-8", errors="replace").splitlines()
        return CommandResult(True, f"Read latest {kind} log.", {"path": str(path), "lines": data[-max(1, lines):]})

    def list_resources(self) -> list[dict[str, Any]]:
        return [
            self._file_resource("unity://project/status", "Project Status", "Generated JSON project status.", "application/json"),
            self._file_resource("unity://compile/latest", "Latest Compile Report", "Repo-local compile report.", "text/plain"),
            self._file_resource("unity://runtime-log/latest", "Latest Runtime Log", "Repo-local runtime log.", "text/plain"),
            self._file_resource("unity://tests/editmode/latest", "Latest EditMode Results", "Latest batch edit-mode test XML.", "application/xml"),
            self._file_resource("unity://tests/playmode/latest", "Latest PlayMode Results", "Latest batch play-mode test XML.", "application/xml"),
            self._file_resource("unity://telemetry/session-latest/session", "Latest Telemetry Session", "Latest structured telemetry session metadata.", "application/json"),
            self._file_resource("unity://telemetry/session-latest/actions", "Latest Telemetry Actions", "Latest session action log.", "application/jsonl"),
            self._file_resource("unity://telemetry/session-latest/summary", "Latest Telemetry Summary", "Latest telemetry markdown summary.", "text/markdown"),
            self._file_resource("unity://mcp/bridge/status", "Editor Bridge Status", "Live editor bridge heartbeat and state.", "application/json"),
        ]

    def read_resource(self, uri: str) -> dict[str, Any]:
        if uri == "unity://project/status":
            status = self.project_status().data
            return {"mimeType": "application/json", "text": json.dumps(status, indent=2)}
        if uri == "unity://compile/latest":
            return self._resource_from_path(self.compile_report, "text/plain")
        if uri == "unity://runtime-log/latest":
            return self._resource_from_path(self.runtime_log, "text/plain")
        if uri == "unity://tests/editmode/latest":
            return self._resource_from_path(self.editmode_results, "application/xml")
        if uri == "unity://tests/playmode/latest":
            return self._resource_from_path(self.playmode_results, "application/xml")
        if uri == "unity://telemetry/session-latest/session":
            return self._resource_from_path(self.telemetry_latest_dir / "session.json", "application/json")
        if uri == "unity://telemetry/session-latest/actions":
            return self._resource_from_path(self.telemetry_latest_dir / "actions.jsonl", "application/jsonl")
        if uri == "unity://telemetry/session-latest/summary":
            return self._resource_from_path(self.telemetry_latest_dir / "summary.md", "text/markdown")
        if uri == "unity://mcp/bridge/status":
            return self._resource_from_path(self.bridge_status_path, "application/json")
        raise KeyError(uri)

    def _resource_from_path(self, path: Path, mime_type: str) -> dict[str, Any]:
        return {"mimeType": mime_type, "text": read_text_file(path), "path": str(path)}

    def _file_resource(self, uri: str, name: str, description: str, mime_type: str) -> dict[str, Any]:
        return {"uri": uri, "name": name, "description": description, "mimeType": mime_type}

    def _run_command(self, command: list[str], *, success_message: str, extra: dict[str, Any] | None = None) -> CommandResult:
        completed = subprocess.run(
            command,
            cwd=self.project_root,
            text=True,
            capture_output=True,
        )
        ok = completed.returncode == 0
        data = {
            "command": command,
            "returnCode": completed.returncode,
            "stdout": completed.stdout,
            "stderr": completed.stderr,
        }
        if extra:
            data.update(extra)
        return CommandResult(ok, success_message if ok else "Command failed.", data)


class JsonRpcMcpServer:
    def __init__(self, controller: UnityProjectController) -> None:
        self.controller = controller
        self.tool_handlers: dict[str, tuple[dict[str, Any], Callable[[dict[str, Any]], CommandResult]]] = {
            "unity_project_status": (
                {
                    "type": "object",
                    "properties": {},
                    "additionalProperties": False,
                },
                lambda args: self.controller.project_status(),
            ),
            "unity_compile": (
                {
                    "type": "object",
                    "properties": {
                        "mode": {"type": "string", "enum": ["auto", "bridge", "batch"]},
                    },
                    "additionalProperties": False,
                },
                lambda args: self.controller.run_compile(args.get("mode", "auto")),
            ),
            "unity_run_tests": (
                {
                    "type": "object",
                    "properties": {
                        "platform": {"type": "string", "enum": ["editmode", "playmode"]},
                        "filter": {"type": "string"},
                    },
                    "required": ["platform"],
                    "additionalProperties": False,
                },
                lambda args: self.controller.run_tests(args["platform"], args.get("filter")),
            ),
            "unity_cancel_tests": (
                {
                    "type": "object",
                    "properties": {},
                    "additionalProperties": False,
                },
                lambda args: self.controller.cancel_tests(),
            ),
            "unity_apply_ui_plan": (
                {
                    "type": "object",
                    "properties": {
                        "dryRun": {"type": "boolean"},
                        "mode": {"type": "string", "enum": ["auto", "bridge", "batch"]},
                    },
                    "additionalProperties": False,
                },
                lambda args: self.controller.apply_ui_plan(bool(args.get("dryRun", False)), args.get("mode", "auto")),
            ),
            "unity_open_scene": (
                {
                    "type": "object",
                    "properties": {"scenePath": {"type": "string"}},
                    "required": ["scenePath"],
                    "additionalProperties": False,
                },
                lambda args: self.controller.open_scene(args["scenePath"]),
            ),
            "unity_play_mode": (
                {
                    "type": "object",
                    "properties": {
                        "action": {"type": "string", "enum": ["enter", "exit"]},
                        "scenePath": {"type": "string"},
                    },
                    "required": ["action"],
                    "additionalProperties": False,
                },
                lambda args: self.controller.play_mode(args["action"], args.get("scenePath")),
            ),
            "unity_list_manual_tests": (
                {
                    "type": "object",
                    "properties": {},
                    "additionalProperties": False,
                },
                lambda args: self.controller.list_manual_tests(),
            ),
            "unity_manual_test_status": (
                {
                    "type": "object",
                    "properties": {},
                    "additionalProperties": False,
                },
                lambda args: self.controller.manual_test_status(),
            ),
            "unity_start_manual_test": (
                {
                    "type": "object",
                    "properties": {
                        "scenarioId": {"type": "string"},
                    },
                    "required": ["scenarioId"],
                    "additionalProperties": False,
                },
                lambda args: self.controller.start_manual_test(args["scenarioId"]),
            ),
            "unity_record_manual_test_step": (
                {
                    "type": "object",
                    "properties": {
                        "result": {"type": "string", "enum": ["Pass", "Fail", "Blocked", "Skipped"]},
                        "note": {"type": "string"},
                    },
                    "required": ["result"],
                    "additionalProperties": False,
                },
                lambda args: self.controller.record_manual_test_step(args["result"], args.get("note", "")),
            ),
            "unity_finish_manual_test": (
                {
                    "type": "object",
                    "properties": {},
                    "additionalProperties": False,
                },
                lambda args: self.controller.finish_manual_test(),
            ),
            "unity_game_hud_state": (
                {
                    "type": "object",
                    "properties": {},
                    "additionalProperties": False,
                },
                lambda args: self.controller.game_hud_state(),
            ),
            "unity_game_world_summary": (
                {
                    "type": "object",
                    "properties": {},
                    "additionalProperties": False,
                },
                lambda args: self.controller.game_world_summary(),
            ),
            "unity_game_selection": (
                {
                    "type": "object",
                    "properties": {},
                    "additionalProperties": False,
                },
                lambda args: self.controller.game_selection(),
            ),
            "unity_game_hover": (
                {
                    "type": "object",
                    "properties": {},
                    "additionalProperties": False,
                },
                lambda args: self.controller.game_hover(),
            ),
            "unity_game_list_entities": (
                {
                    "type": "object",
                    "properties": {},
                    "additionalProperties": False,
                },
                lambda args: self.controller.game_list_entities(),
            ),
            "unity_game_list_actions": (
                {
                    "type": "object",
                    "properties": {},
                    "additionalProperties": False,
                },
                lambda args: self.controller.game_list_actions(),
            ),
            "unity_game_action_schema": (
                {
                    "type": "object",
                    "properties": {
                        "actionId": {"type": "string"},
                    },
                    "required": ["actionId"],
                    "additionalProperties": False,
                },
                lambda args: self.controller.game_action_schema(args["actionId"]),
            ),
            "unity_game_execute_action": (
                {
                    "type": "object",
                    "properties": {
                        "actionId": {"type": "string"},
                        "targetCell": self._cell_schema(),
                        "targetEdge": self._edge_schema(),
                        "targetEntityId": {"type": "string"},
                        "targetConstructionId": {"type": "string"},
                        "hasTargetCell": {"type": "boolean"},
                        "hasTargetEdge": {"type": "boolean"},
                        "floorPurposeKind": {"type": "string"},
                        "edgeFeatureKind": {"type": "string"},
                        "moduleKind": {"type": "string"},
                        "landmarkKind": {"type": "string"},
                        "overlayMode": {"type": "string"},
                        "inspectorTab": {"type": "string"},
                        "tickCount": {"type": "integer"},
                        "speedMultiplier": {"type": "number"},
                        "desiredRunningState": {"type": "boolean"},
                        "note": {"type": "string"},
                    },
                    "required": ["actionId"],
                    "additionalProperties": False,
                },
                lambda args: self.controller.game_execute_action(self._build_action_payload(**args)),
            ),
            "unity_game_blockers": (
                {
                    "type": "object",
                    "properties": {},
                    "additionalProperties": False,
                },
                lambda args: self.controller.game_blockers(),
            ),
            "unity_game_recent_events": (
                {
                    "type": "object",
                    "properties": {},
                    "additionalProperties": False,
                },
                lambda args: self.controller.game_recent_events(),
            ),
            "unity_game_select_cell": (
                {
                    "type": "object",
                    "properties": {"targetCell": self._cell_schema()},
                    "required": ["targetCell"],
                    "additionalProperties": False,
                },
                lambda args: self._execute_action("SelectCell", targetCell=args["targetCell"], hasTargetCell=True, panelId="McpGameplay", elementId="SelectCell"),
            ),
            "unity_game_select_edge": (
                {
                    "type": "object",
                    "properties": {"targetEdge": self._edge_schema()},
                    "required": ["targetEdge"],
                    "additionalProperties": False,
                },
                lambda args: self._execute_action("SelectEdge", targetEdge=args["targetEdge"], hasTargetEdge=True, panelId="McpGameplay", elementId="SelectEdge"),
            ),
            "unity_game_select_entity": (
                {
                    "type": "object",
                    "properties": {"targetEntityId": {"type": "string"}},
                    "required": ["targetEntityId"],
                    "additionalProperties": False,
                },
                lambda args: self._execute_action("SelectEntity", targetEntityId=args["targetEntityId"], panelId="McpGameplay", elementId="SelectEntity"),
            ),
            "unity_game_set_tool": (
                {
                    "type": "object",
                    "properties": {
                        "kind": {"type": "string", "enum": ["floor", "edge", "module", "landmark", "clear-cell", "clear-edge"]},
                        "value": {"type": "string"},
                    },
                    "required": ["kind"],
                    "additionalProperties": False,
                },
                lambda args: self._set_tool(args["kind"], args.get("value")),
            ),
            "unity_game_clear_tool": (
                {
                    "type": "object",
                    "properties": {},
                    "additionalProperties": False,
                },
                lambda args: self._execute_action("ClearTool", panelId="McpGameplay", elementId="ClearTool"),
            ),
            "unity_game_apply_brush": (
                {
                    "type": "object",
                    "properties": {
                        "actionId": {"type": "string", "enum": ["PaintFloor", "ClearCell"]},
                        "targetCell": self._cell_schema(),
                        "floorPurposeKind": {"type": "string"},
                    },
                    "required": ["actionId", "targetCell"],
                    "additionalProperties": False,
                },
                lambda args: self._execute_action(
                    args["actionId"],
                    targetCell=args["targetCell"],
                    hasTargetCell=True,
                    floorPurposeKind=args.get("floorPurposeKind", ""),
                    panelId="McpGameplay",
                    elementId="ApplyBrush"),
            ),
            "unity_game_apply_edge_stroke": (
                {
                    "type": "object",
                    "properties": {
                        "actionId": {"type": "string", "enum": ["BuildEdge", "ClearEdge"]},
                        "targetEdge": self._edge_schema(),
                        "edgeFeatureKind": {"type": "string"},
                    },
                    "required": ["actionId", "targetEdge"],
                    "additionalProperties": False,
                },
                lambda args: self._execute_action(
                    args["actionId"],
                    targetEdge=args["targetEdge"],
                    hasTargetEdge=True,
                    edgeFeatureKind=args.get("edgeFeatureKind", ""),
                    panelId="McpGameplay",
                    elementId="ApplyEdgeStroke"),
            ),
            "unity_game_place_module": (
                {
                    "type": "object",
                    "properties": {
                        "targetCell": self._cell_schema(),
                        "moduleKind": {"type": "string"},
                    },
                    "required": ["targetCell", "moduleKind"],
                    "additionalProperties": False,
                },
                lambda args: self._execute_action("PlaceModule", targetCell=args["targetCell"], hasTargetCell=True, moduleKind=args["moduleKind"], panelId="McpGameplay", elementId="PlaceModule"),
            ),
            "unity_game_place_landmark": (
                {
                    "type": "object",
                    "properties": {
                        "targetCell": self._cell_schema(),
                        "landmarkKind": {"type": "string"},
                    },
                    "required": ["targetCell", "landmarkKind"],
                    "additionalProperties": False,
                },
                lambda args: self._execute_action("PlaceLandmark", targetCell=args["targetCell"], hasTargetCell=True, landmarkKind=args["landmarkKind"], panelId="McpGameplay", elementId="PlaceLandmark"),
            ),
            "unity_game_clear_cell": (
                {
                    "type": "object",
                    "properties": {"targetCell": self._cell_schema()},
                    "required": ["targetCell"],
                    "additionalProperties": False,
                },
                lambda args: self._execute_action("ClearCell", targetCell=args["targetCell"], hasTargetCell=True, panelId="McpGameplay", elementId="ClearCell"),
            ),
            "unity_game_clear_edge": (
                {
                    "type": "object",
                    "properties": {"targetEdge": self._edge_schema()},
                    "required": ["targetEdge"],
                    "additionalProperties": False,
                },
                lambda args: self._execute_action("ClearEdge", targetEdge=args["targetEdge"], hasTargetEdge=True, panelId="McpGameplay", elementId="ClearEdge"),
            ),
            "unity_game_simulation_control": (
                {
                    "type": "object",
                    "properties": {
                        "action": {"type": "string", "enum": ["play", "pause", "tick", "speed", "running"]},
                        "tickCount": {"type": "integer", "minimum": 1},
                        "speedMultiplier": {"type": "number"},
                        "running": {"type": "boolean"},
                    },
                    "required": ["action"],
                    "additionalProperties": False,
                },
                lambda args: self._simulation_control(args["action"], args.get("tickCount"), args.get("speedMultiplier"), args.get("running")),
            ),
            "unity_game_save_world": (
                {
                    "type": "object",
                    "properties": {},
                    "additionalProperties": False,
                },
                lambda args: self._execute_action("SaveWorld", panelId="McpGameplay", elementId="SaveWorld"),
            ),
            "unity_game_reset_world": (
                {
                    "type": "object",
                    "properties": {},
                    "additionalProperties": False,
                },
                lambda args: self._execute_action("ResetWorld", panelId="McpGameplay", elementId="ResetWorld"),
            ),
            "unity_game_context_explain": (
                {
                    "type": "object",
                    "properties": {},
                    "additionalProperties": False,
                },
                lambda args: self._execute_action("ExplainContext", panelId="McpGameplay", elementId="ExplainContext"),
            ),
            "unity_game_force_inspector": (
                {
                    "type": "object",
                    "properties": {
                        "inspectorTab": {"type": "string", "enum": ["Context", "Tasks", "Entities", "World"]},
                    },
                    "required": ["inspectorTab"],
                    "additionalProperties": False,
                },
                lambda args: self._execute_action("SetInspectorTab", inspectorTab=args["inspectorTab"], panelId="McpGameplay", elementId="SetInspectorTab"),
            ),
            "unity_tail_log": (
                {
                    "type": "object",
                    "properties": {
                        "kind": {
                            "type": "string",
                            "enum": [
                                "compile",
                                "runtime",
                                "editmode",
                                "playmode",
                                "telemetry-actions",
                                "telemetry-ui-actions",
                            ],
                        },
                        "lines": {"type": "integer", "minimum": 1, "maximum": 400},
                    },
                    "required": ["kind"],
                    "additionalProperties": False,
                },
                lambda args: self.controller.tail_log(args["kind"], int(args.get("lines", 80))),
            ),
        }

    @staticmethod
    def _cell_schema() -> dict[str, Any]:
        return {
            "type": "object",
            "properties": {
                "Q": {"type": "integer"},
                "R": {"type": "integer"},
            },
            "required": ["Q", "R"],
            "additionalProperties": False,
        }

    @classmethod
    def _edge_schema(cls) -> dict[str, Any]:
        return {
            "type": "object",
            "properties": {
                "A": cls._cell_schema(),
                "B": cls._cell_schema(),
            },
            "required": ["A", "B"],
            "additionalProperties": False,
        }

    def _build_action_payload(self, action_id: str | None = None, **kwargs: Any) -> dict[str, Any]:
        resolved_action_id = action_id if action_id is not None else kwargs.pop("actionId")
        payload = {"actionId": resolved_action_id, "source": "McpAgent"}
        payload.update(kwargs)
        return payload

    def _execute_action(self, action_id: str, **kwargs: Any) -> CommandResult:
        return self.controller.game_execute_action(self._build_action_payload(action_id, **kwargs))

    def _set_tool(self, kind: str, value: str | None = None) -> CommandResult:
        normalized = kind.strip().lower()
        if normalized == "floor":
            return self._execute_action("SetFloorTool", floorPurposeKind=value or "Storage", panelId="McpGameplay", elementId="SetFloorTool")
        if normalized == "edge":
            return self._execute_action("SetEdgeTool", edgeFeatureKind=value or "Wall", panelId="McpGameplay", elementId="SetEdgeTool")
        if normalized == "module":
            return self._execute_action("SetModuleTool", moduleKind=value or "Bed", panelId="McpGameplay", elementId="SetModuleTool")
        if normalized == "landmark":
            return self._execute_action("SetLandmarkTool", landmarkKind=value or "TownCenterCore", panelId="McpGameplay", elementId="SetLandmarkTool")
        if normalized == "clear-cell":
            return self._execute_action("SetClearCellTool", panelId="McpGameplay", elementId="SetClearCellTool")
        if normalized == "clear-edge":
            return self._execute_action("SetClearEdgeTool", panelId="McpGameplay", elementId="SetClearEdgeTool")
        return CommandResult(False, f"Unsupported tool kind: {kind}", {})

    def _simulation_control(self, action: str, tick_count: int | None = None, speed_multiplier: float | None = None, running: bool | None = None) -> CommandResult:
        normalized = action.strip().lower()
        if normalized == "play":
            return self._execute_action("SetRealtimeRunning", desiredRunningState=True, panelId="McpGameplay", elementId="PlayRealTime")
        if normalized == "pause":
            return self._execute_action("SetRealtimeRunning", desiredRunningState=False, panelId="McpGameplay", elementId="PauseRealTime")
        if normalized == "tick":
            return self._execute_action("AdvanceSimulation", tickCount=max(1, int(tick_count or 1)), panelId="McpGameplay", elementId="AdvanceSimulation")
        if normalized == "speed":
            if speed_multiplier is None:
                return CommandResult(False, "speedMultiplier is required for simulation speed changes.", {})
            return self._execute_action("AdjustSimulationSpeed", speedMultiplier=float(speed_multiplier), panelId="McpGameplay", elementId="AdjustSimulationSpeed")
        if normalized == "running":
            if running is None:
                return CommandResult(False, "running is required for simulation running changes.", {})
            return self._execute_action("SetRealtimeRunning", desiredRunningState=bool(running), panelId="McpGameplay", elementId="SetRealtimeRunning")
        return CommandResult(False, f"Unsupported simulation action: {action}", {})

    def serve(self) -> int:
        while True:
            message = self._read_message()
            if message is None:
                return 0
            self._handle_message(message)

    def _handle_message(self, message: dict[str, Any]) -> None:
        method = message.get("method")
        message_id = message.get("id")
        params = message.get("params", {})

        try:
            if method == "initialize":
                self._send_result(
                    message_id,
                    {
                        "protocolVersion": "2024-11-05",
                        "capabilities": {
                            "tools": {},
                            "resources": {},
                        },
                        "serverInfo": {
                            "name": "ggj2026-unity-project-mcp",
                            "version": "1.0.0",
                        },
                    },
                )
                return

            if method == "notifications/initialized":
                return

            if method == "ping":
                self._send_result(message_id, {})
                return

            if method == "tools/list":
                tools = []
                for name, (schema, _) in self.tool_handlers.items():
                    tools.append(
                        {
                            "name": name,
                            "description": self._tool_description(name),
                            "inputSchema": schema,
                        }
                    )
                self._send_result(message_id, {"tools": tools})
                return

            if method == "tools/call":
                tool_name = params.get("name")
                arguments = params.get("arguments", {}) or {}
                if tool_name not in self.tool_handlers:
                    self._send_result(
                        message_id,
                        {
                            "content": [{"type": "text", "text": f"Unknown tool: {tool_name}"}],
                            "isError": True,
                        },
                    )
                    return

                _, handler = self.tool_handlers[tool_name]
                result = handler(arguments)
                text = result.message
                if result.data:
                    text = f"{text}\n\n{json.dumps(result.data, indent=2)}"
                self._send_result(
                    message_id,
                    {
                        "content": [{"type": "text", "text": text}],
                        "structuredContent": result.data,
                        "isError": not result.ok,
                    },
                )
                return

            if method == "resources/list":
                self._send_result(message_id, {"resources": self.controller.list_resources()})
                return

            if method == "resources/read":
                uri = params.get("uri")
                payload = self.controller.read_resource(uri)
                self._send_result(
                    message_id,
                    {
                        "contents": [
                            {
                                "uri": uri,
                                "mimeType": payload["mimeType"],
                                "text": payload["text"],
                            }
                        ]
                    },
                )
                return

            if method == "prompts/list":
                self._send_result(message_id, {"prompts": []})
                return

            self._send_error(message_id, -32601, f"Method not found: {method}")
        except Exception as exc:  # noqa: BLE001
            self._send_error(message_id, -32000, f"{exc}\n{traceback.format_exc()}")

    def _tool_description(self, name: str) -> str:
        descriptions = {
            "unity_project_status": "Return the current Unity project, bridge, compile, test, and telemetry status.",
            "unity_compile": "Compile the Unity project through the live editor bridge when available, otherwise run batch compilation.",
            "unity_run_tests": "Run editmode or playmode tests through the live Unity editor bridge when available, otherwise fall back to the repo-owned batch test wrapper.",
            "unity_cancel_tests": "Cancel or clear the currently active live Unity editor test run.",
            "unity_apply_ui_plan": "Preview or apply the maintained UI Toolkit plan through the live bridge or batch fallback.",
            "unity_open_scene": "Open a scene in the live Unity editor bridge.",
            "unity_play_mode": "Enter or exit play mode in the live Unity editor bridge.",
            "unity_list_manual_tests": "List guided manual test scenarios known to the live Unity editor bridge.",
            "unity_manual_test_status": "Read the current guided manual test run status from the live Unity editor bridge.",
            "unity_start_manual_test": "Start a guided manual test scenario in the live Unity editor bridge.",
            "unity_record_manual_test_step": "Record the current guided manual test step outcome in the live Unity editor bridge.",
            "unity_finish_manual_test": "Finish the currently active guided manual test run in the live Unity editor bridge.",
            "unity_game_hud_state": "Read the full runtime HUD state that the human interface is currently showing.",
            "unity_game_world_summary": "Read the world-bar summary shown in the human HUD.",
            "unity_game_selection": "Read the current gameplay selection and resolved context target.",
            "unity_game_hover": "Read the current hovered cell and edge in the live runtime.",
            "unity_game_list_entities": "List worker/entity summaries from the runtime HUD model.",
            "unity_game_list_actions": "List currently available gameplay actions from the shared action model.",
            "unity_game_action_schema": "Read the shared action schema for a gameplay action id.",
            "unity_game_execute_action": "Execute any shared gameplay action through the human/agent action layer.",
            "unity_game_blockers": "Read current gameplay blockers and shortages.",
            "unity_game_recent_events": "Read the current event strip plus latest gameplay telemetry ids.",
            "unity_game_select_cell": "Select a world cell through the shared gameplay action layer.",
            "unity_game_select_edge": "Select a world edge through the shared gameplay action layer.",
            "unity_game_select_entity": "Select/focus a worker or entity through the shared gameplay action layer.",
            "unity_game_set_tool": "Set the active gameplay tool through the shared action layer.",
            "unity_game_clear_tool": "Clear the active gameplay tool.",
            "unity_game_apply_brush": "Apply a floor or clear-cell action directly to a target cell.",
            "unity_game_apply_edge_stroke": "Apply an edge build or clear action directly to a target edge.",
            "unity_game_place_module": "Place a module through the shared gameplay action layer.",
            "unity_game_place_landmark": "Place a landmark through the shared gameplay action layer.",
            "unity_game_clear_cell": "Clear a target cell through the shared gameplay action layer.",
            "unity_game_clear_edge": "Clear a target edge through the shared gameplay action layer.",
            "unity_game_simulation_control": "Play, pause, step, or retime the live runtime simulation.",
            "unity_game_save_world": "Save the current local world through the shared gameplay action layer.",
            "unity_game_reset_world": "Reset the current local world through the shared gameplay action layer.",
            "unity_game_context_explain": "Ask the runtime to explain the current selection/context in plain language.",
            "unity_game_force_inspector": "Force the runtime inspector to a specific tab.",
            "unity_tail_log": "Read the tail of the latest compile, runtime, test, or telemetry log.",
        }
        return descriptions[name]

    def _read_message(self) -> dict[str, Any] | None:
        headers: dict[str, str] = {}
        while True:
            line = sys.stdin.buffer.readline()
            if not line:
                return None
            if line in (b"\r\n", b"\n"):
                if not headers:
                    return None
                break
            key, value = line.decode("utf-8").split(":", 1)
            headers[key.strip().lower()] = value.strip()

        content_length = int(headers["content-length"])
        body = sys.stdin.buffer.read(content_length)
        return json.loads(body.decode("utf-8"))

    def _send_result(self, message_id: Any, result: dict[str, Any]) -> None:
        self._send({"jsonrpc": "2.0", "id": message_id, "result": result})

    def _send_error(self, message_id: Any, code: int, message: str) -> None:
        self._send({"jsonrpc": "2.0", "id": message_id, "error": {"code": code, "message": message}})

    def _send(self, payload: dict[str, Any]) -> None:
        encoded = json.dumps(payload, separators=(",", ":"), ensure_ascii=False).encode("utf-8")
        sys.stdout.buffer.write(f"Content-Length: {len(encoded)}\r\n\r\n".encode("utf-8"))
        sys.stdout.buffer.write(encoded)
        sys.stdout.buffer.flush()


def main() -> int:
    controller = UnityProjectController(PROJECT_ROOT)
    return JsonRpcMcpServer(controller).serve()


if __name__ == "__main__":
    raise SystemExit(main())
