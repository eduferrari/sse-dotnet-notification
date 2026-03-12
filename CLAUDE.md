# SSE Notification API

ASP.NET Core 8 Minimal API with PostgreSQL and Kafka.

## Commands

```bash
# Infrastructure (Postgres + Kafka)
docker-compose up -d

# Run API
dotnet run --project src/SseNotificationApi

# Run tests
dotnet test tests/SseNotificationApi.Tests
```

## Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/api/users` | Create or return user by `username` |
| `GET` | `/api/users` | List users with online/offline status |
| `POST` | `/api/notifications` | `TargetUserId = null` → broadcast; Guid → specific user |
| `GET` | `/api/notifications/stream?userId=` | SSE stream (`text/event-stream`) |

Swagger UI: `/swagger`

## Key Files

| File | Responsibility |
|------|---------------|
| `src/SseNotificationApi/Program.cs` | Endpoints + DI wiring |
| `Data/AppDbContext.cs` | EF Core + PostgreSQL (`EnsureCreated`) |
| `Services/NotificationChannel.cs` | In-memory SSE channels (`ConcurrentDictionary<Guid, Channel<string>>`) |
| `Services/KafkaProducerService.cs` | `IKafkaProducerService` — publishes to Kafka |
| `Services/KafkaConsumerService.cs` | `BackgroundService` — consumes Kafka → `NotificationChannel` |
| `Models/User.cs` | `Id`, `Username`, `Status` (`Offline=0`, `Online=1`) |
| `Models/Message.cs` | `Id`, `TargetUserId?` (null=broadcast), `Content`, `SentAt` |
| `wwwroot/index.html` | Static frontend (login, user list, send) |

## Infrastructure

| Service | Image | Port |
|---------|-------|------|
| PostgreSQL | `postgres:16` | `5432` |
| Kafka | `confluentinc/cp-kafka:7.6.0` | `9092` |

Postgres: `devuser` / `devpass` / DB `ssenotification`
Kafka topic: `ssenotification` (auto-created on startup)

## Notification Flow

1. `POST /api/notifications` → validate → save to DB → check `HasClient` (404 if disconnected) → publish to Kafka
2. `KafkaConsumerService` consumes topic → `NotificationChannel.PublishAsync` / `PublishToAllAsync`
3. `GET /api/notifications/stream?userId=` reads from channel → sends SSE events

## SSE Event Format

```
event: connected
data: ...

event: notification
data: {"message":"...","sentAt":"..."}
```

## Tests

`tests/SseNotificationApi.Tests` — xUnit + `Microsoft.AspNetCore.Mvc.Testing`

| Path | Type |
|------|------|
| `Unit/NotificationChannelTests.cs` | Unit tests for `NotificationChannel` |
| `Integration/UsersEndpointTests.cs` | Integration tests for `/api/users` |
| `Integration/NotificationsEndpointTests.cs` | Integration tests for `/api/notifications` |
| `Infrastructure/WebAppFactory.cs` | Replaces Postgres with SQLite in-memory, Kafka with no-op |