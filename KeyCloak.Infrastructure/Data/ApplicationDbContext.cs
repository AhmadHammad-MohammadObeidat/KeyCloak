using KeyCloak.Domian.Users;
using Microsoft.EntityFrameworkCore;
using System;
namespace KeyCloak.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // Define your entity sets
    public DbSet<User> Users { get; set; } = null!;
}
