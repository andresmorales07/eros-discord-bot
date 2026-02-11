# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ErosTTS is a Discord bot that converts text to speech using multiple TTS providers (Eleven Labs and OpenAI) and plays audio in Discord voice channels. Built with .NET 10, it uses NetCord for Discord integration and supports both slash commands and optional text channel monitoring.

The bot also supports **AI-powered multi-NPC roleplaying** using OpenRouter API, allowing it to play multiple named characters (NPCs) in D&D sessions or similar roleplaying scenarios. Each NPC has its own personality, optional voice, and conversation history.

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
| `EROSTTS_ElevenLabs__ApiKey` | Eleven Labs API key (default TTS provider) |

### Required for AI Features
| Variable | Description |
|----------|-------------|
| `EROSTTS_OpenRouter__ApiKey` | OpenRouter API key (only needed if using `/prompt` command) |

### Optional - TTS Providers
| Variable | Default | Description |
|----------|---------|-------------|
| `EROSTTS_OpenAiTts__ApiKey` | *(empty)* | OpenAI API key (enables OpenAI TTS provider, optional alternative to ElevenLabs) |
| `EROSTTS_OpenAiTts__Model` | `tts-1` | OpenAI TTS model (tts-1 or tts-1-hd) |
| `EROSTTS_OpenAiTts__Voice` | `alloy` | OpenAI voice (alloy, echo, fable, onyx, nova, shimmer) |
| `EROSTTS_OpenAiTts__OutputFormat` | `mp3` | OpenAI output format (mp3, opus, aac, flac, wav, pcm) |
| `EROSTTS_OpenAiTts__Speed` | `1.0` | Speech speed multiplier (0.25-4.0) |
| `EROSTTS_OpenAiTts__TimeoutSeconds` | `30` | Request timeout in seconds |

### Optional - General
| Variable | Default | Description |
|----------|---------|-------------|
| `EROSTTS_ElevenLabs__VoiceId` | `21m00Tcm4TlvDq8ikWAM` | Eleven Labs voice ID (Rachel) |
| `EROSTTS_ElevenLabs__ModelId` | `eleven_turbo_v2_5` | Eleven Labs model (faster, cheaper, English-only) |
| `EROSTTS_ElevenLabs__OutputFormat` | `mp3_22050_32` | Audio output format (lower quality sufficient for Discord voice, re-encodes to Opus) |
| `EROSTTS_ElevenLabs__Stability` | `0.5` | Voice stability (0.0-1.0) |
| `EROSTTS_ElevenLabs__SimilarityBoost` | `0.75` | Voice similarity boost (0.0-1.0) |
| `EROSTTS_TtsCache__Enabled` | `true` | Enable TTS audio caching to disk |
| `EROSTTS_TtsCache__CacheDirectory` | `data/tts-cache` | Directory for cached TTS audio files |
| `EROSTTS_OpenRouter__Model` | `anthropic/claude-3.5-sonnet` | LLM model ID |
| `EROSTTS_OpenRouter__MaxTokens` | `500` | Max response tokens |
| `EROSTTS_OpenRouter__Temperature` | `0.8` | LLM temperature (0.0-2.0) |
| `EROSTTS_OpenRouter__DefaultSystemPrompt` | *(empty)* | Default system prompt prepended to all AI requests |
| `EROSTTS_Discord__EnableTextChannelMonitoring` | `false` | Monitor text channels (requires Message Content Intent) |
| `EROSTTS_Discord__MaxMessageLength` | `500` | Max TTS message length |
| `EROSTTS_Voice__FFmpegPath` | `ffmpeg` | Path to FFmpeg binary |
| `EROSTTS_Npc__MaxNpcsPerGuild` | `20` | Maximum NPCs per guild |
| `EROSTTS_Npc__MaxHistoryMessages` | `50` | Maximum conversation history messages |
| `EROSTTS_Npc__AutoSwitchContextMessages` | `5` | Recent messages for auto-switch context |
| `EROSTTS_Database__Provider` | `InMemory` | Database provider: `InMemory`, `Sqlite`, or `Postgres` |
| `EROSTTS_Database__ConnectionString` | `Data Source=data/erostts.db` | Database connection string (ignored for InMemory) |

## Docker

The bot image is published to `ghcr.io/andresmorales07/eros-discord-bot`.

