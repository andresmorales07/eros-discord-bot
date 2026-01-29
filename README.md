# ErosTTS Discord Bot

A Discord bot that converts text messages to speech using the Eleven Labs API and plays them in voice channels.

## Features

- Monitors specified text channels for messages
- Converts messages to speech using Eleven Labs TTS API
- Plays audio in designated voice channels
- Message queue system for ordered, conflict-free playback
- Slash commands for configuration
- Docker support for easy deployment
- Comprehensive logging with Serilog

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (for local development)
- [FFmpeg](https://ffmpeg.org/download.html) (for audio processing)
- A [Discord Bot Token](https://discord.com/developers/applications)
- An [Eleven Labs API Key](https://elevenlabs.io/)

## Discord Bot Setup

1. Go to the [Discord Developer Portal](https://discord.com/developers/applications)
2. Create a new application
3. Go to the "Bot" section and create a bot
4. Enable the following Privileged Gateway Intents:
   - **Message Content Intent** (required for reading messages)
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
    "ProcessBotMessages": false
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
| `/tts-setup <text-channel> <voice-channel>` | Configure TTS channels | Manage Guild |
| `/tts-stop` | Disconnect from voice | Everyone |
| `/tts-status` | Show current configuration | Everyone |
| `/tts-clear` | Remove TTS configuration | Manage Guild |

## Usage

1. Invite the bot to your Discord server
2. Use `/tts-setup` to configure the text and voice channels:
   ```
   /tts-setup text-channel:#tts-messages voice-channel:General
   ```
3. Send messages in the configured text channel
4. The bot will join the voice channel and read messages aloud

## Architecture

```
ErosTTS.Bot/
├── Configuration/          # Configuration classes
├── Services/
│   ├── Audio/             # Discord voice playback
│   ├── TTS/               # Eleven Labs API client
│   ├── Queue/             # Message queue (System.Threading.Channels)
│   └── Guild/             # Per-guild configuration
├── Handlers/              # Discord event handlers
├── Commands/              # Slash commands
├── HostedServices/        # Background services
└── Exceptions/            # Custom exceptions
```

## Troubleshooting

### Bot doesn't respond to messages
- Ensure Message Content Intent is enabled in the Discord Developer Portal
- Check that the bot has permission to read the text channel
- Verify the channel is configured with `/tts-setup`

### No audio plays
- Ensure FFmpeg is installed and accessible
- Check that the bot has Connect and Speak permissions in the voice channel
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
