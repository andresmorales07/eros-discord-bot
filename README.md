# ErosTTS Discord Bot

A Discord bot that converts text to speech using the Eleven Labs API and plays it in Discord voice channels. Also supports **AI-powered multi-NPC roleplaying** for D&D sessions and similar scenarios.

## Features

- **Slash command TTS** - Use `/say` to speak text in voice channels
- **Multi-NPC Roleplaying** - Create multiple named NPCs with individual personalities, voices, and conversation histories
- **Auto-switch** - LLM-driven NPC selection automatically picks the best character to respond
- **Per-NPC voices** - Each NPC can have its own ElevenLabs voice ID
- **Import/Export** - Share NPC configurations between guilds via JSON
- **Privacy-focused** - Bot joins voice channels self-deafened and command responses are ephemeral (except `/prompt`)
- Converts text to speech using Eleven Labs TTS API
- AI responses powered by OpenRouter (supports Claude, GPT, and other models)
- Automatic voice channel detection (joins your current voice channel)
- Message queue system for ordered, conflict-free playback
- Shared or per-NPC conversation history modes
- Optional text channel monitoring mode (legacy behavior)
- Docker support for easy deployment
- Comprehensive logging with Serilog

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (for local development)
- [FFmpeg](https://ffmpeg.org/download.html) (for audio processing)
- [Opus codec](https://opus-codec.org/) (included via NuGet package)
- A [Discord Bot Token](https://discord.com/developers/applications)
- An [Eleven Labs API Key](https://elevenlabs.io/)
- An [OpenRouter API Key](https://openrouter.ai/) (optional, for AI character features)

## Discord Bot Setup

1. Go to the [Discord Developer Portal](https://discord.com/developers/applications)
2. Create a new application
3. Go to the "Bot" section and create a bot
4. **Privileged Gateway Intents** (only needed if using text channel monitoring mode):
   - **Message Content Intent** - Only required if `EnableTextChannelMonitoring` is set to `true`
   - For slash-command-only mode, no privileged intents are needed
5. Copy the bot token for configuration
6. Go to OAuth2 > URL Generator and select:
   - Scopes: `bot`, `applications.commands`
   - Bot Permissions: `Connect`, `Speak`, `Send Messages`, `Read Message History`
7. Use the generated URL to invite the bot to your server

## Configuration

### Environment Variables

| Variable | Description | Required |
|----------|-------------|----------|
| `EROSTTS_Discord__Token` | Discord bot token | Yes |
| `EROSTTS_ElevenLabs__ApiKey` | Eleven Labs API key | Yes |
| `EROSTTS_ElevenLabs__VoiceId` | Voice ID to use | No (defaults to Rachel) |
| `EROSTTS_Voice__FFmpegPath` | Path to FFmpeg executable | No (defaults to `ffmpeg`) |
| `EROSTTS_OpenRouter__ApiKey` | OpenRouter API key for AI features | No (AI features disabled if not set) |
| `EROSTTS_OpenRouter__Model` | LLM model to use | No (defaults to `anthropic/claude-3.5-sonnet`) |

### appsettings.json

```json
{
  "Discord": {
    "Token": "your_token_here",
    "MaxMessageLength": 500,
    "ProcessBotMessages": false,
    "EnableTextChannelMonitoring": false
  },
  "ElevenLabs": {
    "ApiKey": "your_api_key_here",
    "VoiceId": "21m00Tcm4TlvDq8ikWAM",
    "ModelId": "eleven_multilingual_v2",
    "Stability": 0.5,
    "SimilarityBoost": 0.75
  },
  "Voice": {
    "FFmpegPath": "ffmpeg",
    "BitRate": 128
  },
  "Queue": {
    "Capacity": 100
  },
  "OpenRouter": {
    "ApiKey": "your_openrouter_api_key",
    "Model": "anthropic/claude-3.5-sonnet",
    "MaxTokens": 500,
    "Temperature": 0.8,
    "TimeoutSeconds": 60,
    "MaxHistoryMessages": 20,
    "DefaultSystemPrompt": "Keep responses concise (under 2 sentences). Respond in character."
  },
  "Npc": {
    "MaxNpcsPerGuild": 20,
    "MaxHistoryMessages": 50,
    "AutoSwitchContextMessages": 5
  },
  "Database": {
    "Provider": "InMemory",
    "ConnectionString": "Data Source=data/erostts.db"
  }
}
```

### Configuration Options

| Option | Description | Default |
|--------|-------------|---------|
| `Discord:EnableTextChannelMonitoring` | Enable legacy text channel monitoring mode | `false` |
| `Discord:MaxMessageLength` | Maximum characters for TTS | `500` |
| `Discord:ProcessBotMessages` | Process messages from other bots | `false` |
| `Voice:FFmpegPath` | Path to FFmpeg executable | `ffmpeg` |
| `OpenRouter:Model` | LLM model ID (see [OpenRouter models](https://openrouter.ai/models)) | `anthropic/claude-3.5-sonnet` |
| `OpenRouter:MaxTokens` | Maximum tokens in AI response | `500` |
| `OpenRouter:Temperature` | Response randomness (0.0-2.0) | `0.8` |
| `OpenRouter:MaxHistoryMessages` | Conversation history limit | `20` |
| `OpenRouter:DefaultSystemPrompt` | Default system prompt prepended to all AI requests | *(empty)* |
| `Npc:MaxNpcsPerGuild` | Maximum NPCs allowed per guild | `20` |
| `Npc:MaxHistoryMessages` | Max conversation history messages per NPC/guild | `50` |
| `Npc:AutoSwitchContextMessages` | Recent messages included for auto-switch context | `5` |
| `Database:Provider` | Database provider: `InMemory`, `Sqlite`, or `Postgres` | `InMemory` |
| `Database:ConnectionString` | Database connection string (ignored for InMemory) | `Data Source=data/erostts.db` |

## Running Locally

1. Clone the repository:
   ```bash
   git clone <repository-url>
   cd eros-discord-bot
   ```

2. Set environment variables:
   ```bash
   # Windows PowerShell
   $env:EROSTTS_Discord__Token = "your_discord_token"
   $env:EROSTTS_ElevenLabs__ApiKey = "your_elevenlabs_api_key"

   # Linux/macOS
   export EROSTTS_Discord__Token="your_discord_token"
   export EROSTTS_ElevenLabs__ApiKey="your_elevenlabs_api_key"
   ```

3. Install FFmpeg:
   - **Windows**: Download from [ffmpeg.org](https://ffmpeg.org/download.html) or use `winget install ffmpeg`
   - **macOS**: `brew install ffmpeg`
   - **Linux**: `apt install ffmpeg` or equivalent

4. Run the bot:
   ```bash
   cd src/ErosTTS.Bot
   dotnet run
   ```

## Running with Docker

The bot image is published to GitHub Container Registry at `ghcr.io/andresmorales07/eros-discord-bot`.

### Docker Compose (Recommended)

1. Create a `docker-compose.yml`:

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

2. Create a `.env` file in the same directory:

   ```env
   # Required
   DISCORD_TOKEN=your_discord_bot_token
   ELEVENLABS_API_KEY=your_elevenlabs_api_key

   # Required for AI features (optional if not using /prompt)
   OPENROUTER_API_KEY=your_openrouter_api_key

   # Optional overrides
   # ELEVENLABS_VOICE_ID=21m00Tcm4TlvDq8ikWAM
   # OPENROUTER_MODEL=anthropic/claude-3.5-sonnet
   # OPENROUTER_DEFAULT_SYSTEM_PROMPT=Keep responses concise. Respond in character.
   # DATABASE_PROVIDER=Sqlite
   # DATABASE_CONNECTION_STRING=Data Source=data/erostts.db
   ```

3. Run:
   ```bash
   docker compose up -d
   ```

4. View logs:
   ```bash
   docker compose logs -f
   ```

### Docker Run

```bash
docker run -d \
  --name eros-discord-bot \
  -e EROSTTS_Discord__Token=your_discord_token \
  -e EROSTTS_ElevenLabs__ApiKey=your_elevenlabs_key \
  -v erostts-data:/app/data \
  -v erostts-logs:/app/logs \
  ghcr.io/andresmorales07/eros-discord-bot:latest
```

### Building from Source

```bash
# Build the image locally
docker build -f docker/Dockerfile -t eros-discord-bot .

# Run the locally built image
docker run -d \
  --name eros-discord-bot \
  -e EROSTTS_Discord__Token=your_discord_token \
  -e EROSTTS_ElevenLabs__ApiKey=your_elevenlabs_key \
  -v erostts-data:/app/data \
  -v erostts-logs:/app/logs \
  eros-discord-bot
```

## Slash Commands

All command responses are **ephemeral** (only visible to the user who ran the command) except `/prompt` which shows the conversation publicly.

### TTS Commands

| Command | Description | Permission | Visibility |
|---------|-------------|------------|------------|
| `/say <text> [voice-channel]` | Speak text in a voice channel | Everyone | Ephemeral |
| `/tts-setup <voice-channel> [text-channel] [voice-id]` | Configure default voice channel and custom voice | Manage Guild | Ephemeral |
| `/tts-stop` | Disconnect from voice | Everyone | Ephemeral |
| `/tts-status` | Show current configuration, mode, and voice ID | Everyone | Ephemeral |
| `/tts-clear` | Remove TTS configuration | Manage Guild | Ephemeral |

### NPC Commands

| Command | Description | Visibility |
|---------|-------------|------------|
| `/npc-create <name> <personality> [voice-id]` | Create a new NPC | Ephemeral |
| `/npc-edit <name> [new-name] [personality] [voice-id] [clear-voice]` | Edit an existing NPC | Ephemeral |
| `/npc-delete <name>` | Delete an NPC and its history | Ephemeral |
| `/npc-list` | List all NPCs in the guild | Ephemeral |
| `/npc-select <name>` | Set the active NPC | Ephemeral |
| `/npc-auto-switch` | Toggle LLM-driven NPC selection | Ephemeral |
| `/npc-history-mode <shared>` | Toggle shared/per-NPC history (clears history) | Ephemeral |
| `/npc-clear-history [name]` | Clear history (one NPC or all) | Ephemeral |
| `/npc-status` | Show NPC settings and counts | Ephemeral |
| `/npc-import <json>` | Import NPCs from JSON | Ephemeral |
| `/npc-export` | Export all NPCs as JSON | Ephemeral |
| `/prompt <message>` | Send prompt to AI NPC; response played via TTS | **Public** |

## Usage

### Basic Usage (Slash Commands)

1. Invite the bot to your Discord server
2. Join a voice channel
3. Use `/say` to speak text:
   ```
   /say text:Hello, this is a test!
   ```
4. The bot will join your voice channel and speak the text

You can also specify a different voice channel:
```
/say text:Hello everyone! voice-channel:General
```

### Custom Voice per Server

Each Discord server can use a different ElevenLabs voice. Use `/tts-setup` with the `voice-id` parameter:

```
/tts-setup voice-channel:General voice-id:EXAVITQu4vr4xnSDxMaL
```

You can find voice IDs in your [ElevenLabs Voice Library](https://elevenlabs.io/voice-library). If no `voice-id` is specified, the default voice from your configuration is used.

### Multi-NPC Roleplaying

The bot supports multiple named NPCs per guild, each with their own personality, voice, and conversation history — perfect for D&D sessions:

1. Configure a default voice channel with `/tts-setup`:
   ```
   /tts-setup voice-channel:General
   ```

2. Create NPCs with unique personalities and optional voice IDs:
   ```
   /npc-create name:Gandalf personality:You are Gandalf the Grey, a wise and cryptic wizard. voice-id:EXAVITQu4vr4xnSDxMaL
   /npc-create name:Saruman personality:You are Saruman the White, a cunning and power-hungry wizard.
   ```

3. Select the active NPC or enable auto-switch:
   ```
   /npc-select name:Gandalf
   ```
   Or let the LLM choose the best NPC automatically:
   ```
   /npc-auto-switch
   ```

4. Prompt the NPC and hear the response (each NPC uses its own voice):
   ```
   /prompt message:Gandalf, what should we do with this artifact?
   ```

5. Switch between shared and per-NPC conversation history:
   ```
   /npc-history-mode shared:true
   ```

6. Export NPCs to share with other guilds:
   ```
   /npc-export
   ```

**Note:** The `/prompt` command response is visible to everyone in the channel, showing the user's message and the NPC's response.

### Optional: Text Channel Monitoring Mode

If you want the bot to automatically read messages from a text channel (legacy behavior):

1. Set `EnableTextChannelMonitoring` to `true` in your configuration
2. Enable the **Message Content Intent** in the Discord Developer Portal
3. Use `/tts-setup` to configure channels:
   ```
   /tts-setup voice-channel:General text-channel:#tts-messages
   ```
4. Messages sent in the configured text channel will be read aloud automatically

## Architecture

```
ErosTTS.Bot/
├── Commands/              # Slash commands (TtsCommands, NpcCommands)
├── Configuration/         # Options pattern config classes
├── Data/                  # EF Core persistence layer
│   ├── Converters/        # Discord ID ulong<->long converter
│   ├── Entities/          # EF entity classes (NpcEntity, GuildNpcSettingsEntity, etc.)
│   ├── Migrations/        # EF Core migrations
│   ├── ErosTtsDbContext.cs
│   └── DesignTimeDbContextFactory.cs
├── Exceptions/            # Custom exception types (TTS, LLM)
├── Extensions/            # DI registration extensions
├── HostedServices/        # Background services (queue processor, gateway events)
├── Services/
│   ├── Audio/             # Discord voice playback
│   ├── Guild/             # Per-guild TTS configuration (in-memory + EF implementations)
│   ├── LLM/               # OpenRouter API client + ConversationMessage DTO
│   ├── Npc/               # Multi-NPC system (CRUD, auto-switch, history, import/export)
│   ├── Queue/             # Message queue (System.Threading.Channels)
│   └── TTS/               # Eleven Labs API client
├── Utilities/             # Text sanitization utilities
└── Program.cs             # Host builder and DI configuration
```

## Troubleshooting

### Bot doesn't respond to `/say` command
- Ensure the bot has been invited with `applications.commands` scope
- Try re-inviting the bot to refresh slash commands
- Check that you're in a voice channel (or specify one with the `voice-channel` parameter)

### Bot doesn't respond to messages (text channel monitoring mode)
- Ensure `EnableTextChannelMonitoring` is set to `true` in configuration
- Ensure Message Content Intent is enabled in the Discord Developer Portal
- Check that the bot has permission to read the text channel
- Verify the channel is configured with `/tts-setup`

### No audio plays
- Ensure FFmpeg is installed and accessible (or set `Voice:FFmpegPath` to the full path)
- Check that the bot has Connect and Speak permissions in the voice channel
- Verify the Opus codec is available (included via `OpusDotNet.opus.win-x64` NuGet package)
- Review logs for TTS API errors

### Rate limit errors
- The bot implements exponential backoff for rate limits
- Consider upgrading your Eleven Labs plan for higher limits
- Reduce message frequency in the monitored channel

### Voice connection issues
- The bot will automatically reconnect on disconnect
- The bot joins voice channels **self-deafened** (this is intentional - it doesn't need to hear voice chat)
- Check Discord server region and latency
- Ensure the bot isn't banned from the voice channel

### NPC not responding
- Ensure `OpenRouter:ApiKey` is configured
- Ensure at least one NPC has been created with `/npc-create`
- Check that a voice channel is configured with `/tts-setup`
- Review logs for API errors (rate limits, authentication failures)
- Verify your OpenRouter account has credits available

## Logging

Logs are written to:
- Console (real-time)
- `logs/erostts-YYYYMMDD.log` (daily rotation, 30 days retention)

Log levels can be configured in `appsettings.json`:
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Discord": "Warning"
      }
    }
  }
}
```

## License

MIT License - See [LICENSE](LICENSE) for details.
