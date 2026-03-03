# SSE .NET Notification MVP

Projeto de estudo (MPV/MVP) para aprender **Server-Sent Events (SSE)** com uma API C#.

## O que este projeto entrega

- API ASP.NET Core com Swagger.
- Endpoint para **enviar mensagem**.
- Endpoint SSE para o front **receber mensagens em tempo real**.
- Front-end estático simples para visualizar e disparar notificações.

## Endpoints

- `POST /api/notifications`
  - Body JSON:
    ```json
    { "message": "Olá via SSE" }
    ```
  - Envia a mensagem para todos os clientes conectados.

- `GET /api/notifications/stream`
  - Abre conexão SSE (`text/event-stream`) para receber eventos `notification`.

## Executar localmente

> Requer .NET 8 SDK.

```bash
cd src/SseNotificationApi
dotnet restore
dotnet run
```

Depois abra:

- App: `http://localhost:5000` (ou porta exibida no terminal)
- Swagger: `http://localhost:5000/swagger`

## Fluxo de uso

1. Abra o front em uma aba para conectar no stream SSE.
2. Envie mensagens pelo formulário da página **ou** pelo Swagger no `POST /api/notifications`.
3. O front recebe o evento e notifica o usuário.
