using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;
using ForexExchange.Services;
using System.Linq;
using System.Threading.Tasks;

namespace ForexExchange.Controllers
{
    /// <summary>
    /// Bank Account Management Controller
    /// کنترلر مدیریت حساب‌های بانکی
    /// </summary>
    //[Authorize(Roles = "Admin,Operator,Programmer")] // Temporarily commented for debugging
    public class BankAccountController : Controller
    {
        private readonly ForexDbContext _context;
        private readonly AdminNotificationService _adminNotificationService;

        public BankAccountController(ForexDbContext context, AdminNotificationService adminNotificationService)
        {
            _context = context;
            _adminNotificationService = adminNotificationService;
        }

        /// <summary>
        /// List all bank accounts (only system customers have bank accounts)
        /// نمایش لیست همه حساب‌های بانکی (فقط مشتریان سیستم حساب بانکی دارند)
        /// </summary>
        public async Task<IActionResult> Index()
        {
            try
            {
                var bankAccounts = await _context.BankAccounts
                    .OrderBy(b => b.BankName)
                    .ToListAsync();

                return View(bankAccounts);
            }
            catch (FormatException ex)
            {
                // Handle DateTime parsing errors - likely due to empty string DateTime fields in database
                TempData["ErrorMessage"] = "خطا در بارگذاری اطلاعات حساب‌های بانکی. لطفاً با مدیر سیستم تماس بگیرید.";
                
                // Log the error for debugging
                Console.WriteLine($"DateTime parsing error in BankAccount Index: {ex.Message}");
                
                // Return empty list to avoid crash
                return View(new List<BankAccount>());
            }
            catch (Exception ex)
            {
                // Handle other potential errors
                TempData["ErrorMessage"] = "خطا در بارگذاری اطلاعات. لطفاً دوباره تلاش کنید.";
                Console.WriteLine($"Error in BankAccount Index: {ex.Message}");
                return View(new List<BankAccount>());
            }
        }

        /// <summary>
        /// List bank accounts for a specific customer (only system customers have bank accounts)
        /// نمایش حساب‌های بانکی یک مشتری خاص (فقط مشتریان سیستم حساب بانکی دارند)
        /// </summary>
        public async Task<IActionResult> CustomerAccounts(int customerId)
        {
            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null)
            {
                return NotFound();
            }

            // Only system customers can have bank accounts
            if (!customer.IsSystem)
            {
                TempData["Error"] = "فقط مشتریان سیستم می‌توانند حساب بانکی داشته باشند.";
                return RedirectToAction("Index");
            }

            var bankAccounts = await _context.BankAccounts
                .Where(b => b.CustomerId == customerId)
                .OrderBy(b => b.BankName)
                .ToListAsync();

            ViewBag.Customer = customer;
            return View(bankAccounts);
        }

        /// <summary>
        /// List system customer bank accounts
        /// نمایش حساب‌های بانکی مشتری سیستم
        /// </summary>
      
        public async Task<IActionResult> SystemAccounts()
        {
            var systemCustomer = await _context.Customers
                .FirstOrDefaultAsync(c => c.IsSystem);

            if (systemCustomer == null)
            {
                TempData["Error"] = "مشتری سیستم یافت نشد.";
                return RedirectToAction("Index");
            }

            var bankAccounts = await _context.BankAccounts
                .Where(b => b.CustomerId == systemCustomer.Id)
                .OrderBy(b => b.BankName)
                .ToListAsync();

            ViewBag.SystemCustomer = systemCustomer;
            return View(bankAccounts);
        }

        /// <summary>
        /// Create new bank account
        /// ایجاد حساب بانکی جدید
        /// </summary>
   
