---
name: ef-migrate
description: Create a new EF Core migration with validation and optional docs update
disable-model-invocation: true
---

# EF Core Migration

Create a new Entity Framework Core migration for the ErosTTS database.

## Instructions

1. **Ask for the migration name** using AskUserQuestion if no name was provided as an argument. Suggest a name based on recent code changes (e.g., `AddNpcVoiceOverride`, `UpdateGuildSettings`). Migration names should be PascalCase with no spaces.

2. **Verify there are pending model changes** by checking for recent modifications to:
   - `src/ErosTTS.Bot/Data/ErosTtsDbContext.cs`
   - `src/ErosTTS.Bot/Data/Entities/*.cs`

   If no entity or DbContext changes are detected, warn the user and ask if they want to proceed anyway.

3. **Run the migration command**:
   ```bash
   dotnet ef migrations add <MigrationName> --project src/ErosTTS.Bot --output-dir Data/Migrations
   ```

4. **Verify the migration was created**:
   - Check that new files appeared in `src/ErosTTS.Bot/Data/Migrations/`
   - Read the generated migration file and summarize what changes it contains (tables added, columns modified, etc.)

5. **Build to verify** the migration compiles:
   ```bash
   dotnet build src/ErosTTS.Bot
   ```

6. **Run the tests** to verify nothing is broken:
   ```bash
   dotnet test tests/ErosTTS.Bot.Tests --verbosity normal --no-restore
   ```

7. **Report results**:
   - Migration file path and summary of schema changes
   - Build result (success/failure)
   - Test result (pass/fail count)
   - Remind the user to run the `docs-updater` agent if the migration adds new configuration or changes the data model significantly
