# SSE .NET Notification

Projeto de estudo para aprender **Server-Sent Events (SSE)** com ASP.NET Core 8.

## Funcionalidades

- Cadastro de usuários com status **online/offline** persistido em SQLite
- Envio de mensagem para um **usuário específico** ou **broadcast** para todos os conectados
- Stream SSE em tempo real por usuário
- Histórico de mensagens salvo no banco
- Front-end estático com login, lista de usuários e envio de mensagens

## Executar localmente

> Requer .NET 8 SDK.

```bash
cd src/SseNotificationApi
dotnet run
```

- App: `http://localhost:5000`
- Swagger: `http://localhost:5000/swagger`

O banco `notifications.db` é criado automaticamente na primeira execução.

## Endpoints

| Method | Path | Descrição |
|--------|------|-----------|
| `POST` | `/api/users` | Cria ou retorna usuário pelo `username` |
| `GET` | `/api/users` | Lista todos os usuários com status |
| `POST` | `/api/notifications` | Envia mensagem — `targetUserId: null` = broadcast |
| `GET` | `/api/notifications/stream?userId=` | Abre stream SSE do usuário |

## Fluxo de uso

1. Acesse `http://localhost:5000`, informe um nome e clique em **Conectar**.
2. Abra o app em outra aba com um nome diferente para ter dois usuários.
3. Envie mensagens para um usuário específico ou para todos pelo formulário ou pelo Swagger.