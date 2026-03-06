using System.Text.Json;
using SseNotificationApi.Models;
using SseNotificationApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<NotificationChannel>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/notifications", async (
    NotificationRequest request,
    NotificationChannel channel,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
    {
        return Results.BadRequest(new { error = "A mensagem é obrigatória." });
    }

    var delivered = await channel.PublishAsync(request.ClientId, request.Message.Trim(), cancellationToken);

    if (!delivered)
    {
        return Results.NotFound(new { error = "Cliente não encontrado ou não está conectado." });
    }

    return Results.Ok(new
    {
        status = "Mensagem enviada para o cliente.",
        clientId = request.ClientId,
        message = request.Message.Trim()
    });
})
.WithName("SendNotification")
.WithSummary("Envia uma mensagem para um cliente específico via SSE")
.WithOpenApi();

app.MapGet("/api/notifications/stream", async (
    HttpContext context,
    NotificationChannel channel,
    CancellationToken cancellationToken) =>
{
    if (!context.Request.Query.TryGetValue("clientId", out var clientIdParam)
        || !Guid.TryParse(clientIdParam, out var clientId))
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("clientId inválido ou ausente.", cancellationToken);
        return;
    }

    context.Response.Headers.Append("Content-Type", "text/event-stream");
    context.Response.Headers.Append("Cache-Control", "no-cache");
    context.Response.Headers.Append("Connection", "keep-alive");

    var reader = channel.RegisterClient(clientId);

    try
    {
        await context.Response.WriteAsync("event: connected\n", cancellationToken);
        await context.Response.WriteAsync("data: Conectado ao stream de notificações\n\n", cancellationToken);
        await context.Response.Body.FlushAsync(cancellationToken);

        await foreach (var message in reader.ReadAllAsync(cancellationToken))
        {
            var payload = JsonSerializer.Serialize(new
            {
                message,
                sentAt = DateTimeOffset.UtcNow
            });

            await context.Response.WriteAsync("event: notification\n", cancellationToken);
            await context.Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
            await context.Response.Body.FlushAsync(cancellationToken);
        }
    }
    catch (OperationCanceledException)
    {
        // conexão fechada pelo cliente
    }
    finally
    {
        channel.UnregisterClient(clientId);
    }
})
.WithName("StreamNotifications")
.WithSummary("Abre um stream SSE para receber notificações em tempo real")
.WithOpenApi();

app.Run();
