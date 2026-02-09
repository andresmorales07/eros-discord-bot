# Test Coverage Analysis

## Current State

The project has **~239 test methods** across **12 test classes** using xUnit, FluentAssertions, and NSubstitute. The testing infrastructure is well-organized with shared utilities (`FakeHttpMessageHandler`, `EfTestBase`).

### Covered Components

| Component | Test Class | Tests | Coverage Quality |
|-----------|-----------|-------|-----------------|
| `TextSanitizer` | `TextSanitizerTests` | 19 | Excellent - edge cases, regex patterns, unicode |
| `ElevenLabsTtsService` | `ElevenLabsTtsServiceTests` | 22 | Excellent - HTTP mocking, error mapping, headers |
| `CachedTtsService` | `CachedTtsServiceTests` | 10 | Good - cache hit/miss, key computation |
| `OpenRouterService` | `OpenRouterServiceTests` | 18 | Excellent - HTTP mocking, error mapping, request shape |
| `TtsQueue` | `TtsQueueTests` | 11 | Good - FIFO order, concurrency, cancellation |
| `GuildConfigurationService` | `GuildConfigurationServiceTests` | 10 | Good - CRUD, concurrency |
| `EfGuildConfigurationService` | `EfGuildConfigurationServiceTests` | 11 | Good - CRUD with SQLite |
| `NpcService` | `NpcServiceTests` | 55 | Excellent - full CRUD, settings, history, import/export |
| `EfNpcService` | `EfNpcServiceTests` | 55 | Excellent - mirrors in-memory tests with SQLite |
| `NpcSelectionService` | `NpcSelectionServiceTests` | 8 | Good - LLM parsing, fallback |
| `VoiceInactivityHostedService` | `VoiceInactivityHostedServiceTests` | 16 | Excellent - timer lifecycle, multi-guild, polling |
| `ErosTtsDbContext` | `ErosTtsDbContextTests` | 10 | Good - schema, relationships, converters |

---

## Coverage Gaps (Ranked by Impact)

### 1. `TtsProcessorService` — HIGH PRIORITY

**File:** `src/ErosTTS.Bot/HostedServices/TtsProcessorService.cs` (229 lines)

**Why it matters:** This is the core orchestrator — it consumes the TTS queue, calls the TTS API, resolves voice configuration, and plays audio. Its `HandleFailureAsync` method contains branching retry logic for five exception types, each with different backoff strategies. A bug here means silent failures, infinite retry loops, or dropped messages.

**What to test:**
- `ProcessItemAsync` skips items when guild has no voice channel configured
- `ProcessItemAsync` uses NPC `VoiceId` override when present, falls back to guild config `VoiceId`
- `ProcessItemAsync` disposes the audio stream in all cases (success, failure, cancellation)
- `HandleFailureAsync` re-enqueues on `RateLimitException` with incremented retry count (up to `MaxRetries`)
- `HandleFailureAsync` does NOT re-enqueue on `AuthenticationException`
- `HandleFailureAsync` does NOT re-enqueue on `InvalidTextException`
- `HandleFailureAsync` re-enqueues once on `VoiceConnectionException` (retry limit = 1)
- `HandleFailureAsync` applies exponential backoff (`2^(retryCount+1)` seconds) for generic exceptions
- `HandleFailureAsync` stops retrying when `RetryCount >= MaxRetries`
- `StopAsync` calls `_queue.Complete()`

**Testability:** High. All dependencies are injected interfaces (`ITtsQueue`, `ITtsService`, `IAudioService`, `IGuildConfigurationService`). The `GatewayClient` dependency for the Ready-wait logic is harder to mock, but `ProcessItemAsync` and `HandleFailureAsync` can be tested by extracting them or testing through the queue.

**Suggested approach:** Mock all services with NSubstitute. Enqueue items into a real `TtsQueue`, configure mock behaviors to throw specific exceptions, and verify re-enqueue behavior and retry counts.

---

### 2. `GatewayEventHostedService` — MEDIUM PRIORITY

**File:** `src/ErosTTS.Bot/HostedServices/GatewayEventHostedService.cs` (144 lines)

**Why it matters:** This service handles the text-channel monitoring path — when enabled, every guild message flows through `OnMessageCreate`, which sanitizes text, checks guild config, and enqueues TTS items. Bugs here would cause silent message drops or incorrect TTS output.

