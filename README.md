# ErosTTS Discord Bot

A Discord bot that converts text to speech using the Eleven Labs API and plays it in Discord voice channels.

## Features

- **Slash command TTS** - Use `/say` to speak text in voice channels
- Converts text to speech using Eleven Labs TTS API
- Automatic voice channel detection (joins your current voice channel)
- Message queue system for ordered, conflict-free playback
- Optional text channel monitoring mode (legacy behavior)
- Docker support for easy deployment
- Comprehensive logging with Serilog

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (for local development)
- [FFmpeg](https://ffmpeg.org/download.html) (for audio processing)
- [Opus codec](https://opus-codec.org/) (included via NuGet package)
- A [Discord Bot Token](https://discord.com/developers/applications)
- An [Eleven Labs API Key](https://elevenlabs.io/)

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

| Command | Description | Permission |
|---------|-------------|------------|
| `/say <text> [voice-channel]` | Speak text in a voice channel | Everyone |
| `/tts-setup <voice-channel> [text-channel]` | Configure default voice channel | Manage Guild |
| `/tts-stop` | Disconnect from voice | Everyone |
| `/tts-status` | Show current configuration and mode | Everyone |
| `/tts-clear` | Remove TTS configuration | Manage Guild |

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
│   ├── TTS/               # Eleven Labs API client
│   ├── Queue/             # Message queue (System.Threading.Channels)
│   └── Guild/             # Per-guild configuration
├── Commands/              # Slash commands (/say, /tts-setup, etc.)
├── HostedServices/        # Background services (queue processor, gateway events)
├── Utilities/             # Shared utilities (text sanitization)
└── Exceptions/            # Custom exceptions
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
- Check Discord server region and latency
- Ensure the bot isn't banned from the voice channel

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
