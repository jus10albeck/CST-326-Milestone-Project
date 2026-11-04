
using EquipmentInventoryApp.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EquipmentInventoryApp.Services
{
    public class CustomCookieAuthenticationEvents
        : CookieAuthenticationEvents
    {
        private readonly ApplicationDbContext _context;

        public CustomCookieAuthenticationEvents(
            ApplicationDbContext context)
        {
            _context = context;
        }

        // Checks if the user's login session is still valid
        public override async Task ValidatePrincipal(
            CookieValidatePrincipalContext context)
        {
            var idValue = context.Principal?
                .FindFirstValue(ClaimTypes.NameIdentifier);

            var stamp = context.Principal?
                .FindFirstValue("SecurityStamp");

            var role = context.Principal?
                .FindFirstValue(ClaimTypes.Role);

            // True when the user signed in with a temporary password
            var signedInWithTemporaryPassword = context.Principal?
                .FindFirstValue(MustChangePasswordFilter.ClaimType) == "true";

            // Makes sure the user ID and security stamp exist
            if (!int.TryParse(idValue, out int userId) ||
                string.IsNullOrEmpty(stamp))
            {
                await RejectAsync(context);
                return;
            }

            // Gets the user's current information from MySQL
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == userId);

            // Checks if the account or login information has changed
            if (user == null ||
                user.Status != "Active" ||
                user.MustChangePassword != signedInWithTemporaryPassword ||
                user.SecurityStamp != stamp ||
                user.Role != role)
            {
                await RejectAsync(context);
            }
        }

        // Signs the user out if their login is no longer valid
        private static async Task RejectAsync(
            CookieValidatePrincipalContext context)
        {
            context.RejectPrincipal();

            await context.HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}
