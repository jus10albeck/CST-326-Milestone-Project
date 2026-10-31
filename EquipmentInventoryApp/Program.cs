
using EquipmentInventoryApp.Data;
using EquipmentInventoryApp.Models;
using EquipmentInventoryApp.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Sets up the MVC application
builder.Services.AddControllersWithViews();

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
