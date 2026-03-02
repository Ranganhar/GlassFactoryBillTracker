using GlassFactory.BillTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlassFactory.BillTracker.Data.Persistence;

public class BillTrackerDbContext : DbContext
{
    public BillTrackerDbContext(DbContextOptions<BillTrackerDbContext> options) : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderAttachment> OrderAttachments => Set<OrderAttachment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BillTrackerDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
