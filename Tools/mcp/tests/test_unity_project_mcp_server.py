import importlib.util
import json
import os
import sys
import tempfile
import unittest
from pathlib import Path


MODULE_PATH = Path(__file__).resolve().parents[1] / "unity_project_mcp_server.py"
SPEC = importlib.util.spec_from_file_location("unity_project_mcp_server", MODULE_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


class UnityProjectMcpServerTests(unittest.TestCase):
    def test_parse_timestamp_accepts_unity_heartbeat_precision(self) -> None:
        value = "2026-03-02T16:59:10.3124590Z"

        parsed = MODULE.parse_timestamp(value)

        self.assertEqual(parsed.tzinfo.utcoffset(parsed).total_seconds(), 0)
        self.assertEqual(parsed.year, 2026)
        self.assertEqual(parsed.month, 3)
        self.assertEqual(parsed.day, 2)

    def test_parse_compile_report_extracts_summary(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "compile-latest.log"
            path.write_text(
                "[2026-03-02T16:00:00.0000000Z] [SUMMARY] FinishedAt=2026-03-02T16:00:02.0000000Z\n"
                "[2026-03-02T16:00:00.0000000Z] [SUMMARY] errors=1 warnings=3\n",
                encoding="utf-8",
            )

            summary = MODULE.parse_compile_report(path)

            self.assertTrue(summary["exists"])
            self.assertEqual(summary["errors"], 1)
            self.assertEqual(summary["warnings"], 3)
            self.assertEqual(summary["finished_at"], "2026-03-02T16:00:02.0000000Z")

    def test_parse_test_results_extracts_failure_messages(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "results.xml"
            path.write_text(
                "<test-run total='2' passed='1' failed='1' skipped='0' result='Failed(Child)'>"
                "<test-suite><test-case fullname='Suite.Pass' result='Passed' />"
                "<test-case fullname='Suite.Fail' result='Failed'>"
                "<failure><message>Broken</message></failure></test-case>"
                "</test-suite></test-run>",
                encoding="utf-8",
            )

            summary = MODULE.parse_test_results(path)

            self.assertEqual(summary["failed"], 1)
            self.assertEqual(summary["failures"][0]["fullname"], "Suite.Fail")
            self.assertIn("Broken", summary["failures"][0]["message"])

    def test_project_status_reports_bridge_and_artifacts(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            (root / "Logs" / "Diagnostics").mkdir(parents=True)
            (root / "Logs" / "PersistentWorld").mkdir(parents=True)
            (root / "Logs" / "TestResults").mkdir(parents=True)
            (root / "Logs" / "Telemetry" / "session-latest").mkdir(parents=True)
            (root / "Logs" / "McpBridge").mkdir(parents=True)

            (root / "Logs" / "Diagnostics" / "compile-latest.log").write_text(
                "[x] [SUMMARY] errors=0 warnings=0\n",
                encoding="utf-8",
            )
            (root / "Logs" / "PersistentWorld" / "runtime-latest.log").write_text("runtime", encoding="utf-8")
            (root / "Logs" / "Telemetry" / "session-latest" / "session.json").write_text(
                json.dumps({"sessionId": "abc", "actionCount": 7}),
                encoding="utf-8",
            )
            (root / "Logs" / "McpBridge" / "status.json").write_text(
                json.dumps(
                    {
                        "projectPath": str(root),
                        "isAvailable": True,
                        "lastHeartbeatUtc": MODULE.utc_now(),
                    }
                ),
                encoding="utf-8",
            )

            controller = MODULE.UnityProjectController(root)
            result = controller.project_status()

            self.assertTrue(result.ok)
            self.assertTrue(result.data["bridge"]["available"])
            self.assertEqual(result.data["telemetrySession"]["sessionId"], "abc")
            self.assertEqual(result.data["compile"]["errors"], 0)

    def test_get_bridge_status_accepts_stale_heartbeat_when_editor_process_is_alive(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            (root / "Logs" / "McpBridge").mkdir(parents=True)
            (root / "Logs" / "McpBridge" / "status.json").write_text(
                json.dumps(
                    {
                        "projectPath": str(root),
                        "isAvailable": True,
                        "editorProcessId": os.getpid(),
                        "lastHeartbeatUtc": "2026-03-01T00:00:00.0000000Z",
                    }
                ),
                encoding="utf-8",
            )

            controller = MODULE.UnityProjectController(root)
            result = controller.get_bridge_status()

            self.assertTrue(result["available"])
            self.assertFalse(result["fresh"])
            self.assertTrue(result["processAlive"])

    def test_run_tests_prefers_live_bridge_when_available(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            controller = MODULE.UnityProjectController(root)

            controller.get_bridge_status = lambda: {"available": True, "path": "bridge-status"}  # type: ignore[method-assign]
            controller.send_bridge_command = lambda command, payload=None, timeout_seconds=0: MODULE.CommandResult(  # type: ignore[method-assign]
                True,
                "editmode tests finished.",
                {
                    "platform": "editmode",
                    "resultsPath": "/tmp/editmode-results.xml",
                    "passed": 3,
                    "failed": 0,
                },
            )

            result = controller.run_tests("editmode")

            self.assertTrue(result.ok)
            self.assertEqual(result.data["mode"], "bridge")
            self.assertEqual(result.data["tests"]["passed"], 3)
            self.assertEqual(result.data["resultsPath"], "/tmp/editmode-results.xml")

    def test_cancel_tests_uses_live_bridge(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            controller = MODULE.UnityProjectController(root)

            controller.get_bridge_status = lambda: {"available": True, "path": "bridge-status"}  # type: ignore[method-assign]
            controller.send_bridge_command = lambda command, payload=None, timeout_seconds=0: MODULE.CommandResult(  # type: ignore[method-assign]
                True,
                "Canceled active Unity test run.",
                {"canceled": True},
            )

            result = controller.cancel_tests()

            self.assertTrue(result.ok)
            self.assertEqual(result.data["canceled"], True)

    def test_manual_test_commands_use_live_bridge(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            controller = MODULE.UnityProjectController(root)

            controller.send_bridge_command = lambda command, payload=None, timeout_seconds=0: MODULE.CommandResult(  # type: ignore[method-assign]
                True,
                f"{command} ok",
                {"command": command, "payload": payload or {}},
            )

            status = controller.manual_test_status()
            started = controller.start_manual_test("foundation-ui-sanity")
            recorded = controller.record_manual_test_step("Pass", "looks good")
            finished = controller.finish_manual_test()

            self.assertTrue(status.ok)
            self.assertEqual(status.data["command"], "manual_test_status")
            self.assertEqual(started.data["payload"]["scenarioId"], "foundation-ui-sanity")
            self.assertEqual(recorded.data["payload"]["result"], "Pass")
            self.assertEqual(recorded.data["payload"]["note"], "looks good")
            self.assertEqual(finished.data["command"], "finish_manual_test")

    def test_gameplay_query_commands_use_live_bridge(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            controller = MODULE.UnityProjectController(root)

            controller.send_bridge_command = lambda command, payload=None, timeout_seconds=0: MODULE.CommandResult(  # type: ignore[method-assign]
                True,
                f"{command} ok",
                {"command": command, "payload": payload or {}},
            )

            hud = controller.game_hud_state()
            selection = controller.game_selection()
            schema = controller.game_action_schema("PaintFloor")

            self.assertTrue(hud.ok)
            self.assertEqual(hud.data["command"], "game_hud_state")
            self.assertEqual(selection.data["command"], "game_selection")
            self.assertEqual(schema.data["payload"]["actionId"], "PaintFloor")

    def test_gameplay_action_command_uses_live_bridge(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            controller = MODULE.UnityProjectController(root)

            controller.send_bridge_command = lambda command, payload=None, timeout_seconds=0: MODULE.CommandResult(  # type: ignore[method-assign]
                True,
                f"{command} ok",
                {"command": command, "payload": payload or {}},
            )

            result = controller.game_execute_action(
                {
                    "actionId": "PaintFloor",
                    "targetCell": {"Q": 2, "R": -1},
                    "hasTargetCell": True,
                    "floorPurposeKind": "Storage",
                }
            )

            self.assertTrue(result.ok)
            self.assertEqual(result.data["command"], "game_execute_action")
            self.assertEqual(result.data["payload"]["actionId"], "PaintFloor")
            self.assertEqual(result.data["payload"]["targetCell"]["Q"], 2)

    def test_gameplay_action_command_supports_defender_orders(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            controller = MODULE.UnityProjectController(root)

            controller.send_bridge_command = lambda command, payload=None, timeout_seconds=0: MODULE.CommandResult(  # type: ignore[method-assign]
                True,
                f"{command} ok",
                {"command": command, "payload": payload or {}},
            )

            result = controller.game_execute_action(
                {
                    "actionId": "DefendArea",
                    "targetCell": {"Q": 4, "R": -1, "S": -3},
                    "hasTargetCell": True,
                }
            )

            self.assertTrue(result.ok)
            self.assertEqual(result.data["payload"]["actionId"], "DefendArea")
            self.assertEqual(result.data["payload"]["targetCell"]["Q"], 4)


if __name__ == "__main__":
    unittest.main()
