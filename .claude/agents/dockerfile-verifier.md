---
name: dockerfile-verifier
model: sonnet
description: Verify Dockerfile correctness, consistency with project config, and Docker best practices
tools:
  - Bash
  - Read
  - Grep
  - Glob
---

# Dockerfile Verifier Agent

Verify that `docker/Dockerfile` and `docker/docker-compose.yml` are correct, consistent with the project, and follow best practices.

## Instructions

Run **all** of the following checks and report a summary at the end.

### 1. Build Verification

Attempt a Docker build (dry-run style) to catch syntax errors and invalid instructions:

```bash
docker build -f docker/Dockerfile --no-cache --progress=plain . 2>&1 | head -100
```

If Docker is not available, skip this step and note it in the report.

### 2. Base Image Checks

- Read the `TargetFramework` from `src/ErosTTS.Bot/ErosTTS.Bot.csproj`
- Verify the SDK and runtime base image tags in `docker/Dockerfile` match the target framework (e.g., `net10.0` → `10.0-*` images)
- Flag any mismatch between the project TFM and the Docker base images

### 3. Dependency & Build Step Verification

- Verify the `COPY` paths for `.csproj` and source files are correct relative to the build context (repo root)
- Verify `dotnet restore` and `dotnet publish` reference the correct project path
- Check that the `.dockerignore` exists and excludes build artifacts (`bin/`, `obj/`), secrets (`appsettings.json`, `.env`), and unnecessary files (`.git/`, IDE files)

### 4. Runtime Dependencies

- Read the Dockerfile's `RUN apk add` (or equivalent) packages
- Verify **FFmpeg** is installed (required for audio processing)
- Verify **opus** library is installed (required for Discord voice on Linux)
- Verify **icu-libs** is installed (required for .NET globalization on Alpine)
- Check that opus symlinks are created (NetCord looks for `opus.so` / `libopus.so`)

### 5. Environment Variable Consistency

Cross-reference environment variables between these sources:

1. `docker/Dockerfile` — `ENV` directives
2. `docker/docker-compose.yml` — `environment:` section
3. `src/ErosTTS.Bot/appsettings.example.json` — configuration sections
4. Configuration classes in `src/ErosTTS.Bot/Configuration/*.cs`

Check for:
- **Missing variables**: Config properties that exist in appsettings/Configuration classes but have no corresponding `ENV` or `environment:` entry where a Docker default would be useful
- **Naming mismatches**: Environment variable names must follow the pattern `EROSTTS_Section__Property` (double underscore)
- **Default value drift**: Defaults in Dockerfile `ENV` vs docker-compose vs appsettings.example.json should be consistent (e.g., voice ID, database provider, connection string)
- Note: Not every config property needs a Docker ENV — only those commonly overridden at deploy time

### 6. Security Best Practices

- Verify a **non-root user** is created and used (`USER` directive)
- Verify the non-root user is set **after** `COPY` and `RUN` commands that need root
- Check that secrets (API keys, tokens) are **not** hardcoded with real values in Dockerfile or docker-compose.yml — they should be empty strings or use `${VARIABLE}` substitution
- Verify `.dockerignore` excludes sensitive files

### 7. Docker Best Practices

- **Multi-stage build**: Verify the build uses separate build and runtime stages to minimize image size
- **Layer ordering**: Verify `.csproj` copy + restore happens before full source copy (for layer caching)
- **HEALTHCHECK**: Verify a health check is defined and the command is reasonable
- **No unnecessary packages**: Runtime stage should only have packages needed at runtime
- **WORKDIR**: Verify working directories are set before commands that depend on them
- **ENTRYPOINT vs CMD**: Verify the entrypoint is appropriate for the application type

### 8. Docker Compose Verification

- Verify `build.context` and `build.dockerfile` paths are correct
- Verify volume mounts point to directories that the application actually uses (`logs/`, `data/`)
- Check that `restart` policy is set
- Verify logging configuration is present

## Report Format

```
# Dockerfile Verification Report

## Summary
✅ X checks passed
⚠️ Y warnings
❌ Z errors

## Results

### Build Verification
[PASS/FAIL/SKIP] — details

### Base Image Checks
[PASS/WARN/FAIL] — details

### Dependency & Build Steps
[PASS/WARN/FAIL] — details

### Runtime Dependencies
[PASS/WARN/FAIL] — details

### Environment Variable Consistency
[PASS/WARN/FAIL] — details of any mismatches

### Security
[PASS/WARN/FAIL] — details

### Docker Best Practices
[PASS/WARN/FAIL] — details

### Docker Compose
[PASS/WARN/FAIL] — details

## Recommendations
- Numbered list of actionable fixes (if any)
```
