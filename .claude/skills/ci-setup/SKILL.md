---
name: ci-setup
description: Scaffold GitHub Actions CI/CD workflows for build, test, and Docker publish
disable-model-invocation: true
---

# CI/CD Setup

Scaffold GitHub Actions workflows for the ErosTTS Discord bot.

## Instructions

1. **Ask the user which workflows they want** using AskUserQuestion with these options (multiSelect):
   - **Build & Test** (Recommended) - Run `dotnet build` and `dotnet test` on push and PR to main
   - **Docker Publish** - Build and push Docker image to GHCR on version tags (e.g., `v1.0.0`)
   - **Dependabot** - Automated NuGet dependency updates

2. **Create the directory** if it doesn't exist:
   ```bash
   mkdir -p .github/workflows
   ```

3. **Generate the selected workflows**:

   ### Build & Test (`ci.yml`)
   - Trigger: push to `main`, pull requests to `main`
   - Steps: checkout, setup .NET 10, restore, build, test
   - Use `dotnet test` with `--logger trx` for structured results
   - Reference the solution file: `ErosTTS.sln`

   ### Docker Publish (`docker-publish.yml`)
   - Trigger: push tags matching `v*` (e.g., `v1.0.0`)
   - Steps: checkout, log in to GHCR using `GITHUB_TOKEN`, build with `docker/Dockerfile`, push to `ghcr.io/andresmorales07/eros-discord-bot`
   - Tag with both the version and `latest`
   - Use `docker/metadata-action` for tag extraction

   ### Dependabot (`dependabot.yml` in `.github/`)
   - Package ecosystem: `nuget`
   - Directory: `/src/ErosTTS.Bot`
   - Schedule: weekly
   - Target branch: `main`

4. **Verify the generated files**:
   - Read back each file and confirm it's valid YAML
   - Check that action versions reference recent stable releases

5. **Report results**:
   - List the files created
   - Note any GitHub repository settings the user needs to configure (e.g., enabling GHCR write permissions for `GITHUB_TOKEN`)
   - Remind the user to commit and push the workflow files to activate them
