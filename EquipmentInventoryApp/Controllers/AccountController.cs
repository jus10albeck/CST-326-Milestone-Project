
using EquipmentInventoryApp.Data;
using EquipmentInventoryApp.Models;
using EquipmentInventoryApp.Services;
using EquipmentInventoryApp.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EquipmentInventoryApp.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;

        public AccountController(
            ApplicationDbContext context,
            IPasswordHasher<User> passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
        }

        // Opens the login page
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login()
        {
            return View(new LoginViewModel());
        }

        // Checks the user's login information
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var email = model.Email.Trim();

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email);

            // Checks the password first, so the message does not reveal
            // whether an email address has an account
            var result = user == null
                ? PasswordVerificationResult.Failed
                : _passwordHasher.VerifyHashedPassword(
                    user,
                    user.PasswordHash,
                    model.Password);

            if (user == null || result == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError("", "Invalid email or password.");

                return View(model);
            }

            // Deactivated and Pending accounts get their own messages (FR-5, Milestone 3)
            if (user.Status == AccountRules.DeactivatedStatus)
            {
                ModelState.AddModelError(
                    "",
                    "This account has been deactivated. Contact an administrator.");

                return View(model);
            }

            if (user.Status == AccountRules.PendingStatus)
            {
                ModelState.AddModelError(
                    "",
                    "Your account request is waiting for administrator approval.");

                return View(model);
            }

            if (user.Status != AccountRules.ActiveStatus)
            {
                ModelState.AddModelError("", "Invalid email or password.");

                return View(model);
            }

            if (result == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.PasswordHash = _passwordHasher.HashPassword(
                    user,
                    model.Password);

                await _context.SaveChangesAsync();
            }

            await SignInUserAsync(user);

            // Shows the overdue reminder again after every login (US-31)
            Response.Cookies.Delete(OverdueReminderService.DismissedCookie);

            // A temporary password must be changed before using the application
            if (user.MustChangePassword)
            {
                return RedirectToAction(nameof(ChangePassword));
            }

            // Sends administrators to their dashboard
            if (user.Role == "Administrator")
            {
                return RedirectToAction("Index", "Admin");
            }

            return RedirectToAction("Index", "Equipment");
        }

        // Saves the user's information and security stamp in the login cookie
        private async Task SignInUserAsync(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    user.UserId.ToString()),

                new Claim(
                    ClaimTypes.Name,
                    user.FirstName),

                new Claim(
                    ClaimTypes.Email,
                    user.Email),

                new Claim(
                    ClaimTypes.Role,
                    user.Role),

                new Claim(
                    "SecurityStamp",
                    user.SecurityStamp)
            };

            if (user.MustChangePassword)
            {
                claims.Add(new Claim(MustChangePasswordFilter.ClaimType, "true"));
            }

            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));
        }

        // Opens the change password page
        [Authorize]
        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View(new ChangePasswordViewModel());
        }

        // Checks the old password before saving the new one
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(
            ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var userIdValue = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdValue, out int userId))
            {
                await HttpContext.SignOutAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme);

                return RedirectToAction("Login");
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null || user.Status != "Active")
            {
                await HttpContext.SignOutAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme);

                return RedirectToAction("Login");
            }

            var result = _passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                model.CurrentPassword);

            if (result == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(
                    "CurrentPassword",
                    "Your current password is incorrect.");

                return View(model);
            }

            // Makes sure the new password is different
            var samePassword = _passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                model.NewPassword);

            if (samePassword != PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(
                    "NewPassword",
                    "Your new password must be different.");

                return View(model);
            }

            // Saves the new password hash
            user.PasswordHash = _passwordHasher.HashPassword(
                user,
                model.NewPassword);

            user.MustChangePassword = false;

            // Changes the security stamp to invalidate old logins
            user.SecurityStamp = Guid.NewGuid().ToString("N");

            await _context.SaveChangesAsync();

            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            TempData["SuccessMessage"] =
                "Password changed successfully. Please log in again.";

            return RedirectToAction("Login");
        }

        // Logs the user out of the application
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            Response.Cookies.Delete(OverdueReminderService.DismissedCookie);

            return RedirectToAction("Login");
        }

        // Displays a message when the user does not have access
        [AllowAnonymous]
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return Content(
                "Access denied. You do not have permission.");
        }
    }
}
