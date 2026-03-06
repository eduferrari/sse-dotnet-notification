using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SseNotificationApi.Data;
using SseNotificationApi.Models;
using SseNotificationApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<NotificationChannel>();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=notifications.db"));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseDefaultFiles();
app.UseStaticFiles();

// ── Usuários ────────────────────────────────────────────────────────────────

app.MapGet("/api/users", async (AppDbContext db) =>
    Results.Ok(await db.Users.OrderBy(u => u.Username).ToListAsync()))
.WithName("ListUsers")
.WithSummary("Lista todos os usuários e seus status")
.WithOpenApi();

app.MapPost("/api/users", async (CreateUserRequest request, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.Username))
        return Results.BadRequest(new { error = "Username é obrigatório." });

    var username = request.Username.Trim();
    var existing = await db.Users.FirstOrDefaultAsync(u => u.Username == username);

    if (existing is not null)
        return Results.Ok(existing);

    var user = new User { Username = username };
    db.Users.Add(user);
    await db.SaveChangesAsync();

    return Results.Ok(user);
})
.WithName("CreateUser")
.WithSummary("Cria ou retorna um usuário pelo username")
.WithOpenApi();

// ── Notificações ─────────────────────────────────────────────────────────────

app.MapPost("/api/notifications", async (
    NotificationRequest request,
    AppDbContext db,
    NotificationChannel channel,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
        return Results.BadRequest(new { error = "A mensagem é obrigatória." });

    var message = request.Message.Trim();

    db.Messages.Add(new Message
    {
        TargetUserId = request.TargetUserId,
        Content = message
    });
    await db.SaveChangesAsync();

    if (request.TargetUserId is null)
    {
        await channel.PublishToAllAsync(message, cancellationToken);
        return Results.Ok(new { status = "Mensagem enviada para todos os usuários online.", message });
    }

    var delivered = await channel.PublishAsync(request.TargetUserId.Value, message, cancellationToken);

    if (!delivered)
        return Results.NotFound(new { error = "Usuário não encontrado ou não está conectado." });

    return Results.Ok(new
    {
        status = "Mensagem enviada.",
        targetUserId = request.TargetUserId,
        message
    });
})
.WithName("SendNotification")
.WithSummary("Envia mensagem para um usuário específico ou para todos (TargetUserId = null)")
.WithOpenApi();

app.MapGet("/api/notifications/stream", async (
    HttpContext context,
    IServiceScopeFactory scopeFactory,
    NotificationChannel channel,
    CancellationToken cancellationToken) =>
{
    if (!context.Request.Query.TryGetValue("userId", out var userIdParam)
        || !Guid.TryParse(userIdParam, out var userId))
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("userId inválido ou ausente.", cancellationToken);
        return;
    }

    using (var scope = scopeFactory.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FindAsync([userId], cancellationToken);

        if (user is null)
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsync("Usuário não encontrado.", cancellationToken);
            return;
        }

        user.Status = UserStatus.Online;
        await db.SaveChangesAsync(cancellationToken);
    }

    context.Response.Headers.Append("Content-Type", "text/event-stream");
    context.Response.Headers.Append("Cache-Control", "no-cache");
    context.Response.Headers.Append("Connection", "keep-alive");

    var reader = channel.RegisterClient(userId);

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
        channel.UnregisterClient(userId);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FindAsync([userId]);

        if (user is not null)
        {
            user.Status = UserStatus.Offline;
            await db.SaveChangesAsync();
        }
    }
})
.WithName("StreamNotifications")
.WithSummary("Abre um stream SSE para receber notificações em tempo real")
.WithOpenApi();

app.Run();

public partial class Program { }
