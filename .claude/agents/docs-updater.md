---
description: Update CLAUDE.md and documentation after code changes
tools:
  - Bash
  - Read
  - Edit
  - Grep
  - Glob
---

# Documentation Updater Agent

Analyze recent code changes and update CLAUDE.md and other documentation to keep them in sync with the codebase.

## When to Use

Run this agent after making changes that affect:
- Configuration classes (new options, environment variables)
- Slash commands (new commands, changed parameters)
- Project structure (new directories, services)
- Dependencies (new NuGet packages)
- Code patterns or conventions

## Instructions

### 1. Analyze Recent Changes

Check what was recently modified:
```bash
git diff HEAD~1 --name-only
git diff HEAD~1 --stat
```

For unstaged changes:
```bash
git diff --name-only
```

### 2. Identify Documentation Impact

For each changed area, check if CLAUDE.md needs updates:

| Changed Files | CLAUDE.md Section to Update |
|---------------|----------------------------|
| `Configuration/*.cs` | "Required Environment Variables" - add new `EROSTTS_SectionName__PropertyName` entries |
| `Commands/*.cs` | "Slash Commands" - add/update command descriptions |
| `Services/*/` (new directory) | "Architecture" tree diagram |
| `*.csproj` (new packages) | "Key Technologies" list |
| New patterns introduced | "Code Patterns" section |

### 3. Read Current Documentation

```bash
# Read CLAUDE.md
cat CLAUDE.md
```

### 4. Update Documentation

Make targeted edits to keep documentation accurate:

**For new configuration options:**
- Add environment variable to "Required Environment Variables" section
- Follow format: `- \`EROSTTS_Section__Property\` - Description (required/optional, default: value)`

**For new slash commands:**
- Add to appropriate subsection (TTS Commands or AI Character Commands)
- Follow format: `- \`/command-name <required> [optional]\` - Description (ephemeral/public)`

**For new services/directories:**
- Update the ASCII tree in "Architecture" section
- Keep consistent indentation with `├──` and `└──`

**For new dependencies:**
- Add to "Key Technologies" with brief description
- Format: `- **PackageName** - What it's used for`

### 5. Verify Changes

After editing, verify the documentation is valid:
- Check markdown formatting
- Ensure environment variable names match actual config binding (`Section:Property` → `EROSTTS_Section__Property`)
- Confirm command signatures match the actual code

## Example: New Configuration Property

If `OpenRouterConfiguration.cs` adds `DefaultSystemPrompt`:

1. Check the property:
```csharp
public string DefaultSystemPrompt { get; init; } = string.Empty;
```

2. Add to CLAUDE.md "Required Environment Variables":
```markdown
- `EROSTTS_OpenRouter__DefaultSystemPrompt` - Default system prompt prepended to all AI requests (optional)
```

## Example: New Slash Command

If `CharacterCommands.cs` adds `/character-export`:

1. Check the command signature and attributes
2. Add to CLAUDE.md "AI Character Commands":
```markdown
- `/character-export` - Export current character context to file (ephemeral)
```

## Output

Report what was updated:
- List of sections modified in CLAUDE.md
- Summary of changes made
- Any manual review needed (e.g., descriptions that need human input)