**What to test:**
- `OnMessageCreate` ignores bot messages when `ProcessBotMessages = false`
- `OnMessageCreate` processes bot messages when `ProcessBotMessages = true`
- `OnMessageCreate` ignores DMs (no `GuildId`)
- `OnMessageCreate` ignores messages from unmonitored channels
- `OnMessageCreate` skips messages when guild has no voice channel configured
- `OnMessageCreate` sanitizes message content through `TextSanitizer`
- `OnMessageCreate` skips messages that are empty after sanitization
- `OnMessageCreate` truncates messages exceeding `MaxMessageLength`
- `OnMessageCreate` creates correct `TtsQueueItem` with username prefix
- `OnMessageCreate` swallows exceptions without propagating

**Testability:** Medium. The `OnMessageCreate` method is private, but you can test it by making the class more testable (extracting the message-processing logic into a separate method or service), or by calling `StartAsync` and then invoking the registered handler.

**Suggested approach:** Extract the message-processing logic into an internal/public method, or test through a thin wrapper that exposes the handler.

---

### 3. `NpcCommands.PromptAsync` — MEDIUM-HIGH PRIORITY

**File:** `src/ErosTTS.Bot/Commands/NpcCommands.cs`, lines 341–463

**Why it matters:** `PromptAsync` is the most complex slash command — it orchestrates NPC selection (manual vs auto-switch), conversation history retrieval, LLM calls, history storage, text sanitization, TTS queuing, and user response formatting. It's the primary user-facing AI feature.

**What to test:**
- Returns error when no voice channel is configured
- Returns error when no NPCs exist
- Uses auto-switch selection when `AutoSwitchEnabled` and `npcs.Count > 1`
- Falls back to active NPC when auto-switch is off
- Falls back to first NPC when no active NPC is set
- Retrieves per-NPC history when `SharedHistory = false`
- Retrieves shared history when `SharedHistory = true`
- Prefixes shared-history assistant messages with `[NpcName]:` for LLM context
- Stores both user and assistant messages in conversation history
- Sanitizes LLM response for TTS
- Uses "I have nothing to say." when sanitized response is empty
- Truncates response to `MaxMessageLength`
- Creates `TtsQueueItem` with NPC's `VoiceId`
- Handles `LlmServiceException` gracefully with error message

**Testability:** Low-Medium. Command classes inherit from NetCord's `ApplicationCommandModule<ApplicationCommandContext>`, which requires a `Context` with `Interaction`, `User`, etc. This would need either a test harness that can set up the context, or refactoring the orchestration logic out of the command class into a testable service.

**Suggested approach:** Extract the prompt orchestration into a dedicated service (e.g., `PromptOrchestrationService`) that takes plain parameters instead of depending on Discord context. The command becomes a thin adapter. This also benefits `GatewayEventHostedService` which has similar message-processing logic.

---

### 4. `TtsCommands.SayAsync` — MEDIUM PRIORITY

**File:** `src/ErosTTS.Bot/Commands/TtsCommands.cs`, lines 51–128

