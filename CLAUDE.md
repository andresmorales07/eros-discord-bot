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

# Run with Docker (GHCR image)
docker compose up -d

# Run with Docker (local build)
cd docker && docker-compose up -d
```

## Environment Variables

### Required
| Variable | Description |
|----------|-------------|
| `EROSTTS_Discord__Token` | Discord bot token |
| `EROSTTS_ElevenLabs__ApiKey` | Eleven Labs API key |

### Required for AI Features
| Variable | Description |
|----------|-------------|
| `EROSTTS_OpenRouter__ApiKey` | OpenRouter API key (only needed if using `/prompt` command) |

### Optional
| Variable | Default | Description |
|----------|---------|-------------|
| `EROSTTS_ElevenLabs__VoiceId` | `21m00Tcm4TlvDq8ikWAM` | Eleven Labs voice ID (Rachel) |
| `EROSTTS_ElevenLabs__ModelId` | `eleven_multilingual_v2` | Eleven Labs model |
| `EROSTTS_ElevenLabs__Stability` | `0.5` | Voice stability (0.0-1.0) |
| `EROSTTS_ElevenLabs__SimilarityBoost` | `0.75` | Voice similarity boost (0.0-1.0) |
| `EROSTTS_OpenRouter__Model` | `anthropic/claude-3.5-sonnet` | LLM model ID |
| `EROSTTS_OpenRouter__MaxTokens` | `500` | Max response tokens |
| `EROSTTS_OpenRouter__Temperature` | `0.8` | LLM temperature (0.0-2.0) |
| `EROSTTS_OpenRouter__DefaultSystemPrompt` | *(empty)* | Default system prompt prepended to all AI requests |
| `EROSTTS_Discord__EnableTextChannelMonitoring` | `false` | Monitor text channels (requires Message Content Intent) |
| `EROSTTS_Discord__MaxMessageLength` | `500` | Max TTS message length |
| `EROSTTS_Voice__FFmpegPath` | `ffmpeg` | Path to FFmpeg binary |
| `EROSTTS_Database__Provider` | `InMemory` | Database provider: `InMemory`, `Sqlite`, or `Postgres` |
| `EROSTTS_Database__ConnectionString` | `Data Source=data/erostts.db` | Database connection string (ignored for InMemory) |

## Docker

The bot image is published to `ghcr.io/andresmorales07/eros-discord-bot`.

### Docker Compose (GHCR image)

```yaml
services:
  eros-discord-bot:
    container_name: eros-discord-bot
    volumes:
      - data:/app/data
      - logs:/app/logs
    restart: unless-stopped
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "3"
    image: ghcr.io/andresmorales07/eros-discord-bot:latest
    environment:
      - EROSTTS_Discord__Token=${DISCORD_TOKEN}
      - EROSTTS_ElevenLabs__ApiKey=${ELEVENLABS_API_KEY}
      # Required for AI features
      - EROSTTS_OpenRouter__ApiKey=${OPENROUTER_API_KEY}
      # Optional
      - EROSTTS_ElevenLabs__VoiceId=${ELEVENLABS_VOICE_ID:-21m00Tcm4TlvDq8ikWAM}
      - EROSTTS_OpenRouter__Model=${OPENROUTER_MODEL:-anthropic/claude-3.5-sonnet}
      - EROSTTS_OpenRouter__DefaultSystemPrompt=${OPENROUTER_DEFAULT_SYSTEM_PROMPT:-}
      - EROSTTS_Database__Provider=${DATABASE_PROVIDER:-Sqlite}
      - EROSTTS_Database__ConnectionString=${DATABASE_CONNECTION_STRING:-Data Source=data/erostts.db}

volumes:
  logs:
    name: eros-discord-bot-logs
  data:
    name: eros-discord-bot-data
```

### Building from Source

```bash
# Build the image locally
docker build -f docker/Dockerfile -t eros-discord-bot .

# Run with docker-compose (uses local build)
cd docker && docker-compose up -d
```

## Architecture

```
src/ErosTTS.Bot/
├── Commands/              # Slash commands (TtsCommands.cs, CharacterCommands.cs)
├── Configuration/         # Options pattern config classes
├── Data/                  # EF Core persistence layer
│   ├── Converters/        # Discord ID ulong<->long converter
│   ├── Entities/          # EF entity classes
│   ├── Migrations/        # EF Core migrations
│   ├── ErosTtsDbContext.cs
│   └── DesignTimeDbContextFactory.cs
├── Exceptions/            # Custom exception types (TtsExceptions, LlmExceptions)
├── Extensions/            # DI registration extensions (DatabaseServiceExtensions)
├── HostedServices/        # Background services
│   ├── GatewayEventHostedService.cs  # Discord event handlers
│   └── TtsProcessorService.cs        # Queue processor
├── Services/
│   ├── Audio/             # Discord voice playback (AudioService)
│   ├── Character/         # Per-guild character state (in-memory + EF implementations)
│   ├── Guild/             # Per-guild configuration storage (in-memory + EF implementations)
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
- **Entity Framework Core** (SQLite) - Database persistence for guild configuration and character state

## Code Patterns

- **Options pattern**: All configuration uses `IOptions<T>` with sections bound in `Program.cs`
- **Configuration sync**: When modifying `Configuration/*.cs` classes, update `appsettings.example.json` to include any new properties with sensible defaults
- **Hosted services**: Background work via `IHostedService` (queue processing, gateway events)
- **Slash commands**: NetCord's `ApplicationCommandModule<ApplicationCommandContext>` base class
- **DI**: All services registered in `Program.cs` ConfigureServices
- **Persistence**: Config-driven provider selection (`Database:Provider`): `InMemory` (default, ConcurrentDictionary), `Sqlite` (EF Core), or `Postgres` (future). EF services use `IDbContextFactory<T>` to stay singleton-compatible.
- **Async throughout**: Methods return `Task` or `Task<T>`

## Configuration Modes

1. **Slash command only** (default): Uses `/say` command, no privileged intents required
2. **Text channel monitoring**: Set `Discord:EnableTextChannelMonitoring=true`, requires Message Content Intent

## Slash Commands

All commands respond with ephemeral messages (only visible to the user) except `/prompt` which is public.

### TTS Commands (all ephemeral)
- `/say <text> [voice-channel]` - Speak text in voice channel
- `/tts-setup <voice-channel> [text-channel] [voice-id]` - Configure defaults (Manage Guild permission)
- `/tts-stop` - Disconnect from voice
- `/tts-status` - Show current configuration (including voice ID)
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
