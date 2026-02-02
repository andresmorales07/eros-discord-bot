# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ErosTTS is a Discord bot that converts text to speech using the Eleven Labs API and plays audio in Discord voice channels. Built with .NET 10, it uses NetCord for Discord integration and supports both slash commands and optional text channel monitoring.

The bot also supports **AI-powered character roleplaying** using OpenRouter API, allowing it to play characters in D&D sessions or similar roleplaying scenarios.

## Build & Run Commands

```bash
# Run the bot (from repo root)
cd src/ErosTTS.Bot && dotnet run

# Build only
dotnet build

# Build release
dotnet build -c Release

# Run with Docker
cd docker && docker-compose up -d
```

## Required Environment Variables

- `EROSTTS_Discord__Token` - Discord bot token (required)
- `EROSTTS_ElevenLabs__ApiKey` - Eleven Labs API key (required)
- `EROSTTS_ElevenLabs__VoiceId` - Voice ID (optional, defaults to Rachel)
- `EROSTTS_Voice__FFmpegPath` - Path to FFmpeg (optional, defaults to `ffmpeg`)
- `EROSTTS_OpenRouter__ApiKey` - OpenRouter API key (required for AI features)
- `EROSTTS_OpenRouter__Model` - LLM model ID (optional, defaults to `anthropic/claude-3.5-sonnet`)

## Architecture

```
src/ErosTTS.Bot/
├── Commands/              # Slash commands (TtsCommands.cs, CharacterCommands.cs)
├── Configuration/         # Options pattern config classes
├── Exceptions/            # Custom exception types (TtsExceptions, LlmExceptions)
├── HostedServices/        # Background services
│   ├── GatewayEventHostedService.cs  # Discord event handlers
│   └── TtsProcessorService.cs        # Queue processor
├── Services/
│   ├── Audio/             # Discord voice playback (AudioService)
│   ├── Character/         # Per-guild character state (context, conversation history)
│   ├── Guild/             # Per-guild configuration storage
│   ├── LLM/               # OpenRouter API client for AI responses
│   ├── Queue/             # TTS message queue (System.Threading.Channels)
│   └── TTS/               # Eleven Labs API client
├── Utilities/             # Text sanitization utilities
└── Program.cs             # Host builder and DI configuration
```

## Key Technologies

- **NetCord** (v1.0.0-alpha.460) - Discord library for .NET
- **Eleven Labs API** - Text-to-speech synthesis
- **OpenRouter API** - LLM access for AI character responses
- **Polly** - HTTP retry policies with exponential backoff
- **Serilog** - Structured logging to console and rolling files
- **System.Threading.Channels** - Async message queue
- **OpusDotNet** - Audio codec for Discord voice

## Code Patterns

- **Options pattern**: All configuration uses `IOptions<T>` with sections bound in `Program.cs`
- **Hosted services**: Background work via `IHostedService` (queue processing, gateway events)
- **Slash commands**: NetCord's `ApplicationCommandModule<ApplicationCommandContext>` base class
- **DI**: All services registered in `Program.cs` ConfigureServices
- **Async throughout**: Methods return `Task` or `Task<T>`

## Configuration Modes

1. **Slash command only** (default): Uses `/say` command, no privileged intents required
2. **Text channel monitoring**: Set `Discord:EnableTextChannelMonitoring=true`, requires Message Content Intent

## Slash Commands

All commands respond with ephemeral messages (only visible to the user) except `/prompt` which is public.

### TTS Commands (all ephemeral)
- `/say <text> [voice-channel]` - Speak text in voice channel
- `/tts-setup <voice-channel> [text-channel]` - Configure defaults (Manage Guild permission)
- `/tts-stop` - Disconnect from voice
- `/tts-status` - Show current configuration
- `/tts-clear` - Remove configuration (Manage Guild permission)

### AI Character Commands
- `/character-context <context> [append]` - Set or append character context/system prompt (ephemeral)
- `/prompt <message>` - Send a prompt to the AI character, response played via TTS (**public** - visible to all)
- `/character-clear` - Clear character context and conversation history (ephemeral)
- `/character-status` - View current character state (ephemeral)

## Voice Channel Behavior

- Bot joins voice channels **self-deafened** by default (doesn't listen to voice chat)
- Uses `UpdateVoiceStateAsync` with `SelfDeaf = true` after connecting

## External Dependencies

- **FFmpeg** - Required for audio processing, must be in PATH or configured via `Voice:FFmpegPath`
- **Opus codec** - Included via NuGet package `OpusDotNet.opus.win-x64`