**Why it matters:** This is the primary TTS entry point. The voice channel resolution logic has a three-step fallback (explicit parameter → user's current channel → guild default), and bugs would leave users unable to use the bot.

**What to test:**
- Uses explicit voice channel parameter when provided
- Falls back to user's current voice channel from gateway cache
- Falls back to guild default voice channel from config
- Returns error when no voice channel can be resolved
- Sanitizes and truncates text
- Returns error when text is empty after sanitization
- Creates correct `TtsQueueItem` (no "Username says:" prefix for slash commands)

**Testability:** Same challenge as `NpcCommands` — requires Discord context. The `GetUserVoiceChannel` helper accesses `_gatewayClient.Cache` directly.

---

### 5. `DatabaseServiceExtensions.AddPersistence` — LOW PRIORITY

**File:** `src/ErosTTS.Bot/Extensions/DatabaseServiceExtensions.cs` (52 lines)

**Why it matters:** Incorrect provider selection silently registers the wrong service implementations, leading to data loss (in-memory when SQLite was intended) or startup crashes.

**What to test:**
- `"sqlite"` registers `EfGuildConfigurationService` and `EfNpcService`
- `"inmemory"` registers `GuildConfigurationService` and `NpcService`
- `"postgres"` throws `InvalidOperationException`
- Default (unrecognized provider) falls through to in-memory
- Case-insensitive matching works (`"SQLite"`, `"SQLITE"`, etc.)

**Testability:** High. Build a `ServiceCollection`, call `AddPersistence`, and inspect registered service types.

---

### 6. `AudioService` — LOW PRIORITY (Hard to Test)

**File:** `src/ErosTTS.Bot/Services/Audio/AudioService.cs` (287 lines)

**Why it matters:** Audio playback is the final step in the pipeline. The `GetOrConnectAsync` method has retry logic with 4006-specific error handling, and `PlayAudioAsync` manages FFmpeg process lifecycle.

**What to test (if feasible):**
- `IsConnected` / `GetConnectedGuildIds` — simple state queries on `ConcurrentDictionary`
- `DisconnectAsync` removes client from dictionary and calls `CloseAsync`
- `GetOrConnectAsync` reuses existing connection (double-check pattern)
- `GetOrConnectAsync` retries on 4006 errors with exponential backoff
- `GetOrConnectAsync` throws `VoiceConnectionException` after max retries

**Testability:** Low. Depends on `GatewayClient` (concrete class), `VoiceClient`, `Process` (FFmpeg), and `OpusEncodeStream`. The `GetOrConnectAsync` retry logic is the most valuable to test but requires mocking `GatewayClient.JoinVoiceChannelAsync`.

**Suggested approach:** Extract connection management into a separate class with an interface for the gateway interaction. The FFmpeg/Opus path is inherently integration-level and best covered by an integration test with a real FFmpeg binary.

---

## Structural Recommendations

### 1. Extract Command Logic into Testable Services

The biggest gap is in the command layer. Both `NpcCommands.PromptAsync` and `TtsCommands.SayAsync` contain business logic that's locked behind the Discord context. Consider:

```
// Before: logic in command class
public async Task PromptAsync(string message) {
    // 120 lines of orchestration using Context.Interaction.GuildId, etc.
}

// After: thin command delegates to service
public async Task PromptAsync(string message) {
    var guildId = Context.Interaction.GuildId;
    var result = await _promptService.HandlePromptAsync(guildId, message, Context.User.Id);
    await FollowupAsync(...);
}
```

This would make the NPC selection → LLM call → history storage → TTS queueing pipeline fully testable without any Discord dependencies.

### 2. Extract `TtsProcessorService` Processing Methods

`ProcessItemAsync` and `HandleFailureAsync` are private methods on a `BackgroundService`. Two approaches:
- Make them `internal` and use `[InternalsVisibleTo]` for the test project
- Extract the processing logic into a separate `TtsItemProcessor` service

### 3. Extract `GatewayEventHostedService.OnMessageCreate` Logic

The message-processing logic in `OnMessageCreate` (sanitization, truncation, queue item creation) could be extracted into a `MessageProcessor` service, making it testable independently of Discord gateway events.

### 4. Add Integration Tests

Consider a separate integration test project that:
- Tests the full `TtsProcessorService` pipeline with real `TtsQueue` and mocked external services
- Tests database migrations round-trip (create → migrate → query)
- Tests `DatabaseServiceExtensions` provider selection with real `ServiceCollection`

---

## Suggested Priority Order for New Tests

| Priority | Component | Estimated Tests | Rationale |
|----------|-----------|----------------|-----------|
| P0 | `TtsProcessorService.HandleFailureAsync` | 8-10 | Retry logic is error-prone and critical |
| P0 | `TtsProcessorService.ProcessItemAsync` | 5-7 | Core pipeline, voice override logic |
| P1 | `GatewayEventHostedService.OnMessageCreate` | 8-10 | Message filtering and queuing |
| P1 | `NpcCommands.PromptAsync` (via extracted service) | 10-12 | Most complex user feature |
| P2 | `DatabaseServiceExtensions` | 4-5 | Quick wins, high testability |
| P2 | `TtsCommands.SayAsync` (via extracted service) | 6-8 | Voice channel resolution logic |
| P3 | `AudioService` state management | 3-4 | Connection tracking logic |

Total estimated new tests: **44-56**

This would bring the project from ~239 tests to ~290+ tests and cover the previously untested orchestration and retry logic that represents the highest-risk code.
