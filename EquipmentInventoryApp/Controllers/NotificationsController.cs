
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
