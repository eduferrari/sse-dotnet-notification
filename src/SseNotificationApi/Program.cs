using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SseNotificationApi.Data;
using SseNotificationApi.Models;
using SseNotificationApi.Services;

var builder = WebApplication.CreateBuilder(args);

var jwtKey      = builder.Configuration["Jwt:Key"]!;
var jwtIssuer   = builder.Configuration["Jwt:Issuer"]!;
var jwtAudience = builder.Configuration["Jwt:Audience"]!;
var jwtExpiry   = int.Parse(builder.Configuration["Jwt:ExpiresInMinutes"] ?? "60");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtIssuer,
            ValidAudience            = jwtAudience,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
        // EventSource (SSE) cannot set headers — read token from query string
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                if (ctx.Request.Query.TryGetValue("token", out var token))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Enter: Bearer {token}"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddSingleton<NotificationChannel>();
builder.Services.AddSingleton<IKafkaProducerService, KafkaProducerService>();
builder.Services.AddHostedService<KafkaConsumerService>();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    // Add PasswordHash column if the table already existed before this change
    db.Database.ExecuteSqlRaw("""
        ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "PasswordHash" TEXT NOT NULL DEFAULT '';
        """);
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

// ── Auth ─────────────────────────────────────────────────────────────────────

app.MapPost("/api/auth/register", async (AuthRequest request, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.Username))
        return Results.BadRequest(new { error = "Username é obrigatório." });

    if (string.IsNullOrWhiteSpace(request.Password))
        return Results.BadRequest(new { error = "Password é obrigatório." });

    var username = request.Username.Trim();

    if (await db.Users.AnyAsync(u => u.Username == username))
        return Results.Conflict(new { error = "Username já está em uso." });

    var user = new User
    {
        Username     = username,
        PasswordHash = PasswordHasher.Hash(request.Password)
    };

    db.Users.Add(user);
    await db.SaveChangesAsync();

    var token = GenerateToken(user);
    return Results.Ok(new { token, userId = user.Id, username = user.Username });
})
.WithName("Register")
.WithSummary("Registra novo usuário e retorna JWT")
.WithOpenApi();

app.MapPost("/api/auth/login", async (AuthRequest request, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        return Results.BadRequest(new { error = "Username e password são obrigatórios." });

    var username = request.Username.Trim();
    var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username);

    if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
        return Results.Unauthorized();

    var token = GenerateToken(user);
    return Results.Ok(new { token, userId = user.Id, username = user.Username });
})
.WithName("Login")
.WithSummary("Autentica usuário e retorna JWT")
.WithOpenApi();

// ── Usuários ─────────────────────────────────────────────────────────────────

app.MapGet("/api/users", async (AppDbContext db) =>
    Results.Ok(await db.Users.OrderBy(u => u.Username).ToListAsync()))
.WithName("ListUsers")
.WithSummary("Lista todos os usuários e seus status")
.RequireAuthorization()
.WithOpenApi();

// ── Notificações ──────────────────────────────────────────────────────────────

app.MapPost("/api/notifications", async (
    NotificationRequest request,
    AppDbContext db,
    NotificationChannel channel,
    IKafkaProducerService kafkaProducer,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
        return Results.BadRequest(new { error = "A mensagem é obrigatória." });

    var message = request.Message.Trim();

    db.Messages.Add(new Message
    {
        TargetUserId = request.TargetUserId,
        Content      = message
    });
    await db.SaveChangesAsync(cancellationToken);

    if (request.TargetUserId is not null && !channel.HasClient(request.TargetUserId.Value))
        return Results.NotFound(new { error = "Usuário não encontrado ou não está conectado." });

    await kafkaProducer.PublishAsync(request.TargetUserId, message, cancellationToken);

    if (request.TargetUserId is null)
        return Results.Ok(new { status = "Mensagem enviada para todos os usuários online.", message });

    return Results.Ok(new { status = "Mensagem enviada.", targetUserId = request.TargetUserId, message });
})
.WithName("SendNotification")
.WithSummary("Envia mensagem para um usuário específico ou para todos (TargetUserId = null)")
.RequireAuthorization()
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
        var db   = scope.ServiceProvider.GetRequiredService<AppDbContext>();
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

    // Register client before broadcasting so the new user also receives the update
    var reader = channel.RegisterClient(userId);

    // Broadcast updated user list to all connected clients (including the new one)
    using (var scope = scopeFactory.CreateScope())
    {
        var db       = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var allUsers = await db.Users.OrderBy(u => u.Username).ToListAsync(cancellationToken);
        await channel.PublishUserListAsync(JsonSerializer.Serialize(allUsers), cancellationToken);
    }

    try
    {
        await context.Response.WriteAsync("event: connected\ndata: Conectado ao stream de notificações\n\n", cancellationToken);
        await context.Response.Body.FlushAsync(cancellationToken);

        await foreach (var ev in reader.ReadAllAsync(cancellationToken))
        {
            if (ev.EventType == "notification")
            {
                var payload = JsonSerializer.Serialize(new
                {
                    message = ev.Data,
                    sentAt  = DateTimeOffset.UtcNow
                });
                await context.Response.WriteAsync($"event: notification\ndata: {payload}\n\n", cancellationToken);
            }
            else
            {
                await context.Response.WriteAsync($"event: {ev.EventType}\ndata: {ev.Data}\n\n", cancellationToken);
            }

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
        var db   = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FindAsync([userId]);

        if (user is not null)
        {
            user.Status = UserStatus.Offline;
            await db.SaveChangesAsync();

            var allUsers = await db.Users.OrderBy(u => u.Username).ToListAsync();
            await channel.PublishUserListAsync(JsonSerializer.Serialize(allUsers));
        }
    }
})
.WithName("StreamNotifications")
.WithSummary("Abre um stream SSE para receber notificações em tempo real")
.RequireAuthorization()
.WithOpenApi();

app.Run();

string GenerateToken(User user)
{
    var claims = new[]
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Name, user.Username)
    };

    var key         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
    var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    var token       = new JwtSecurityToken(
        issuer:             jwtIssuer,
        audience:           jwtAudience,
        claims:             claims,
        expires:            DateTime.UtcNow.AddMinutes(jwtExpiry),
        signingCredentials: credentials);

    return new JwtSecurityTokenHandler().WriteToken(token);
}

public partial class Program { }