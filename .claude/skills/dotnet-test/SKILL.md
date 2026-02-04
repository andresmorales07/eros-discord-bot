---
name: dotnet-test
description: Run .NET unit tests and report results with failure details
---

# .NET Test Runner

Run the unit tests for ErosTTS.Bot and provide a clear summary of results.

## Instructions

1. Run the tests with normal verbosity to capture details:
```bash
dotnet test tests/ErosTTS.Bot.Tests --verbosity normal --no-restore
```

2. Parse the output and report:
   - Total tests run, passed, failed, skipped
   - For any failures: test name, assertion message, and relevant stack trace
   - Suggest which source files likely need fixes based on the failing test names

3. If all tests pass, confirm success with the count.

4. If tests fail to build, report the compilation errors first.
