---
name: docker-publish
description: Build and publish the Docker image to GitHub Container Registry (ghcr.io)
user_invocable: true
---

# Docker Publish to GHCR

Build the Docker image and push it to GitHub Container Registry.

## Instructions

1. **Always ask the user for a tag** using AskUserQuestion before proceeding. Suggest `latest` and the current git short SHA as options.

2. **Verify prerequisites**:
   - Run `gh auth status` to confirm the user is authenticated with GitHub CLI
   - Run `docker info` to confirm Docker daemon is running
   - If either check fails, tell the user what to fix and stop

3. **Authenticate Docker with GHCR** (if not already):
   ```bash
   gh auth token | docker login ghcr.io -u USERNAME --password-stdin
   ```
   Get the GitHub username from `gh api user --jq .login`.

4. **Build the image** from the repo root:
   ```bash
   docker build -f docker/Dockerfile -t ghcr.io/andresmorales07/eros-discord-bot:TAG .
   ```
   Replace `TAG` with the user's chosen tag.

5. **Push the image**:
   ```bash
   docker push ghcr.io/andresmorales07/eros-discord-bot:TAG
   ```

6. **Report the result**:
   - On success: confirm the full image URI that was pushed (e.g. `ghcr.io/andresmorales07/eros-discord-bot:TAG`)
   - On failure: show the error output and suggest fixes

7. **If the user chose a tag other than `latest`**, ask whether they also want to tag and push as `latest`.
