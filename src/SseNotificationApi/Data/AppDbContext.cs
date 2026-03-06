using Microsoft.EntityFrameworkCore;
using SseNotificationApi.Models;

namespace SseNotificationApi.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Message> Messages => Set<Message>();
}