        public async Task<IActionResult> Create()
        {
           
            // Only system customers can have bank accounts
            var systemCustomer = await _context.Customers
                .FirstOrDefaultAsync(c => c.IsActive && c.IsSystem);

           
            ViewBag.Currencies = await _context.Currencies
          .Where(c => c.IsActive)
          .OrderBy(c => c.DisplayOrder)
          .ToListAsync();

            var model = new BankAccount();

            if (systemCustomer == null)
            {
                TempData["Error"] = "کاربر سیستمی  وجود ندارد";
            }
            else
            {

                model.CustomerId = systemCustomer.Id;
            }


            return View(model);
        }

        /// <summary>
        /// Create bank account POST
        /// ایجاد حساب بانکی POST
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BankAccount model)
        {
           

            // Remove Customer validation error since we handle CustomerId separately
            ModelState.Remove("Customer");

            if (ModelState.IsValid)
            {
                // Validate that the customer is a system customer
                var customer = await _context.Customers.FindAsync(model.CustomerId);
                if (customer == null || !customer.IsSystem)
                {
                    ModelState.AddModelError("", "فقط مشتریان سیستم می‌توانند حساب بانکی داشته باشند.");
                    ViewBag.Customers = await _context.Customers
                        .Where(c => c.IsActive && c.IsSystem)
                        .OrderBy(c => c.FullName)
                        .ToListAsync();
                    ViewBag.Currencies = await _context.Currencies
                        .Where(c => c.IsActive)
                        .OrderBy(c => c.DisplayOrder)
                        .ToListAsync();
                    return View(model);
                }

                // If this is set as default, unset other defaults for this customer
                if (model.IsDefault)
                {
                    var otherDefaults = await _context.BankAccounts
                        .Where(b => b.CustomerId == model.CustomerId && b.IsDefault)
                        .ToListAsync();

                    foreach (var account in otherDefaults)
                    {
                        account.IsDefault = false;
                        _context.BankAccounts.Update(account);
                    }
                }

                model.CreatedAt = DateTime.Now;
                _context.BankAccounts.Add(model);
                await _context.SaveChangesAsync();

                // Send notification about new bank account
                await _adminNotificationService.SendBankAccountNotificationAsync(model, "created");

                TempData["Success"] = $"حساب بانکی {model.BankName} با موفقیت ایجاد شد.";
                return RedirectToAction("CustomerAccounts", new { customerId = model.CustomerId });
            }

            ViewBag.Customers = await _context.Customers
                .Where(c => c.IsActive && c.IsSystem)
                .OrderBy(c => c.FullName)
                .ToListAsync();

            ViewBag.Currencies = await _context.Currencies
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();

            return View(model);
        }

        /// <summary>
        /// View bank account details
        /// نمایش جزئیات حساب بانکی
        /// </summary>
        public async Task<IActionResult> Details(int id)
        {
            var bankAccount = await _context.BankAccounts
                .Include(b => b.Customer)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (bankAccount == null)
            {
                return NotFound();
            }

            return View(bankAccount);
        }

        /// <summary>
        /// Edit bank account
        /// ویرایش حساب بانکی
        /// </summary>
        [Authorize(Roles = "Admin,Programmer")]
        public async Task<IActionResult> Edit(int id)
        {
            var bankAccount = await _context.BankAccounts
                .Include(b => b.Customer)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (bankAccount == null)
            {
                return NotFound();
            }

            ViewBag.Customers = await _context.Customers
                .Where(c => c.IsActive && c.IsSystem)
                .OrderBy(c => c.FullName)
                .ToListAsync();

            ViewBag.Currencies = await _context.Currencies
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();

            return View(bankAccount);
        }

        /// <summary>
        /// Edit bank account POST
        /// ویرایش حساب بانکی POST
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Programmer")]
        public async Task<IActionResult> Edit(int id, BankAccount model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            // Remove Customer validation error since we handle CustomerId separately
            ModelState.Remove("Customer");

            if (ModelState.IsValid)
            {
                // If this is set as default, unset other defaults for this customer
                if (model.IsDefault)
                {
                    var otherDefaults = await _context.BankAccounts
                        .Where(b => b.CustomerId == model.CustomerId && b.Id != id && b.IsDefault)
                        .ToListAsync();

                    foreach (var account in otherDefaults)
                    {
                        account.IsDefault = false;
                        _context.BankAccounts.Update(account);
                    }
                }

                model.LastModified = DateTime.Now;
                _context.BankAccounts.Update(model);
                await _context.SaveChangesAsync();

                // Send notification about bank account update
                await _adminNotificationService.SendBankAccountNotificationAsync(model, "updated");

                TempData["Success"] = $"حساب بانکی {model.BankName} با موفقیت بروزرسانی شد.";
                return RedirectToAction("CustomerAccounts", new { customerId = model.CustomerId });
            }

            ViewBag.Customers = await _context.Customers
                .Where(c => c.IsActive && c.IsSystem)
                .OrderBy(c => c.FullName)
                .ToListAsync();

            ViewBag.Currencies = await _context.Currencies
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();

            return View(model);
        }

        /// <summary>
        /// Delete bank account
        /// حذف حساب بانکی
        /// </summary>
        [Authorize(Roles = "Admin,Programmer")]
        public async Task<IActionResult> Delete(int id)
        {
            var bankAccount = await _context.BankAccounts
                .Include(b => b.Customer)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (bankAccount == null)
            {
                return NotFound();
            }

            // TODO: Check if account is being used in AccountingDocuments in new architecture
            /*
            // Check if account is being used in transactions
            var hasTransactions = await _context.Transactions.AnyAsync(t => t.BuyerBankAccountId == id || t.SellerBankAccountId == id);

            if (hasTransactions)
            {
                TempData["Error"] = "این حساب بانکی در تراکنش‌ها استفاده شده و نمی‌توان آن را حذف کرد.";
                return RedirectToAction("CustomerAccounts", new { customerId = bankAccount.CustomerId });
            }
            */

            return View(bankAccount);
        }

        /// <summary>
        /// Delete bank account POST
        /// حذف حساب بانکی POST
        /// </summary>
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Programmer")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var bankAccount = await _context.BankAccounts.FindAsync(id);
            if (bankAccount == null)
            {
                return NotFound();
            }

            // TODO: Double-check if account is being used in AccountingDocuments in new architecture
            /*
            // Double-check if account is being used

            var hasTransactions = await _context.Transactions.AnyAsync(t => t.BuyerBankAccountId == id || t.SellerBankAccountId == id);

            if (hasTransactions)
            {
                TempData["Error"] = "این حساب بانکی در تراکنش‌ها استفاده شده و نمی‌توان آن را حذف کرد.";
                return RedirectToAction("CustomerAccounts", new { customerId = bankAccount.CustomerId });
            }
            */

            _context.BankAccounts.Remove(bankAccount);
            await _context.SaveChangesAsync();

            TempData["Success"] = "حساب بانکی با موفقیت حذف شد.";
            return RedirectToAction("CustomerAccounts", new { customerId = bankAccount.CustomerId });
        }

        /// <summary>
        /// Get bank accounts for a customer (AJAX)
        /// دریافت حساب‌های بانکی یک مشتری (AJAX)
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin,Programmer")]
        public async Task<IActionResult> GetCustomerBankAccounts(int customerId)
        {
            var bankAccounts = await _context.BankAccounts
                .Where(b => b.CustomerId == customerId && b.IsActive)
                .OrderBy(b => b.IsDefault ? 0 : 1)
                .ThenBy(b => b.BankName)
                .Select(b => new
                {
                    id = b.Id,
                    bankName = b.BankName,
                    accountNumber = b.AccountNumber,
                    accountHolderName = b.AccountHolderName,
                    currencyId = b.CurrencyId,
                    currencyCode = b.Currency != null ? b.Currency.Code : b.CurrencyCode, // Display from navigation
                    isDefault = b.IsDefault
                })
                .ToListAsync();

            return Json(bankAccounts);
        }
    }
}
