using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;

namespace ForexExchange.Controllers
{
    [Authorize(Roles = "Admin,Operator,Programmer")]
    public class CurrenciesController : Controller
    {
        private readonly ForexDbContext _context;
        private readonly ILogger<CurrenciesController> _logger;

        public CurrenciesController(ForexDbContext context, ILogger<CurrenciesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Currencies
        public async Task<IActionResult> Index(bool? onlyActive)
        {
            var query = _context.Currencies.AsQueryable();
            if (onlyActive == true)
            {
                query = query.Where(c => c.IsActive);
            }

            var currencies = await query
                .OrderBy(c => c.RatePriority)
                .ThenBy(c => c.DisplayOrder)
                .ToListAsync();

            ViewBag.OnlyActive = onlyActive == true;
            return View(currencies);
        }

        // GET: Currencies/ManageDisplayOrder
        public async Task<IActionResult> ManageDisplayOrder()
        {
            var currencies = await _context.Currencies
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.Code)
                .ToListAsync();

            ViewData["Title"] = "ترتیب نمایش داشبورد";
            ViewBag.Mode = "DisplayOrder";
            return View("Reorder", currencies);
        }

        // POST: Currencies/UpdateDisplayOrder
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDisplayOrder(List<int> orderedIds)
        {
            if (orderedIds == null || orderedIds.Count == 0)
            {
                TempData["ErrorMessage"] = "لیست ارزها خالی است.";
                return RedirectToAction(nameof(ManageDisplayOrder));
            }

            var currencies = await _context.Currencies
                .Where(c => orderedIds.Contains(c.Id))
                .ToListAsync();

            for (var i = 0; i < orderedIds.Count; i++)
            {
                var currency = currencies.FirstOrDefault(c => c.Id == orderedIds[i]);
                if (currency != null)
                    currency.DisplayOrder = i + 1;
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "ترتیب نمایش داشبورد بروزرسانی شد.";
            return RedirectToAction(nameof(ManageDisplayOrder));
        }

        // GET: Currencies/ManageRatePriority
        public async Task<IActionResult> ManageRatePriority()
        {
            var currencies = await _context.Currencies
                .OrderBy(c => c.RatePriority)
                .ThenBy(c => c.Code)
                .ToListAsync();

            ViewData["Title"] = "قدرت ارز (اولویت نرخ)";
            ViewBag.Mode = "RatePriority";
            return View("Reorder", currencies);
        }

        // POST: Currencies/UpdateRatePriority
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRatePriority(List<int> orderedIds)
        {
            if (orderedIds == null || orderedIds.Count == 0)
            {
                TempData["ErrorMessage"] = "لیست ارزها خالی است.";
                return RedirectToAction(nameof(ManageRatePriority));
            }

            var currencies = await _context.Currencies
                .Where(c => orderedIds.Contains(c.Id))
                .ToListAsync();

            for (var i = 0; i < orderedIds.Count; i++)
            {
                var currency = currencies.FirstOrDefault(c => c.Id == orderedIds[i]);
                if (currency != null)
                    currency.RatePriority = i + 1; // lower number = stronger
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "قدرت ارزها (اولویت نرخ) بروزرسانی شد.";
            return RedirectToAction(nameof(ManageRatePriority));
        }

        // GET: Currencies/Create
        public IActionResult Create()
        {
            return View(new Currency { IsActive = true });
        }

        // POST: Currencies/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Code,Name,PersianName,Symbol,IsActive")] Currency model)
        {
            // Normalize
            model.Code = model.Code?.Trim().ToUpperInvariant() ?? string.Empty;
            model.Name = model.Name?.Trim() ?? string.Empty;
            model.PersianName = model.PersianName?.Trim() ?? string.Empty;
            model.Symbol = model.Symbol?.Trim() ?? string.Empty;

            if (await _context.Currencies.AnyAsync(c => c.Code == model.Code))
            {
                ModelState.AddModelError("Code", "کد ارز باید یکتا باشد.");
            }

            // IRR validation handled separately - only one IRR currency allowed
            if (model.Code == "IRR" && await _context.Currencies.AnyAsync(c => c.Code == "IRR"))
            {
                ModelState.AddModelError("Code", "فقط یک ارز IRR مجاز است.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var maxDisplayOrder = await _context.Currencies.MaxAsync(c => (int?)c.DisplayOrder) ?? 0;
            var maxRatePriority = await _context.Currencies.MaxAsync(c => (int?)c.RatePriority) ?? 0;
            model.DisplayOrder = maxDisplayOrder + 1;
            model.RatePriority = maxRatePriority + 1;
            model.CreatedAt = DateTime.Now;
            _context.Currencies.Add(model);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "ارز با موفقیت ایجاد شد. ترتیب نمایش و قدرت را از صفحات مربوطه تنظیم کنید.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Currencies/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var currency = await _context.Currencies.FindAsync(id);
            if (currency == null) return NotFound();

            return View(currency);
        }

        // POST: Currencies/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Code,Name,PersianName,Symbol,IsActive,CreatedAt")] Currency model)
        {
            if (id != model.Id) return NotFound();

            // Normalize
            model.Code = model.Code?.Trim().ToUpperInvariant() ?? string.Empty;
            model.Name = model.Name?.Trim() ?? string.Empty;
            model.PersianName = model.PersianName?.Trim() ?? string.Empty;
            model.Symbol = model.Symbol?.Trim() ?? string.Empty;

            if (await _context.Currencies.AnyAsync(c => c.Code == model.Code && c.Id != model.Id))
            {
                ModelState.AddModelError("Code", "کد ارز باید یکتا باشد.");
            }

            // IRR validation handled separately - only one IRR currency allowed
            if (model.Code == "IRR" && await _context.Currencies.AnyAsync(c => c.Code == "IRR" && c.Id != model.Id))
            {
                ModelState.AddModelError("Code", "فقط یک ارز IRR مجاز است.");
            }

            // Prevent deactivating IRR currency
            if (!model.IsActive && model.Code == "IRR")
            {
                ModelState.AddModelError("IsActive", "غیرفعال کردن ارز IRR مجاز نیست.");
            }

            if (!ModelState.IsValid)
            {
                var current = await _context.Currencies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
                if (current != null)
                {
                    model.DisplayOrder = current.DisplayOrder;
                    model.RatePriority = current.RatePriority;
                    model.IsActive = current.IsActive;
                }
                return View(model);
            }

            try
            {
                var existing = await _context.Currencies.FindAsync(id);
                if (existing == null) return NotFound();

                // Keep DisplayOrder / RatePriority — managed on dedicated reorder pages
                existing.Code = model.Code;
                existing.Name = model.Name;
                existing.PersianName = model.PersianName;
                existing.Symbol = model.Symbol;
                existing.IsActive = model.IsActive;

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "اطلاعات ارز بروزرسانی شد.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Currencies.AnyAsync(c => c.Id == model.Id))
                    return NotFound();
                throw;
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Currencies/ToggleActive/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var currency = await _context.Currencies.FindAsync(id);
            if (currency == null) return NotFound();

            if (currency.Code == "IRR" && currency.IsActive == true)
            {
                TempData["ErrorMessage"] = "غیرفعال کردن ارز IRR مجاز نیست.";
                return RedirectToAction(nameof(Index));
            }

            currency.IsActive = !currency.IsActive;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = currency.IsActive ? "ارز فعال شد." : "ارز غیرفعال شد.";
            return RedirectToAction(nameof(Index));
        }
    }
}

