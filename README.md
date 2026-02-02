# ErosTTS Discord Bot

A Discord bot that converts text to speech using the Eleven Labs API and plays it in Discord voice channels. Also supports **AI-powered character roleplaying** for D&D sessions and similar scenarios.

## Features

- **Slash command TTS** - Use `/say` to speak text in voice channels
- **AI Character Roleplaying** - Set character context and prompt an AI to respond in character via TTS
- **Privacy-focused** - Bot joins voice channels self-deafened and command responses are ephemeral (except `/prompt`)
- Converts text to speech using Eleven Labs TTS API
- AI responses powered by OpenRouter (supports Claude, GPT, and other models)
- Automatic voice channel detection (joins your current voice channel)
- Message queue system for ordered, conflict-free playback
- Per-guild character state with conversation history
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
    "MaxHistoryMessages": 20
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

1. Copy the example environment file:
   ```bash
   cp docker/.env.example docker/.env
   ```

2. Edit `docker/.env` with your tokens:
   ```env
   DISCORD_TOKEN=your_discord_bot_token
   ELEVENLABS_API_KEY=your_elevenlabs_api_key
   ```

3. Build and run:
   ```bash
   cd docker
   docker-compose up -d
   ```

4. View logs:
   ```bash
   docker-compose logs -f
   ```

### Docker Commands

```bash
# Build the image
docker build -f docker/Dockerfile -t erostts-bot .

# Run directly
docker run -d \
  -e EROSTTS_Discord__Token=your_token \
  -e EROSTTS_ElevenLabs__ApiKey=your_key \
  -v ./logs:/app/logs \
  --name erostts \
  erostts-bot

# Stop
docker stop erostts

# View logs
docker logs -f erostts
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

### AI Character Commands

| Command | Description | Visibility |
|---------|-------------|------------|
| `/character-context <context> [append]` | Set or append character context/system prompt | Ephemeral |
| `/prompt <message>` | Send a prompt to the AI character (response via TTS) | **Public** |
| `/character-clear` | Clear character context and conversation history | Ephemeral |
| `/character-status` | View current character state | Ephemeral |

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

### AI Character Roleplaying

The bot can play characters in D&D sessions or other roleplaying scenarios:

1. Configure a default voice channel with `/tts-setup`:
   ```
   /tts-setup voice-channel:General
   ```

2. Set up the character context (system prompt):
   ```
   /character-context context:You are Gandalf, a wise wizard. Speak in a mystical, cryptic manner. You are helping a group of adventurers on their quest.
   ```

3. Add more context as the story progresses:
   ```
   /character-context context:The party has just defeated a dragon and found a magical artifact. append:true
   ```

4. Prompt the character and hear the response:
   ```
   /prompt message:Gandalf, what should we do with this artifact?
   ```
   The AI will respond in character, and the response will be spoken via TTS in the voice channel.

5. When switching characters, clear the state:
   ```
   /character-clear
   ```

**Note:** The `/prompt` command response is visible to everyone in the channel, showing both the user's message and the character's response.

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
├── Configuration/          # Configuration classes
├── Services/
│   ├── Audio/             # Discord voice playback
│   ├── Character/         # Per-guild AI character state (context, conversation history)
│   ├── Guild/             # Per-guild TTS configuration
│   ├── LLM/               # OpenRouter API client for AI responses
│   ├── Queue/             # Message queue (System.Threading.Channels)
│   └── TTS/               # Eleven Labs API client
├── Commands/              # Slash commands (TtsCommands, CharacterCommands)
├── HostedServices/        # Background services (queue processor, gateway events)
├── Utilities/             # Shared utilities (text sanitization)
└── Exceptions/            # Custom exceptions (TTS, LLM)
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

### AI character not responding
- Ensure `OpenRouter:ApiKey` is configured
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
