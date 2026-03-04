# Compilation Workflow

## Unity menu actions
- `Window > GGJ2026 > Diagnostics > Refresh Assets And Recompile`
- `Window > GGJ2026 > Diagnostics > Open Latest Compile Report`
- `Window > GGJ2026 > Diagnostics > Copy Latest Compile Report Path`

## Compile report output
- `Logs/Diagnostics/compile-latest.log`
- `Logs/Diagnostics/compile-YYYYMMDD-HHMMSS.log`
- `compile-latest.log` may start as a placeholder file before the first captured compile.

## What the tool does
- Saves assets.
- Forces an asset refresh.
- Requests a clean script compilation with `CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache)`.
- Records per-assembly compiler messages and a final summary into repo-local log files.
- Also auto-captures ambient Unity compilations, so `compile-latest.log` is usually present even after a normal editor-triggered recompile.

## Batchmode entry point
- `Ggj2026.Game.Authoring.Editor.ProjectCompilationTools.RefreshAssetsAndRecompileFromCommandLine`

Use this with Unity `-executeMethod` when the editor is not already open on the project.

## Batch test workflow
- Preferred wrapper:
  - `Tools/run-unity-tests.sh --platform editmode`
  - `Tools/run-unity-tests.sh --platform playmode`
- This wrapper:
  - uses the correct no-`-quit` Unity CLI shape for this repo,
  - clears a stale `Temp/UnityLockfile` when the editor is not running,
  - writes latest test artifacts to `Logs/TestResults`,
  - and archives timestamped copies of each run.
- Latest test outputs:
  - `Logs/TestResults/editmode-results.xml`
  - `Logs/TestResults/editmode-batch.log`
  - `Logs/TestResults/playmode-results.xml`
  - `Logs/TestResults/playmode-batch.log`

## Live editor limitations
- Unity will reject a second batchmode instance while the same project is already open in the editor.
- On macOS, terminal-driven UI automation of Unity may fail if Accessibility permissions are not granted.
- Command-line Unity test runs in this project should not include `-quit`; the installed Test Framework package skips the run when `-quit` is present.
- If either limitation blocks triggering the menu action externally, let the open editor perform the recompile and then inspect:
  - `Logs/Diagnostics/compile-latest.log`
  - or `~/Library/Logs/Unity/Editor.log` as fallback

## Recommended agent check order
1. Trigger the compile workflow from Unity.
2. Read `Logs/Diagnostics/compile-latest.log`.
3. If compile succeeds and runtime behavior matters, run Play Mode and then read `Logs/PersistentWorld/runtime-latest.log`.
4. If the compile report is incomplete or Unity editor code is failing before the report finishes, fall back to `~/Library/Logs/Unity/Editor.log`.
