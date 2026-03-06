# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# From the project directory
cd src/SseNotificationApi

dotnet restore
dotnet build
dotnet run
```

No test project exists in this repository.

## Architecture

Single-project ASP.NET Core 8 Minimal API (`src/SseNotificationApi`) with two endpoints:

- `POST /api/notifications` — accepts `{ "message": "..." }`, publishes to all connected SSE clients
- `GET /api/notifications/stream` — long-lived SSE connection (`text/event-stream`)

**SSE broadcast mechanism** (`Services/NotificationChannel.cs`): a singleton that holds a `ConcurrentDictionary<Guid, Channel<string>>`. Each SSE client gets its own unbounded `System.Threading.Channels.Channel<string>`. Publishing iterates all registered channels and writes to each writer; disconnected clients (whose channel is closed) are collected and removed.

**SSE event format** emitted by the stream endpoint:
- On connect: `event: connected\ndata: ...\n\n`
- On message: `event: notification\ndata: {"message":"...","sentAt":"..."}\n\n`

**Front-end** (`wwwroot/index.html`): static page served via `UseDefaultFiles`/`UseStaticFiles`. Uses `EventSource` to connect to the stream and the Web Notifications API (with `alert` fallback) to surface messages.

Swagger UI is available at `/swagger` in all environments.