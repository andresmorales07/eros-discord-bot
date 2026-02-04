---
description: Run .NET unit tests and report results with failure details
tools:
  - Bash
  - Read
  - Grep
---

# Test Runner Agent

Run the unit tests for ErosTTS.Bot and provide a clear summary of results.

## Instructions

1. **Run Tests**: Execute the test suite with normal verbosity:
   ```bash
   dotnet test tests/ErosTTS.Bot.Tests --verbosity normal --no-restore
   ```

2. **Parse Results**: Analyze the output and report:
   - Total tests run, passed, failed, skipped
   - For any failures: test name, assertion message, and relevant stack trace
   - Map failing tests to source files:
     - `ElevenLabsTtsServiceTests` → `src/ErosTTS.Bot/Services/TTS/ElevenLabsTtsService.cs`
     - `OpenRouterServiceTests` → `src/ErosTTS.Bot/Services/LLM/OpenRouterService.cs`
     - `TtsQueueTests` → `src/ErosTTS.Bot/Services/Queue/TtsQueue.cs`
     - `CharacterStateServiceTests` → `src/ErosTTS.Bot/Services/Character/CharacterStateService.cs`
     - `GuildConfigurationServiceTests` → `src/ErosTTS.Bot/Services/Guild/GuildConfigurationService.cs`
     - `TextSanitizerTests` → `src/ErosTTS.Bot/Utilities/TextSanitizer.cs`

3. **Report Findings**:
   - If all tests pass: confirm success with the count
   - If tests fail: list each failing test with expected vs actual values and suggested source file to fix
   - If tests fail to build: report compilation errors first

## Example Output

```
Test Results: 102 passed, 0 failed, 0 skipped

All tests passed.
```

Or for failures:

```
Test Results: 100 passed, 2 failed, 0 skipped

FAILURES:

1. TextSanitizerTests.Sanitize_RemovesEmojis_FromText
   Expected: "Hello world"
   Actual: "Hello 👋 world"
   Source: src/ErosTTS.Bot/Utilities/TextSanitizer.cs

Suggested fix: Update the emoji regex pattern in TextSanitizer.cs
```
