
using System.Security.Claims;
using EquipmentInventoryApp.Data;
using EquipmentInventoryApp.Models;
using EquipmentInventoryApp.Services;
using EquipmentInventoryApp.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

namespace EquipmentInventoryApp.Controllers
{
    // User Management: create accounts, reset passwords, and activate or
    // deactivate accounts (US-2, US-4, US-5, FR-1 to FR-6)
    [Authorize(Roles = "Administrator")]
    public class UsersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly CheckoutService _checkoutService;

        public UsersController(
            ApplicationDbContext context,
            IPasswordHasher<User> passwordHasher,
            CheckoutService checkoutService)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _checkoutService = checkoutService;
        }

        // Lists every account
        public async Task<IActionResult> Index()
        {
            var currentUserId = GetCurrentUserId();

            var users = await _context.Users
                .AsNoTracking()
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .ToListAsync();

            var model = users
                .Select(u => new UserListItemViewModel
                {
                    UserId = u.UserId,
                    FullName = $"{u.FirstName} {u.LastName}",
                    Email = u.Email,
                    Role = u.Role,
                    Status = u.Status,
                    MustChangePassword = u.MustChangePassword,
                    IsCurrentUser = u.UserId == currentUserId
                })
                .ToList();

            return View(model);
        }

        // Opens the Create Account form (FR-1)
        [HttpGet]
        public IActionResult Create()
        {
            return View(new CreateAccountViewModel());
        }

        // Creates an Active account with a temporary password (FR-2, FR-3)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateAccountViewModel model)
        {
            var email = model.Email?.Trim() ?? string.Empty;

            if (!AccountRules.Roles.Contains(model.Role))
            {
                ModelState.AddModelError(nameof(model.Role), "Please select a role");
            }

            // Email addresses must be unique, ignoring capitalization.
            // Checked whenever the email itself is valid, so every error shows at once.
            if (ModelState.GetFieldValidationState(nameof(model.Email)) != ModelValidationState.Invalid &&
                await EmailInUseAsync(email))
            {
                ModelState.AddModelError(
                    nameof(model.Email),
                    "An account with this email already exists.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = new User
            {
                FirstName = model.FirstName.Trim(),
                LastName = model.LastName.Trim(),
                Email = email,
                Role = model.Role,
                Status = AccountRules.ActiveStatus,

                // The new user must change the temporary password at first login
                MustChangePassword = true
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, model.TemporaryPassword);

            _context.Users.Add(user);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Another administrator may have used the same email at the same moment
                if (!await EmailInUseAsync(email))
                {
                    throw;
                }

                _context.Entry(user).State = EntityState.Detached;

                ModelState.AddModelError(
                    nameof(model.Email),
                    "An account with this email already exists.");

                return View(model);
            }

            TempData["SuccessMessage"] = "Account created.";

            return RedirectToAction(nameof(Index));
        }

        // Opens the Reset Password form (US-4)
        [HttpGet]
        public async Task<IActionResult> ResetPassword(int id)
        {
            var user = await FindOtherUserAsync(id);

            if (user == null)
            {
                return RedirectToAction(nameof(Index));
            }

            return View(new ResetPasswordViewModel
            {
                UserId = user.UserId,
                FullName = $"{user.FirstName} {user.LastName}"
            });
        }

        // Gives the user a new temporary password that must be changed at next login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            var user = await FindOtherUserAsync(model.UserId);

            if (user == null)
            {
                return RedirectToAction(nameof(Index));
            }

            model.FullName = $"{user.FirstName} {user.LastName}";

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            user.PasswordHash = _passwordHasher.HashPassword(user, model.TemporaryPassword);
            user.MustChangePassword = true;

            // Logs the user out anywhere they are signed in
            user.SecurityStamp = Guid.NewGuid().ToString("N");

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Password reset for {model.FullName}. They must change it the next time they log in.";

            return RedirectToAction(nameof(Index));
        }

        // Changes an account between Active and Deactivated (FR-4).
        // The account's checkout, reservation, and damage report records are kept (FR-6).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetStatus(int id, string status)
        {
            if (status != AccountRules.ActiveStatus &&
                status != AccountRules.DeactivatedStatus)
            {
                return RedirectToAction(nameof(Index));
            }

            var user = await FindOtherUserAsync(id);

            if (user == null)
            {
                return RedirectToAction(nameof(Index));
            }

            var fullName = $"{user.FirstName} {user.LastName}";

            if (user.Status == status)
            {
                return RedirectToAction(nameof(Index));
            }

            // A user with items checked out goes to the Deactivate page first,
            // where the items can be returned or the deactivation canceled
            if (status == AccountRules.DeactivatedStatus &&
                await _checkoutService.CountActiveCheckoutsAsync(user.UserId) > 0)
            {
                return RedirectToAction(nameof(Deactivate), new { id = user.UserId });
            }

            user.Status = status;

            if (status == AccountRules.DeactivatedStatus)
            {
                // Logs the user out right away
                user.SecurityStamp = Guid.NewGuid().ToString("N");
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = status == AccountRules.DeactivatedStatus
                ? $"{fullName}'s account has been deactivated."
                : $"{fullName}'s account is active.";

            return RedirectToAction(nameof(Index));
        }

        // Shows the items a user still has checked out before deactivating the account
        [HttpGet]
        public async Task<IActionResult> Deactivate(int id)
        {
            var user = await FindOtherUserAsync(id);

            if (user == null || user.Status == AccountRules.DeactivatedStatus)
            {
                return RedirectToAction(nameof(Index));
            }

            var today = DateTime.Today;

            var checkouts = await _context.Checkouts
                .Include(c => c.Equipment)
                .AsNoTracking()
                .Where(c => c.UserId == user.UserId && c.ReturnDate == null)
                .OrderBy(c => c.DueDate)
                .ToListAsync();

            return View(new DeactivateUserViewModel
            {
                UserId = user.UserId,
                FullName = $"{user.FirstName} {user.LastName}",
                Email = user.Email,
                Items = checkouts
                    .Select(c => new MyEquipmentItemViewModel
                    {
                        CheckoutId = c.CheckoutId,
                        EquipmentId = c.EquipmentId,
                        Name = c.Equipment.Name,
                        SerialNumber = c.Equipment.SerialNumber,
                        CheckoutDate = c.CheckoutDate,
                        DueDate = c.DueDate,
                        IsOverdue = c.DueDate.Date < today
                    })
                    .ToList()
            });
        }

        // Returns the user's items, then deactivates the account, in one transaction
        [HttpPost]
        [ActionName("Deactivate")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeactivateConfirmed(
            int id,
            List<DeactivateItemChoiceViewModel> items)
        {
            var user = await FindOtherUserAsync(id);

            if (user == null || user.Status == AccountRules.DeactivatedStatus)
            {
                return RedirectToAction(nameof(Index));
            }

            var fullName = $"{user.FirstName} {user.LastName}";

            await using var transaction = await _context.Database.BeginTransactionAsync();

            // Items the administrator chose to mark Lost. Everything else is returned.
            var lostEquipmentIds = items
                .Where(i => i.MarkLost)
                .Select(i => i.EquipmentId)
                .ToHashSet();

            var (returnedCount, lostCount) =
                await _checkoutService.CloseAllForUserAsync(
                    user.UserId,
                    lostEquipmentIds,
                    GetCurrentUserId());

            user.Status = AccountRules.DeactivatedStatus;

            // Logs the user out right away
            user.SecurityStamp = Guid.NewGuid().ToString("N");

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            var itemSummary = new List<string>();

            if (returnedCount > 0)
            {
                itemSummary.Add($"{returnedCount} item(s) returned");
            }

            if (lostCount > 0)
            {
                itemSummary.Add($"{lostCount} item(s) marked Lost");
            }

            TempData["SuccessMessage"] = itemSummary.Count == 0
                ? $"{fullName}'s account has been deactivated."
                : $"{fullName}'s account has been deactivated. {string.Join(", ", itemSummary)}.";

            return RedirectToAction(nameof(Index));
        }

        // Finds a user other than the administrator who is signed in.
        // Administrators cannot reset or deactivate their own account here,
        // so they cannot lock themselves out.
        private async Task<User?> FindOtherUserAsync(int id)
        {
            if (id == GetCurrentUserId())
            {
                TempData["ErrorMessage"] =
                    "You cannot change your own account here. Use Change Password instead.";

                return null;
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == id);

            if (user == null)
            {
                TempData["ErrorMessage"] = "That account no longer exists.";
            }

            return user;
        }

        // Checks for an account with the same email, ignoring capitalization
        private Task<bool> EmailInUseAsync(string email)
        {
            var lowerEmail = email.ToLower();

            return _context.Users.AnyAsync(u => u.Email.ToLower() == lowerEmail);
        }

        private int GetCurrentUserId()
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
                ? id
                : 0;
        }
    }
}
