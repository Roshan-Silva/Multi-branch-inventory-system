using Microsoft.EntityFrameworkCore;
using MultiBranchInventory.Domain.Entities;

namespace MultiBranchInventory.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Branch> Branches => Set<Branch>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<Inventory> Inventories => Set<Inventory>();

    public DbSet<InventoryTransaction> InventoryTransactions =>
        Set<InventoryTransaction>();

    public DbSet<PurchaseRequest> PurchaseRequests =>
        Set<PurchaseRequest>();

    public DbSet<PurchaseRequestItem> PurchaseRequestItems =>
        Set<PurchaseRequestItem>();

    public DbSet<PurchaseOrder> PurchaseOrders =>
        Set<PurchaseOrder>();

    public DbSet<PurchaseOrderItem> PurchaseOrderItems =>
        Set<PurchaseOrderItem>();

    public DbSet<GoodsReceivedNote> GoodsReceivedNotes =>
        Set<GoodsReceivedNote>();

    public DbSet<GoodsReceivedItem> GoodsReceivedItems =>
        Set<GoodsReceivedItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AppDbContext).Assembly);
    }
}