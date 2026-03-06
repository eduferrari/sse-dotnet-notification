# CLAUDE.md

## Commands

```bash
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
| `Data/AppDbContext.cs` | EF Core + SQLite (`notifications.db`, criado via `EnsureCreated`) |
| `Services/NotificationChannel.cs` | In-memory SSE channels (`ConcurrentDictionary<Guid, Channel<string>>`) |
| `Models/User.cs` | `Id`, `Username`, `Status` (enum: `Offline=0`, `Online=1`) |
| `Models/Message.cs` | `Id`, `TargetUserId?` (null=broadcast), `Content`, `SentAt` |
| `wwwroot/index.html` | Frontend estático com login, lista de usuários e envio |

### Test Structure

`tests/SseNotificationApi.Tests` — xUnit + `Microsoft.AspNetCore.Mvc.Testing`

| Path | Type |
|------|------|
| `Unit/NotificationChannelTests.cs` | Testes unitários do `NotificationChannel` |
| `Integration/UsersEndpointTests.cs` | Testes de integração do endpoint `/api/users` |
| `Integration/NotificationsEndpointTests.cs` | Testes de integração do endpoint `/api/notifications` |
| `Infrastructure/WebAppFactory.cs` | `WebApplicationFactory` compartilhada pelos testes de integração |

### SSE Flow

1. `POST /api/users` → obtém `userId`
2. `GET /api/notifications/stream?userId=` → marca usuário `Online`, abre canal
3. Na desconexão (`finally`) → marca usuário `Offline`, remove canal
4. `POST /api/notifications` → salva `Message` no DB, chama `PublishAsync` ou `PublishToAllAsync`

### SSE Event Format

```
event: connected
data: ...

event: notification
data: {"message":"...","sentAt":"..."}
```