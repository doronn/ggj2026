# Control Surface

## Primary artifacts

- Compile report: `/Users/doronnacash/RiderProjects/adventure-game/ggj2026/Logs/Diagnostics/compile-latest.log`
- Runtime log: `/Users/doronnacash/RiderProjects/adventure-game/ggj2026/Logs/PersistentWorld/runtime-latest.log`
- Latest telemetry: `/Users/doronnacash/RiderProjects/adventure-game/ggj2026/Logs/Telemetry/session-latest`
- Bridge status: `/Users/doronnacash/RiderProjects/adventure-game/ggj2026/Logs/McpBridge/status.json`
- Latest edit-mode XML: `/Users/doronnacash/RiderProjects/adventure-game/ggj2026/Logs/TestResults/editmode-results.xml`
- Latest play-mode XML: `/Users/doronnacash/RiderProjects/adventure-game/ggj2026/Logs/TestResults/playmode-results.xml`

## Preferred commands

- Start MCP server:
  - `/Users/doronnacash/RiderProjects/adventure-game/ggj2026/Tools/run-unity-mcp-server.sh`
- Batch tests when editor is closed:
  - `/Users/doronnacash/RiderProjects/adventure-game/ggj2026/Tools/run-unity-tests.sh --platform editmode`
  - `/Users/doronnacash/RiderProjects/adventure-game/ggj2026/Tools/run-unity-tests.sh --platform playmode`

## Live bridge tools

- `unity_project_status`
- `unity_compile`
- `unity_run_tests`
- `unity_cancel_tests`
- `unity_apply_ui_plan`
- `unity_polygon_visual_maintenance`
- `unity_open_scene`
- `unity_play_mode`
- `unity_list_manual_tests`
- `unity_manual_test_status`
- `unity_start_manual_test`
- `unity_record_manual_test_step`
- `unity_finish_manual_test`
- `unity_tail_log`

## Known behavior

- Live play-mode test runs may return `recovered from editor state` or `recovered after cleanup error`.
- In this project, that usually means Unity’s transitive performance-testing cleanup threw after a passing run.
- Accept the run if the recovered NUnit XML matches the requested platform and reports zero failures.