Two Docker Compose files are available:
- **`docker-compose.yml`** (root) - For deploying the pre-built GHCR image
- **`docker/docker-compose.yml`** - For building and running from local source

### Docker Compose (GHCR image)

Use the root-level `docker-compose.yml` file to run the published image

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
      # Optional - TTS Providers
      - EROSTTS_ElevenLabs__VoiceId=${ELEVENLABS_VOICE_ID:-21m00Tcm4TlvDq8ikWAM}
      - EROSTTS_OpenAiTts__ApiKey=${OPENAI_TTS_API_KEY:-}
      - EROSTTS_OpenAiTts__Model=${OPENAI_TTS_MODEL:-tts-1}
      - EROSTTS_OpenAiTts__Voice=${OPENAI_TTS_VOICE:-alloy}
      # Optional - AI and Database
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

Use `docker/docker-compose.yml` to build and run from local source:

```bash
# Build the image locally
docker build -f docker/Dockerfile -t eros-discord-bot .

# Run with docker-compose (uses local build from docker/docker-compose.yml)
cd docker && docker-compose up -d
```

## Architecture

```
src/ErosTTS.Bot/
├── Commands/              # Slash commands (TtsCommands.cs, NpcCommands.cs)
├── Configuration/         # Options pattern config classes
├── Data/                  # EF Core persistence layer
│   ├── Converters/        # Discord ID ulong<->long converter
│   ├── Entities/          # EF entity classes (NpcEntity, GuildNpcSettingsEntity, etc.)
│   ├── Migrations/        # EF Core migrations
│   ├── ErosTtsDbContext.cs
│   └── DesignTimeDbContextFactory.cs
├── Exceptions/            # Custom exception types (TtsExceptions, LlmExceptions)
├── Extensions/            # DI registration extensions (DatabaseServiceExtensions)
├── HostedServices/        # Background services
│   ├── GatewayEventHostedService.cs     # Discord event handlers
│   ├── TtsProcessorService.cs           # Queue processor
│   └── VoiceInactivityHostedService.cs  # Auto-disconnect from empty voice channels
├── Services/
│   ├── Audio/             # Discord voice playback (AudioService, VoiceChannelInspector)
│   ├── Guild/             # Per-guild configuration storage (in-memory + EF implementations)
│   ├── LLM/               # OpenRouter API client + ConversationMessage DTO
│   ├── Npc/               # Multi-NPC system (INpcService, INpcSelectionService, domain records)
│   ├── Queue/             # TTS message queue (System.Threading.Channels)
│   ├── TTS/               # Multi-provider TTS (ITtsProvider, ITtsProviderFactory, ElevenLabsTtsService, OpenAiTtsService, CachedTtsService decorator)
│   ├── MessageProcessingService.cs      # Message processing logic (sanitization, truncation, queue item creation)
│   ├── PromptOrchestrationService.cs    # NPC prompt pipeline (selection, LLM call, history, TTS queueing)
│   └── VoiceChannelResolverService.cs   # Three-step voice channel resolution (explicit → user → default)
├── Utilities/             # Text sanitization utilities
└── Program.cs             # Host builder and DI configuration
```

## Key Technologies

- **NetCord** (v1.0.0-alpha.460) - Discord library for .NET
- **Eleven Labs API** - Text-to-speech synthesis (default provider)
- **OpenAI API** - Alternative text-to-speech synthesis provider
- **OpenRouter API** - LLM access for AI character responses
- **Polly** - HTTP retry policies with exponential backoff
- **Serilog** - Structured logging to console and rolling files
- **System.Threading.Channels** - Async message queue
- **OpusDotNet** - Audio codec for Discord voice
- **Entity Framework Core** (SQLite) - Database persistence for guild configuration and NPC state

## Agents

- **docs-updater**: After making code changes that affect configuration, slash commands, project structure, dependencies, or code patterns, run the `docs-updater` agent to keep CLAUDE.md and other documentation in sync.
- **dockerfile-verifier**: After making changes to `docker/Dockerfile`, `docker/docker-compose.yml`, configuration classes, environment variables, or runtime dependencies, run the `dockerfile-verifier` agent to verify Docker files stay correct and consistent.
- **test-runner**: After making code changes, run the `test-runner` agent to execute the unit test suite and report results.
- **code-simplifier**: After feature additions or refactors, run the `code-simplifier` agent (built-in `code-simplifier:code-simplifier` subagent type) to review recently modified code for unnecessary complexity, redundant patterns, or over-engineering while preserving all functionality.

