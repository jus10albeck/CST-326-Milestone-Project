
using EquipmentInventoryApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EquipmentInventoryApp.Data
{
    public static class DbInitializer
    {
        // Creates the test accounts for the application
        public static async Task SeedUsersAsync(
            IServiceProvider services,
            IConfiguration configuration)
        {
            var context = services.GetRequiredService<ApplicationDbContext>();
            var hasher = services.GetRequiredService<IPasswordHasher<User>>();

            // Sets up the administrator and employee accounts
            var accounts = new[]
            {
                new
                {
                    Email = "admin@equipment.test",
                    FirstName = "Test",
                    LastName = "Administrator",
                    Role = "Administrator",
                    PasswordKey = "SeedUsers:AdminPassword"
                },
                new
                {
                    Email = "employee@equipment.test",
                    FirstName = "Test",
                    LastName = "Employee",
                    Role = "Employee",
                    PasswordKey = "SeedUsers:EmployeePassword"
                }
            };

            // Goes through each account and checks if it already exists
            foreach (var account in accounts)
            {
                if (await context.Users.AnyAsync(u => u.Email == account.Email))
                    continue;

                // Gets the test password from the application settings
                var password = configuration[account.PasswordKey];

                if (string.IsNullOrWhiteSpace(password))
                    throw new InvalidOperationException(
                        $"Missing configuration: {account.PasswordKey}");

                // Creates the new user account
                var user = new User
                {
                    FirstName = account.FirstName,
                    LastName = account.LastName,
                    Email = account.Email,
                    Role = account.Role,
                    Status = "Active",
                    MustChangePassword = false
                };

                // Hashes the password before saving it
                user.PasswordHash = hasher.HashPassword(user, password);

                context.Users.Add(user);
            }

            // Saves the new accounts to MySQL
            await context.SaveChangesAsync();
        }
    }
}
