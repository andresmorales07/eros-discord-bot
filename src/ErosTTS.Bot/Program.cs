using ErosTTS.Bot.Configuration;
using ErosTTS.Bot.Data;
using ErosTTS.Bot.Extensions;
using ErosTTS.Bot.HostedServices;
using ErosTTS.Bot.Services.Audio;
using ErosTTS.Bot.Services.LLM;
using ErosTTS.Bot.Services.Queue;
using ErosTTS.Bot.Services.TTS;
using ErosTTS.Bot.Commands;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services.ApplicationCommands;
using Polly;
using Polly.Extensions.Http;
using Serilog;
using Serilog.Events;

// Configure Serilog before anything else
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("NetCord", LogEventLevel.Information)
    .MinimumLevel.Override("System.Net.Http", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithThreadId()
    .Enrich.WithMachineName()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.Async(a => a.File(
        path: "logs/erostts-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"))
    .CreateLogger();

try
{
    Log.Information("Starting ErosTTS Bot");

    // Build configuration early to determine required gateway intents
    var configuration = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: false)
        .AddEnvironmentVariables("EROSTTS_")
        .Build();

    var enableTextChannelMonitoring = configuration.GetValue<bool>("Discord:EnableTextChannelMonitoring");

    var builder = Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((context, config) =>
        {
            config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                  .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true)
                  .AddEnvironmentVariables("EROSTTS_");
        })
        .UseSerilog()
        .UseDiscordGateway(options =>
        {
            // Base intents required for slash commands and voice
            options.Intents = GatewayIntents.Guilds | GatewayIntents.GuildVoiceStates;

            // Only add message intents if text channel monitoring is enabled
            if (enableTextChannelMonitoring)
            {
                options.Intents |= GatewayIntents.GuildMessages | GatewayIntents.MessageContent;
                Log.Information("Text channel monitoring enabled - requesting GuildMessages and MessageContent intents");
            }
            else
            {
                Log.Information("Slash command mode - minimal intents requested (no MessageContent privilege required)");
            }
        })
        .UseApplicationCommands()
        .ConfigureServices((context, services) =>
        {
            // Bind configuration sections
            services.Configure<BotConfiguration>(
                context.Configuration.GetSection(BotConfiguration.SectionName));
            services.Configure<ElevenLabsConfiguration>(
                context.Configuration.GetSection(ElevenLabsConfiguration.SectionName));
            services.Configure<VoiceConfiguration>(
                context.Configuration.GetSection(VoiceConfiguration.SectionName));
            services.Configure<QueueConfiguration>(
                context.Configuration.GetSection(QueueConfiguration.SectionName));
            services.Configure<OpenRouterConfiguration>(
                context.Configuration.GetSection(OpenRouterConfiguration.SectionName));
            services.Configure<DatabaseConfiguration>(
                context.Configuration.GetSection(DatabaseConfiguration.SectionName));

            // HTTP Client for Eleven Labs with retry policy
            services.AddHttpClient<ITtsService, ElevenLabsTtsService>()
                .AddPolicyHandler(GetRetryPolicy());

            // HTTP Client for OpenRouter with retry policy
            services.AddHttpClient<ILlmService, OpenRouterService>()
                .AddPolicyHandler(GetRetryPolicy());

            // Application Services
            services.AddSingleton<ITtsQueue, TtsQueue>();
            services.AddSingleton<IAudioService, AudioService>();

            // Persistence (guild config + character state) — provider-driven
            services.AddPersistence(context.Configuration);

            // Gateway event handlers (registered as hosted services)
            services.AddHostedService<GatewayEventHostedService>();

            // Hosted Services
            services.AddHostedService<TtsProcessorService>();
        });

    var host = builder.Build();

    // Apply database migrations at startup (only when using a database provider)
    var dbConfig = host.Services.GetService<IOptions<DatabaseConfiguration>>()?.Value;
    if (dbConfig?.Provider.ToLowerInvariant() is "sqlite" or "postgres" or "postgresql")
    {
        // Ensure the directory exists for file-based providers (SQLite)
        if (dbConfig.Provider.Equals("sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var builder2 = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(dbConfig.ConnectionString);
            var dbPath = builder2.DataSource;
            var dbDir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dbDir))
            {
                Directory.CreateDirectory(dbDir);
            }
        }

        var factory = host.Services.GetRequiredService<IDbContextFactory<ErosTtsDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.MigrateAsync();
        Log.Information("Database migrations applied for provider {Provider}", dbConfig.Provider);
    }

    // Validate configuration at startup
    ValidateConfiguration(host.Services);

    // Add slash command modules
    host.AddApplicationCommandModule<TtsCommands>();
    host.AddApplicationCommandModule<CharacterCommands>();

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    Environment.ExitCode = 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt =>
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) +
                TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)),
            onRetry: (outcome, timespan, retryAttempt, context) =>
            {
                Log.Warning(
                    "HTTP request failed with {StatusCode}, retrying in {Delay}s (attempt {Attempt})",
                    outcome.Result?.StatusCode, timespan.TotalSeconds, retryAttempt);
            });
}

static void ValidateConfiguration(IServiceProvider services)
{
    var botConfig = services.GetRequiredService<IOptions<BotConfiguration>>().Value;
    var elevenLabsConfig = services.GetRequiredService<IOptions<ElevenLabsConfiguration>>().Value;

    if (string.IsNullOrWhiteSpace(botConfig.Token))
    {
        throw new InvalidOperationException(
            "Discord bot token is not configured. " +
            "Set the EROSTTS_Discord__Token environment variable or configure it in appsettings.json");
    }

    if (string.IsNullOrWhiteSpace(elevenLabsConfig.ApiKey))
    {
        throw new InvalidOperationException(
            "Eleven Labs API key is not configured. " +
            "Set the EROSTTS_ElevenLabs__ApiKey environment variable or configure it in appsettings.json");
    }

    Log.Information("Configuration validated successfully");
}
