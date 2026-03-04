# Test Strategy

## Rule
Every maintained `_Game` system with behavioral logic must have automated tests.

## Edit mode
Use edit mode tests for:
- pure logic
- serializers
- manifest validation
- world diff generation
- invariant checks
- storage/room rules
- UI asset existence and naming checks

## Play mode
Use play mode tests for:
- runtime UI Toolkit controller binding
- runtime telemetry output
- input-capture behavior
- integrated shell flows that depend on Unity runtime objects

## Batch execution
- Preferred wrapper:
  - `Tools/run-unity-tests.sh --platform editmode`
  - `Tools/run-unity-tests.sh --platform playmode`
- Latest result artifacts:
  - `Logs/TestResults/editmode-results.xml`
  - `Logs/TestResults/editmode-batch.log`
  - `Logs/TestResults/playmode-results.xml`
  - `Logs/TestResults/playmode-batch.log`
- The wrapper also archives timestamped copies of both files for each run.
- Do not add `-quit` to command-line Unity test runs. In this project’s installed Unity Test Framework package, `-quit` prevents the command-line run from executing.

## Manual tests
Use guided manual scenarios for:
- interaction sanity
- milestone regression sweeps
- UX checks that are costly or ambiguous to automate fully

## Merge gates
- New behavioral code: edit mode tests required
- New integrated runtime feature: play mode scenario required
- New milestone UI/system: guided manual scenario required

## Minimum regression focus
- UI must not leak clicks into the world behind it
- telemetry must remain writable and parseable
- world diffs must still reflect the action that produced them
