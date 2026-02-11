# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview-alpine AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY src/ErosTTS.Bot/ErosTTS.Bot.csproj ./ErosTTS.Bot/
RUN dotnet restore ErosTTS.Bot/ErosTTS.Bot.csproj

# Copy source code and build
COPY src/ErosTTS.Bot/ ./ErosTTS.Bot/
WORKDIR /src/ErosTTS.Bot
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview-alpine AS runtime

# Install FFmpeg for audio conversion (MP3 to PCM) and Opus codec for Discord voice
# Note: opus is required for NetCord voice - the NuGet package only includes Windows binaries
RUN apk add --no-cache \
    ffmpeg \
    icu-libs \
    opus \
    && ln -s /usr/lib/libopus.so.0 /usr/lib/opus.so \
    && ln -s /usr/lib/libopus.so.0 /usr/lib/libopus.so

# Create non-root user for security
RUN addgroup -S erostts && adduser -S erostts -G erostts

# Set working directory
WORKDIR /app

# Copy published application
COPY --from=build /app/publish .

# Create logs and data directories and set ownership
RUN mkdir -p /app/logs /app/data && chown -R erostts:erostts /app

# Switch to non-root user
USER erostts

# Environment variables (override these in production)
ENV DOTNET_ENVIRONMENT=Production
ENV EROSTTS_Discord__Token=""
ENV EROSTTS_ElevenLabs__ApiKey=""
ENV EROSTTS_ElevenLabs__VoiceId="21m00Tcm4TlvDq8ikWAM"
ENV EROSTTS_Voice__FFmpegPath="/usr/bin/ffmpeg"
ENV EROSTTS_OpenAiTts__ApiKey=""
ENV EROSTTS_OpenAiTts__Model="tts-1"
ENV EROSTTS_OpenAiTts__Voice="alloy"
ENV EROSTTS_OpenRouter__ApiKey=""
ENV EROSTTS_OpenRouter__Model="anthropic/claude-3.5-sonnet"
ENV EROSTTS_OpenRouter__DefaultSystemPrompt=""
ENV EROSTTS_Database__Provider="Sqlite"
ENV EROSTTS_Database__ConnectionString="Data Source=data/erostts.db"

# Health check - verify the process is running
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD pgrep -f "dotnet ErosTTS.Bot.dll" || exit 1

# Run the application
ENTRYPOINT ["dotnet", "ErosTTS.Bot.dll"]
