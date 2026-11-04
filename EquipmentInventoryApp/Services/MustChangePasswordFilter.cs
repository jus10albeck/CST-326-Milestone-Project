
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EquipmentInventoryApp.Services
{
    // Sends users with a temporary password to Change Password and blocks every
    // other page until a new password is saved (Milestone 3, Account and User Management)
    public class MustChangePasswordFilter : IActionFilter
    {
        // Added to the login cookie when the user signed in with a temporary password
        public const string ClaimType = "MustChangePassword";

        public void OnActionExecuting(ActionExecutingContext context)
        {
            var user = context.HttpContext.User;

            if (user.Identity?.IsAuthenticated != true ||
                user.FindFirstValue(ClaimType) != "true")
            {
                return;
            }

            var controller = context.RouteData.Values["controller"]?.ToString();
            var action = context.RouteData.Values["action"]?.ToString();

            // These pages stay open so the user can change the password or log out
            if (controller == "Account" &&
                action is "ChangePassword" or "Logout" or "Login" or "AccessDenied")
            {
                return;
            }

            context.Result = new RedirectToActionResult("ChangePassword", "Account", null);
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
        }
    }
}