## Code Patterns

- **Options pattern**: All configuration uses `IOptions<T>` with sections bound in `Program.cs`
- **Configuration sync**: When modifying `Configuration/*.cs` classes, update `appsettings.example.json` to include any new properties with sensible defaults
- **Hosted services**: Background work via `IHostedService` (queue processing, gateway events)
- **Slash commands**: NetCord's `ApplicationCommandModule<ApplicationCommandContext>` base class
- **DI**: All services registered in `Program.cs` ConfigureServices
- **Persistence**: Config-driven provider selection (`Database:Provider`): `InMemory` (default, ConcurrentDictionary), `Sqlite` (EF Core), or `Postgres` (future). EF services use `IDbContextFactory<T>` to stay singleton-compatible.
- **Multi-provider TTS**: `ITtsProvider` interface extends `ITtsService` with provider metadata (ProviderName, DefaultVoiceId, ModelId, OutputFormat). `ITtsProviderFactory` resolves guild-specific provider selection. Currently supports ElevenLabs (default) and OpenAI. OpenAI provider is conditionally registered only if `OpenAiTts:ApiKey` is configured.
- **Decorator pattern**: `CachedTtsService` decorates `ITtsProvider` implementations to provide transparent disk caching of TTS audio. Cache keys use SHA256 hash of text+voiceId+modelId+outputFormat. Enabled by default via `TtsCache:Enabled` configuration. Decorator is provider-agnostic.
- **Service extraction for testability**: Complex logic extracted from commands and hosted services into dedicated service interfaces (`IMessageProcessingService`, `IPromptOrchestrationService`, `IVoiceChannelResolverService`) to enable unit testing without Discord dependencies.
- **Async throughout**: Methods return `Task` or `Task<T>`

## Configuration Modes

1. **Slash command only** (default): Uses `/say` command, no privileged intents required
2. **Text channel monitoring**: Set `Discord:EnableTextChannelMonitoring=true`, requires Message Content Intent

## Slash Commands

All commands respond with ephemeral messages (only visible to the user) except `/prompt` which is public.

### TTS Commands (all ephemeral)
- `/say <text> [voice-channel]` - Speak text in voice channel
- `/tts-config [voice-channel] [text-channel] [voice-id] [provider]` - Configure TTS settings; at least one option required (Manage Guild permission)
- `/tts-stop` - Disconnect from voice
- `/tts-status` - Show current configuration (including voice ID and TTS provider)
- `/tts-clear` - Remove configuration (Manage Guild permission)

### NPC Commands (all ephemeral except `/prompt`)
- `/npc-create <name> <personality> [voice-id]` - Create a new NPC
- `/npc-edit <name> [new-name] [personality] [voice-id] [clear-voice]` - Edit NPC fields
- `/npc-delete <name>` - Delete NPC + its history
- `/npc-list` - List all NPCs with preview
- `/npc-select <name>` - Set active NPC
- `/npc-auto-switch` - Toggle LLM-driven NPC selection on/off
- `/npc-history-mode <shared>` - Toggle shared/per-NPC history (clears history on change)
- `/npc-clear-history [name]` - Clear history (one NPC or all)
- `/npc-status` - Show settings, active NPC, counts
- `/npc-import <json>` - Import NPCs from JSON
- `/npc-export` - Export all NPCs as JSON code block
- `/prompt <message>` - Send prompt; auto-switch or active NPC responds; TTS with NPC voice (**public**)

## Voice Channel Behavior

- Bot joins voice channels **self-deafened** by default (doesn't listen to voice chat)
- Uses `UpdateVoiceStateAsync` with `SelfDeaf = true` after connecting
- **Auto-disconnect**: Bot automatically disconnects after 60 seconds when alone in a voice channel (no human users present)
  - Monitored by `VoiceInactivityHostedService` listening to voice state updates
  - Timer is cancelled if users rejoin before the delay expires

## External Dependencies

- **FFmpeg** - Required for audio processing, must be in PATH or configured via `Voice:FFmpegPath`
- **Opus codec** - Included via NuGet package `OpusDotNet.opus.win-x64`
