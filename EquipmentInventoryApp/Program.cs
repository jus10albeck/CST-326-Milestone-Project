
using EquipmentInventoryApp.Data;
using EquipmentInventoryApp.Models;
using EquipmentInventoryApp.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Sets up the MVC application
// Blocks other pages until a temporary password is changed
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add<MustChangePasswordFilter>());

// Gets the connection to the MySQL database
var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "DefaultConnection was not configured.");

// Connects the database to Entity Framework
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString)));

// Sets up password hashing and cookie security
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

builder.Services.AddScoped<CustomCookieAuthenticationEvents>();

// Sets up checkout rules. The loan period and checkout limit can be changed
// in appsettings.json under "Checkout"; otherwise 7 days and 3 items are used.
builder.Services.Configure<CheckoutOptions>(
    builder.Configuration.GetSection("Checkout"));

builder.Services.AddScoped<CheckoutService>();

// Records and reads each item's history
builder.Services.AddScoped<EquipmentHistoryService>();

// Rules for changing an item's status and retiring it
builder.Services.AddScoped<EquipmentStatusService>();

// Sets up login cookies and the 30-minute session
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";

        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = false;

        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;

        options.EventsType = typeof(CustomCookieAuthenticationEvents);
    });

// Allows different roles to have different permissions
builder.Services.AddAuthorization();

var app = builder.Build();

// Creates or updates the database tables when the application starts.
// If the tables are already up to date, nothing is changed.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();

    await context.Database.MigrateAsync();

    // Adds the test accounts and starter categories only when test passwords
    // are configured. Records that already exist are skipped.
    if (app.Configuration.GetSection("SeedUsers").Exists())
    {
        await DbInitializer.SeedUsersAsync(services, app.Configuration);
        await DbInitializer.SeedCategoriesAsync(services);
    }
}

// Handles errors when the application is not in development
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Sets up how the application handles requests
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Checks login information and user permissions
app.UseAuthentication();
app.UseAuthorization();

// Sets the default route for the website
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
