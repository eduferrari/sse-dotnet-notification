# CLAUDE.md

## Commands

```bash
cd src/SseNotificationApi
dotnet restore
dotnet build
dotnet run
```

No test project exists.

## Architecture

ASP.NET Core 8 Minimal API — `src/SseNotificationApi`.

### Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/api/users` | Cria ou retorna usuário pelo `username` |
| `GET` | `/api/users` | Lista todos os usuários com status |
| `POST` | `/api/notifications` | Envia mensagem — `TargetUserId = null` = broadcast, Guid = usuário específico |
| `GET` | `/api/notifications/stream?userId=` | Long-lived SSE connection (`text/event-stream`) |

### SSE Broadcast (`Services/NotificationChannel.cs`)

Singleton holding a `ConcurrentDictionary<Guid, Channel<string>>`. Each client gets its own unbounded channel.
- `PublishAsync(userId, message)` — envia para um usuário específico.
- `PublishToAllAsync(message)` — itera todos os canais; remove os fechados (desconectados).

### SSE Event Format

```
event: connected
data: ...

event: notification
data: {"message":"...","sentAt":"..."}
```

### Persistência (`Data/AppDbContext.cs`)

EF Core + SQLite (`notifications.db`, criado via `EnsureCreated` na inicialização).

- `Users` — `Id` (Guid), `Username` (string), `Status` (`UserStatus` enum: Offline=0, Online=1)
- `Messages` — `Id`, `TargetUserId?` (null=broadcast), `Content`, `SentAt`

O stream endpoint marca o usuário como `Online` ao conectar e `Offline` ao desconectar (via `IServiceScopeFactory`).

### Front-end (`wwwroot/index.html`)

Duas fases: formulário de login (cria/recupera usuário) → UI principal com lista de usuários, select de destinatário (Todos / específico) e mensagens recebidas.

Swagger UI available at `/swagger`.