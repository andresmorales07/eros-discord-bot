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
FROM mcr.microsoft.com/dotnet/runtime:10.0-preview-alpine AS runtime

# Install FFmpeg for audio conversion (MP3 to PCM)
# Note: NetCord bundles its own Opus/Sodium native libraries
RUN apk add --no-cache \
    ffmpeg \
    icu-libs

# Create non-root user for security
RUN addgroup -S erostts && adduser -S erostts -G erostts

# Set working directory
WORKDIR /app

# Copy published application
COPY --from=build /app/publish .

# Create logs directory and set ownership
RUN mkdir -p /app/logs && chown -R erostts:erostts /app

# Switch to non-root user
USER erostts

# Environment variables (override these in production)
ENV DOTNET_ENVIRONMENT=Production
ENV EROSTTS_Discord__Token=""
ENV EROSTTS_ElevenLabs__ApiKey=""
ENV EROSTTS_Voice__FFmpegPath="/usr/bin/ffmpeg"

# Health check - verify the process is running
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD pgrep -f "dotnet ErosTTS.Bot.dll" || exit 1

# Run the application
ENTRYPOINT ["dotnet", "ErosTTS.Bot.dll"]
