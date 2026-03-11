# CLAUDE.md

## Commands

```bash
# Subir infraestrutura (Postgres + Kafka)
docker-compose up -d

# Run the API
cd src/SseNotificationApi
dotnet restore && dotnet build && dotnet run

# Run tests
cd tests/SseNotificationApi.Tests
dotnet test
```

## Architecture

ASP.NET Core 8 Minimal API — `src/SseNotificationApi`.

### Endpoints

| Method | Path | Notes |
|--------|------|-------|
| `POST` | `/api/users` | Cria ou retorna usuário pelo `username` |
| `GET` | `/api/users` | Lista usuários com status online/offline |
| `POST` | `/api/notifications` | `TargetUserId = null` → broadcast; Guid → usuário específico |
| `GET` | `/api/notifications/stream?userId=` | SSE — `text/event-stream`, long-lived |

Swagger UI: `/swagger`.

### Key Files

| File | Responsibility |
|------|---------------|
| `Data/AppDbContext.cs` | EF Core + PostgreSQL (tabelas criadas via `EnsureCreated`) |
| `Services/NotificationChannel.cs` | In-memory SSE channels (`ConcurrentDictionary<Guid, Channel<string>>`) |
| `Services/KafkaProducerService.cs` | `IKafkaProducerService` — publica mensagens no tópico Kafka |
| `Services/KafkaConsumerService.cs` | `BackgroundService` — consome do Kafka e entrega ao `NotificationChannel` |
| `Models/User.cs` | `Id`, `Username`, `Status` (enum: `Offline=0`, `Online=1`) |
| `Models/Message.cs` | `Id`, `TargetUserId?` (null=broadcast), `Content`, `SentAt` |
| `appsettings.json` | Connection string do Postgres e config do Kafka |
| `wwwroot/index.html` | Frontend estático com login, lista de usuários e envio |

### Infrastructure

| Serviço | Imagem | Porta |
|---------|--------|-------|
| PostgreSQL | `postgres:16` | `5432` |
| Kafka | `confluentinc/cp-kafka:7.6.0` | `9092` |

Credenciais Postgres: `devuser` / `devpass` / DB `ssenotification`.
Tópico Kafka: `ssenotification` (criado automaticamente no startup).

### Test Structure

`tests/SseNotificationApi.Tests` — xUnit + `Microsoft.AspNetCore.Mvc.Testing`

| Path | Type |
|------|------|
| `Unit/NotificationChannelTests.cs` | Testes unitários do `NotificationChannel` |
| `Integration/UsersEndpointTests.cs` | Testes de integração do endpoint `/api/users` |
| `Integration/NotificationsEndpointTests.cs` | Testes de integração do endpoint `/api/notifications` |
| `Infrastructure/WebAppFactory.cs` | `WebApplicationFactory` — substitui Postgres por SQLite in-memory e Kafka por no-op |

### Notification Flow

1. `POST /api/notifications` → valida → salva no DB → checa se usuário está conectado (404 se não) → publica no Kafka
2. `KafkaConsumerService` consome o tópico → chama `NotificationChannel.PublishAsync` / `PublishToAllAsync`
3. `GET /api/notifications/stream?userId=` lê do canal e envia ao cliente via SSE

### SSE Event Format

```
event: connected
data: ...

event: notification
data: {"message":"...","sentAt":"..."}
```