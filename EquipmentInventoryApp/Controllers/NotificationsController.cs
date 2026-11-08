
using System.Security.Claims;
using EquipmentInventoryApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentInventoryApp.Controllers
{
    // Dismisses on-screen notifications (FR-37)
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly NotificationService _notifications;

        public NotificationsController(NotificationService notifications)
        {
            _notifications = notifications;
        }

        // Dismisses one notification, then returns to the page the user was on
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Dismiss(int id, string? returnUrl)
        {
            await _notifications.DismissAsync(id, GetUserId());

            return GoBack(returnUrl);
        }

        // Dismisses all of the user's notifications
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DismissAll(string? returnUrl)
        {
            await _notifications.DismissAllAsync(GetUserId());

            return GoBack(returnUrl);
        }

        // Hides the overdue reminder until the employee logs in again (US-31).
        // The reminder still goes away on its own once the items are returned.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DismissOverdueReminder(string? returnUrl)
        {
            Response.Cookies.Append(
                OverdueReminderService.DismissedCookie,
                "1",
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    IsEssential = true
                });

            return GoBack(returnUrl);
        }

        // Only returns to pages on this site
        private IActionResult GoBack(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        private int GetUserId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }
    }
}
