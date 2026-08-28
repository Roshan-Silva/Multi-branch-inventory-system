using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MultiBranchInventory.Application.Authentication.Interfaces;
using MultiBranchInventory.Api.Services;
using MultiBranchInventory.Infrastructure.Authentication;
using MultiBranchInventory.Infrastructure.Persistence;
using MultiBranchInventory.Application.Branches.Interfaces;
using MultiBranchInventory.Application.Branches.Services;
using MultiBranchInventory.Application.Categories.Interfaces;
using MultiBranchInventory.Application.Categories.Services;
using MultiBranchInventory.Application.Inventories.Interfaces;
using MultiBranchInventory.Application.Inventories.Services;
using MultiBranchInventory.Application.InventoryTransactions.Interfaces;
using MultiBranchInventory.Application.InventoryTransactions.Services;
using MultiBranchInventory.Application.Products.Interfaces;
using MultiBranchInventory.Application.Products.Services;
using MultiBranchInventory.Application.PurchaseOrders.Interfaces;
using MultiBranchInventory.Application.PurchaseOrders.Services;
using MultiBranchInventory.Application.PurchaseRequests.Interfaces;
using MultiBranchInventory.Application.PurchaseRequests.Services;
using MultiBranchInventory.Application.Suppliers.Interfaces;
using MultiBranchInventory.Application.Suppliers.Services;
using MultiBranchInventory.Application.Users.Interfaces;
using MultiBranchInventory.Application.Users.Services;
using MultiBranchInventory.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// ======================================================
// Database Configuration
// ======================================================

var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' was not found.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));


// ======================================================
// JWT Configuration
// ======================================================

builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection(JwtSettings.SectionName));

var jwtSettings = builder.Configuration
    .GetSection(JwtSettings.SectionName)
    .Get<JwtSettings>()
    ?? throw new InvalidOperationException(
        "JWT configuration was not found.");

if (string.IsNullOrWhiteSpace(jwtSettings.Key))
{
    throw new InvalidOperationException(
        "JWT signing key was not configured.");
}


// ======================================================
// Authentication Services
// ======================================================

builder.Services.AddScoped<IPasswordHasher, PasswordHasherService>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();


// ======================================================
// Branch Services
// ======================================================

builder.Services.AddScoped<IBranchRepository, BranchRepository>();
builder.Services.AddScoped<IBranchService, BranchService>();


// ======================================================
// Category Services
// ======================================================

builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<ICategoryService, CategoryService>();


// ======================================================
// Product Services
// ======================================================

builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IProductService, ProductService>();


// ======================================================
// Supplier Services
// ======================================================

builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();
builder.Services.AddScoped<ISupplierService, SupplierService>();


// ======================================================
// Inventory Services
// ======================================================

builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IInventoryTransactionRepository, InventoryTransactionRepository>();
builder.Services.AddScoped<IInventoryTransactionService, InventoryTransactionService>();


// ======================================================
// Procurement Services
// ======================================================

builder.Services.AddScoped<IPurchaseRequestRepository, PurchaseRequestRepository>();
builder.Services.AddScoped<IPurchaseRequestService, PurchaseRequestService>();
builder.Services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepository>();
builder.Services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();


// ======================================================
// User Services
// ======================================================

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();


// ======================================================
// JWT Authentication
// ======================================================

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,

                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,

                ValidateLifetime = true,

                ValidateIssuerSigningKey = true,

                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtSettings.Key)),

                ClockSkew = TimeSpan.Zero
            };
    });

builder.Services.AddAuthorization();


// ======================================================
// Initial SuperAdmin Seeder Configuration
// ======================================================

builder.Services.Configure<AdminSeedSettings>(
    builder.Configuration.GetSection(
        AdminSeedSettings.SectionName));

builder.Services.AddScoped<DatabaseSeeder>();


// ======================================================
// Controllers / OpenAPI
// ======================================================

builder.Services.AddControllers();

builder.Services.AddOpenApi();


// ======================================================
// Build Application
// ======================================================

var app = builder.Build();


// ======================================================
// Seed Initial Database Data
// ======================================================

using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider
        .GetRequiredService<DatabaseSeeder>();

    await seeder.SeedAsync();
}


// ======================================================
// HTTP Request Pipeline
// ======================================================

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();


// IMPORTANT:
// Authentication must come before Authorization.

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();


// ======================================================
// Run Application
// ======================================================

app.Run();
