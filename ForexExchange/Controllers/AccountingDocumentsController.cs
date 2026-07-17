using System.Globalization;
using System.IO;
using System.Text;
using ForexExchange.Helpers;
using ForexExchange.Models;
using ForexExchange.Services;
using ForexExchange.Services.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ForexExchange.Authorization; // Add for custom permissions


namespace ForexExchange.Controllers
{
    [Authorize]
    [HasPermission(Permissions.Documents_View)] // Or a new specific permission for managing role permissions
    public class AccountingDocumentsController : Controller
    {
        private readonly ForexDbContext _context;
        private readonly ICustomerBalanceService _customerBalanceService;
        private readonly IBankAccountBalanceService _bankAccountBalanceService;
        private readonly IOcrService _ocrService;
        private readonly AdminNotificationService _adminNotificationService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationHub _notificationHub;
        private readonly ICentralFinancialService _centralFinancialService;
        private readonly ICsvImportService _csvImportService;
        private readonly ILogger<AccountingDocumentsController> _logger;

        public AccountingDocumentsController(
            ForexDbContext context,
            ICustomerBalanceService customerBalanceService,
            IBankAccountBalanceService bankAccountBalanceService,
            IOcrService ocrService,
            AdminNotificationService adminNotificationService,
            UserManager<ApplicationUser> userManager,
            INotificationHub notificationHub,
            ICentralFinancialService centralFinancialService,
            ICsvImportService csvImportService,
            ILogger<AccountingDocumentsController> logger
        )
        {
            _context = context;
            _customerBalanceService = customerBalanceService;
            _bankAccountBalanceService = bankAccountBalanceService;
            _ocrService = ocrService;
            _adminNotificationService = adminNotificationService;
            _userManager = userManager;
            _notificationHub = notificationHub;
            _centralFinancialService = centralFinancialService;
            _csvImportService = csvImportService;
            _logger = logger;
        }

        // GET: AccountingDocuments
        [HasPermission(Permissions.Documents_View)] 
        public async Task<IActionResult> Index(
            string sortOrder,
            string currentFilter,
            string searchString,
            string referenceNumber,
            int? customerFilter,
            DocumentType? typeFilter,
            bool? statusFilter,
            int? page
        )
        {
            ViewData["CurrentSort"] = sortOrder;
            ViewData["IdSortParm"] = String.IsNullOrEmpty(sortOrder) ? "id_desc" : "";
            ViewData["TitleSortParm"] = sortOrder == "title" ? "title_desc" : "title";
            ViewData["TypeSortParm"] = sortOrder == "type" ? "type_desc" : "type";
            ViewData["CustomerSortParm"] = sortOrder == "customer" ? "customer_desc" : "customer";
            ViewData["AmountSortParm"] = sortOrder == "amount" ? "amount_desc" : "amount";
            ViewData["DateSortParm"] = sortOrder == "date" ? "date_desc" : "date";
            ViewData["StatusSortParm"] = sortOrder == "status" ? "status_desc" : "status";

            if (searchString != null)
            {
                page = 1;
            }
            else
            {
                searchString = currentFilter;
            }

            ViewData["CurrentFilter"] = searchString;
            ViewData["ReferenceFilter"] = referenceNumber;
            ViewData["CustomerFilter"] = customerFilter;
            ViewData["TypeFilter"] = typeFilter;
            ViewData["StatusFilter"] = statusFilter;

            var documents = _context
                .AccountingDocuments.Include(d => d.PayerCustomer)
                .Include(d => d.ReceiverCustomer)
                .Include(d => d.PayerBankAccount)
                .Include(d => d.ReceiverBankAccount)
                .AsQueryable();

            // Apply filters
            if (!String.IsNullOrEmpty(searchString))
            {
                // Search by document ID
                if (int.TryParse(searchString, out int documentId))
                {
                    documents = documents.Where(d => d.Id == documentId);
                }
                else
                {
                    // If not a valid integer, return no results
                    documents = documents.Where(d => false);
                }
            }

            // Filter by reference number
            if (!String.IsNullOrEmpty(referenceNumber))
            {
                documents = documents.Where(d =>
                    !string.IsNullOrEmpty(d.ReferenceNumber)
                    && d.ReferenceNumber.Contains(referenceNumber)
                );
            }

            if (customerFilter.HasValue)
            {
                documents = documents.Where(d =>
                    d.PayerCustomerId == customerFilter || d.ReceiverCustomerId == customerFilter
                );
            }

            if (typeFilter.HasValue)
            {
                documents = documents.Where(d => d.Type == typeFilter);
            }

            if (statusFilter.HasValue)
            {
                documents = documents.Where(d => d.IsVerified == statusFilter);
            }

            // Apply sorting
            switch (sortOrder)
            {
                case "id_desc":
                    documents = documents.OrderByDescending(d => d.Id);
                    break;
                case "title":
                    documents = documents.OrderBy(d => d.Title);
                    break;
                case "title_desc":
                    documents = documents.OrderByDescending(d => d.Title);
                    break;
                case "type":
                    documents = documents.OrderBy(d => d.Type);
                    break;
                case "type_desc":
                    documents = documents.OrderByDescending(d => d.Type);
                    break;
                case "customer":
                    documents = documents.OrderBy(d =>
                        d.Customer != null ? d.Customer.FullName : ""
                    );
                    break;
                case "customer_desc":
                    documents = documents.OrderByDescending(d =>
                        d.Customer != null ? d.Customer.FullName : ""
                    );
                    break;
                case "amount":
                    documents = documents.OrderBy(d => d.Amount);
                    break;
                case "amount_desc":
                    documents = documents.OrderByDescending(d => d.Amount);
                    break;
                case "date":
                    documents = documents.OrderBy(d => d.DocumentDate);
                    break;
                case "date_desc":
                    documents = documents.OrderByDescending(d => d.DocumentDate);
                    break;
                case "status":
                    documents = documents.OrderBy(d => d.IsVerified);
                    break;
                case "status_desc":
                    documents = documents.OrderByDescending(d => d.IsVerified);
                    break;
                default:
                    documents = documents.OrderByDescending(d => d.CreatedAt);
                    break;
            }

            // Pagination
            int pageSize = 6; // 6 items per page
            int pageNumber = (page ?? 1);

            // Get total count before pagination
            int totalItems = await documents.CountAsync();

            // Apply pagination and exclude FileData to prevent memory leak
            var pagedDocuments = await documents
                .Select(d => new AccountingDocument
                {
                    Id = d.Id,
                    Type = d.Type,
                    PayerType = d.PayerType,
                    PayerCustomerId = d.PayerCustomerId,
                    PayerBankAccountId = d.PayerBankAccountId,
                    ReceiverType = d.ReceiverType,
                    ReceiverCustomerId = d.ReceiverCustomerId,
                    ReceiverBankAccountId = d.ReceiverBankAccountId,
                    Amount = d.Amount,
                    CurrencyId = d.CurrencyId,
                    CurrencyCode = d.Currency != null ? d.Currency.Code : d.CurrencyCode, // Display from navigation
                    Title = d.Title,
                    Description = d.Description,
                    DocumentDate = d.DocumentDate,
                    CreatedAt = d.CreatedAt,
                    IsVerified = d.IsVerified,
                    VerifiedAt = d.VerifiedAt,
                    VerifiedBy = d.VerifiedBy,
                    ReferenceNumber = d.ReferenceNumber,
                    FileName = d.FileName,
                    ContentType = d.ContentType,
                    // FileData is excluded to prevent memory leak
                    Notes = d.Notes,
                    IsDeleted = d.IsDeleted,
                    DeletedAt = d.DeletedAt,
                    DeletedBy = d.DeletedBy,
                    IsFrozen = d.IsFrozen,
                    PayerCustomer = d.PayerCustomer,
                    ReceiverCustomer = d.ReceiverCustomer,
                    PayerBankAccount = d.PayerBankAccount,
                    ReceiverBankAccount = d.ReceiverBankAccount,
                })
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Pass pagination info to view
            ViewBag.CurrentPage = pageNumber;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;
            ViewBag.HasPreviousPage = pageNumber > 1;
            ViewBag.HasNextPage = pageNumber < ViewBag.TotalPages;

            // Load customers list for filter dropdown
            var customers = await _context
                .Customers.Where(c => c.IsActive && !c.IsSystem)
                .OrderBy(c => c.FullName)
                .ToListAsync();

            ViewBag.CustomersList = customers
                .Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.FullName,
                })
                .ToList();

            return View(pagedDocuments);
        }

        // GET: AccountingDocuments/Details/5
        [HasPermission(Permissions.Documents_Detail)] 
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var accountingDocument = await _context
                .AccountingDocuments.Include(a => a.PayerCustomer)
                .Include(a => a.ReceiverCustomer)
                .Include(a => a.PayerBankAccount)
                .Include(a => a.ReceiverBankAccount)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (accountingDocument == null)
            {
                return NotFound();
            }

            // Convert FileData to base64 for display in view
            if (accountingDocument.FileData != null && accountingDocument.FileData.Length > 0)
            {
                ViewData["FileDataBase64"] = Convert.ToBase64String(accountingDocument.FileData);
            }
            else
            {
                ViewData["FileDataBase64"] = null;
            }

            return View(accountingDocument);
        }

        // GET: AccountingDocuments/CustomerStatement/5
       
        public async Task<IActionResult> CustomerStatement(int? customerId, int? documentId = null)
        {
            if (customerId == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == customerId);

            if (customer == null)
            {
                return NotFound();
            }

            // Get all accounting documents for this customer (excluding FileData to prevent memory leak)
            var documents = await _context
                .AccountingDocuments.Include(a => a.PayerCustomer)
                .Include(a => a.ReceiverCustomer)
                .Include(a => a.PayerBankAccount)
                .Include(a => a.ReceiverBankAccount)
                .Where(a => a.PayerCustomerId == customerId || a.ReceiverCustomerId == customerId)
                .Select(a => new AccountingDocument
                {
                    Id = a.Id,
                    Type = a.Type,
                    PayerType = a.PayerType,
                    PayerCustomerId = a.PayerCustomerId,
                    PayerBankAccountId = a.PayerBankAccountId,
                    ReceiverType = a.ReceiverType,
                    ReceiverCustomerId = a.ReceiverCustomerId,
                    ReceiverBankAccountId = a.ReceiverBankAccountId,
                    Amount = a.Amount,
                    CurrencyId = a.CurrencyId,
                    CurrencyCode = a.Currency != null ? a.Currency.Code : a.CurrencyCode, // Display from navigation
                    Title = a.Title,
                    Description = a.Description,
                    DocumentDate = a.DocumentDate,
                    CreatedAt = a.CreatedAt,
                    IsVerified = a.IsVerified,
                    VerifiedAt = a.VerifiedAt,
                    VerifiedBy = a.VerifiedBy,
                    ReferenceNumber = a.ReferenceNumber,
                    FileName = a.FileName,
                    ContentType = a.ContentType,
                    // FileData is excluded to prevent memory leak
                    Notes = a.Notes,
                    IsDeleted = a.IsDeleted,
                    DeletedAt = a.DeletedAt,
                    DeletedBy = a.DeletedBy,
                    IsFrozen = a.IsFrozen,
                    PayerCustomer = a.PayerCustomer,
                    ReceiverCustomer = a.ReceiverCustomer,
                    PayerBankAccount = a.PayerBankAccount,
                    ReceiverBankAccount = a.ReceiverBankAccount,
                })
                .OrderByDescending(a => a.DocumentDate)
                .ToListAsync();

            // Get customer balance
            var balances = await _customerBalanceService.GetCustomerBalancesAsync(customerId.Value);

            var viewModel = new CustomerStatementViewModel
            {
                Customer = customer,
                Documents = documents,
                Balances = balances,
                StatementDate = DateTime.Now,
            };

            // Pass document ID for back navigation
            if (documentId.HasValue)
            {
                ViewBag.DocumentId = documentId.Value;
            }

            return View(viewModel);
        }

        // GET: AccountingDocuments/Upload
        public IActionResult Upload()
        {
            ViewData["Customers"] = _context
                .Customers.Where(c => c.IsActive && !c.IsSystem)
                .ToList();
            ViewData["Currencies"] = _context.Currencies.Where(c => c.IsActive).ToList();
            ViewData["BankAccounts"] = _context.BankAccounts.ToList();
            return View();
        }

        // POST: AccountingDocuments/Upload
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(
            AccountingDocument accountingDocument,
            IFormFile documentFile
        )
        {
            // Check if this is an AJAX request
            bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

            // DETAILED LOGGING FOR DEBUGGING
            _logger.LogInformation("=== Upload POST Action Started ===");
            _logger.LogInformation($"Is AJAX Request: {isAjax}");
            _logger.LogInformation($"Request Content Type: {Request.ContentType}");
            _logger.LogInformation($"Request Method: {Request.Method}");

            // Log all headers
            foreach (var header in Request.Headers)
            {
                _logger.LogInformation($"Header: {header.Key} = {header.Value}");
            }

            // Log form data
            _logger.LogInformation($"Form Count: {Request.Form.Count}");
            foreach (var formItem in Request.Form)
            {
                _logger.LogInformation($"Form Field: {formItem.Key} = {formItem.Value}");
            }

            // Log file information
            _logger.LogInformation($"Files Count: {Request.Form.Files.Count}");
            foreach (var file in Request.Form.Files)
            {
                _logger.LogInformation(
                    $"File: {file.Name}, FileName: {file.FileName}, Size: {file.Length}, ContentType: {file.ContentType}"
                );
            }

            // Try to get file from Request.Form.Files if not bound via parameter
            if (documentFile == null && Request.Form.Files.Count > 0)
            {
                // Try to find file with name "documentFile"
                var fileFromRequest = Request.Form.Files.FirstOrDefault(f =>
                    f.Name == "documentFile"
                );
                if (fileFromRequest != null && fileFromRequest.Length > 0)
                {
                    documentFile = fileFromRequest;
                    _logger.LogInformation(
                        $"File retrieved from Request.Form.Files: {documentFile.FileName} ({documentFile.Length} bytes)"
                    );
                }
                else if (Request.Form.Files.Count == 1)
                {
                    // If only one file, use it
                    documentFile = Request.Form.Files[0];
                    _logger.LogInformation(
                        $"Single file retrieved from Request.Form.Files: {documentFile.FileName} ({documentFile.Length} bytes)"
                    );
                }
            }

            // Log model state
            _logger.LogInformation($"Model State Valid: {ModelState.IsValid}");
            _logger.LogInformation($"Model State Error Count: {ModelState.ErrorCount}");

            // Log AccountingDocument properties
            _logger.LogInformation($"AccountingDocument - Type: {accountingDocument.Type}");
            _logger.LogInformation(
                $"AccountingDocument - PayerType: {accountingDocument.PayerType}"
            );
            _logger.LogInformation(
                $"AccountingDocument - ReceiverType: {accountingDocument.ReceiverType}"
            );
            _logger.LogInformation($"AccountingDocument - Amount: {accountingDocument.Amount}");
            _logger.LogInformation(
                $"AccountingDocument - CurrencyCode: {accountingDocument.CurrencyCode}"
            );
            _logger.LogInformation(
                $"AccountingDocument - PayerCustomerId: {accountingDocument.PayerCustomerId}"
            );
            _logger.LogInformation(
                $"AccountingDocument - ReceiverCustomerId: {accountingDocument.ReceiverCustomerId}"
            );
            _logger.LogInformation($"DocumentFile null: {documentFile == null}");
            if (documentFile != null)
            {
                _logger.LogInformation(
                    $"DocumentFile - Name: {documentFile.Name}, FileName: {documentFile.FileName}, Size: {documentFile.Length}"
                );
            }

            // Remove validation error for documentFile since it's optional
            if (ModelState.ContainsKey("documentFile"))
            {
                ModelState.Remove("documentFile");
            }

            // File is now optional, but if provided, validate it
            if (documentFile != null && documentFile.Length > 0)
            {
                // Validate file size (max 10MB)
                if (documentFile.Length > 10 * 1024 * 1024)
                {
                    ModelState.AddModelError(
                        "documentFile",
                        "حجم فایل نمی‌تواند بیشتر از 10 مگابایت باشد."
                    );

                    if (isAjax)
                    {
                        return Json(
                            new
                            {
                                success = false,
                                message = "حجم فایل نمی‌تواند بیشتر از 10 مگابایت باشد.",
                                errors = GetModelStateErrors(),
                            }
                        );
                    }

                    TempData["ErrorMessage"] = "حجم فایل نمی‌تواند بیشتر از 10 مگابایت باشد.";
                    ViewData["Customers"] = _context
                        .Customers.Where(c => c.IsActive && !c.IsSystem)
                        .ToList();
                    ViewData["Currencies"] = _context.Currencies.Where(c => c.IsActive).ToList();
                    ViewData["BankAccounts"] = _context.BankAccounts.ToList();
                    return View(accountingDocument);
                }

                // Validate file type
                var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx" };
                var fileExtension = Path.GetExtension(documentFile.FileName).ToLower();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    ModelState.AddModelError(
                        "documentFile",
                        "فرمت فایل مجاز نیست. فرمت‌های مجاز: PDF, JPG, PNG, DOC, DOCX"
                    );

                    if (isAjax)
                    {
                        return Json(
                            new
                            {
                                success = false,
                                message = "فرمت فایل مجاز نیست. فرمت‌های مجاز: PDF, JPG, PNG, DOC, DOCX",
                                errors = GetModelStateErrors(),
                            }
                        );
                    }

                    TempData["ErrorMessage"] =
                        "فرمت فایل مجاز نیست. فرمت‌های مجاز: PDF, JPG, PNG, DOC, DOCX";
                    ViewData["Customers"] = _context
                        .Customers.Where(c => c.IsActive && !c.IsSystem)
                        .ToList();
                    ViewData["Currencies"] = _context.Currencies.Where(c => c.IsActive).ToList();
                    ViewData["BankAccounts"] = _context.BankAccounts.ToList();
                    return View(accountingDocument);
                }
            }

            // Validate CurrencyId is required FIRST (no fallback to CurrencyCode)
            if (!accountingDocument.CurrencyId.HasValue)
            {
                ModelState.AddModelError(
                    "CurrencyId",
                    "CurrencyId الزامی است. لطفاً ارز را انتخاب کنید."
                );
            }
            else
            {
                // Populate CurrencyCode from CurrencyId for backward compatibility BEFORE validation
                var currency = await _context.Currencies.FindAsync(
                    accountingDocument.CurrencyId.Value
                );
                if (currency == null)
                {
                    ModelState.AddModelError("CurrencyId", "ارز مورد نظر یافت نشد.");
                }
                else
                {
                    // Set CurrencyCode from Currency for backward compatibility
                    accountingDocument.CurrencyCode = currency.Code ?? "";
                }
            }

            // Validate bank account currency match using CurrencyId
            // Check payer bank account
            if (
                accountingDocument.PayerBankAccountId.HasValue
                && accountingDocument.CurrencyId.HasValue
            )
            {
                var payerBankAccount = await _context
                    .BankAccounts.Include(ba => ba.Currency)
                    .FirstOrDefaultAsync(ba =>
                        ba.Id == accountingDocument.PayerBankAccountId.Value
                    );
                if (
                    payerBankAccount != null
                    && payerBankAccount.CurrencyId != accountingDocument.CurrencyId
                )
                {
                    var payerCurrencyCode =
                        payerBankAccount.Currency != null
                            ? payerBankAccount.Currency.Code
                            : payerBankAccount.CurrencyCode ?? "";
                    var docCurrencyCode = accountingDocument.CurrencyCode ?? "";
                    ModelState.AddModelError(
                        "PayerBankAccountId",
                        $"ارز حساب بانکی پرداخت کننده ({payerCurrencyCode}) با ارز سند ({docCurrencyCode}) مطابقت ندارد."
                    );
                }
            }

            // Check receiver bank account
            if (
                accountingDocument.ReceiverBankAccountId.HasValue
                && accountingDocument.CurrencyId.HasValue
            )
            {
                var receiverBankAccount = await _context
                    .BankAccounts.Include(ba => ba.Currency)
                    .FirstOrDefaultAsync(ba =>
                        ba.Id == accountingDocument.ReceiverBankAccountId.Value
                    );
                if (
                    receiverBankAccount != null
                    && receiverBankAccount.CurrencyId != accountingDocument.CurrencyId
                )
                {
                    var receiverCurrencyCode =
                        receiverBankAccount.Currency != null
                            ? receiverBankAccount.Currency.Code
                            : receiverBankAccount.CurrencyCode ?? "";
                    var docCurrencyCode = accountingDocument.CurrencyCode ?? "";
                    ModelState.AddModelError(
                        "ReceiverBankAccountId",
                        $"ارز حساب بانکی دریافت کننده ({receiverCurrencyCode}) با ارز سند ({docCurrencyCode}) مطابقت ندارد."
                    );
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    accountingDocument.CreatedAt = DateTime.Now;

                    // Handle file upload only if a file is provided
                    if (documentFile != null && documentFile.Length > 0)
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            await documentFile.CopyToAsync(memoryStream);
                            accountingDocument.FileData = memoryStream.ToArray();
                            // Save original filename (without path) to preserve the original file name
                            accountingDocument.FileName = Path.GetFileName(documentFile.FileName);
                            accountingDocument.ContentType = documentFile.ContentType;
                        }
                    }

                    _context.Add(accountingDocument);

                    try
                    {
                        await _context.SaveChangesAsync();
                    }
                    catch (DbUpdateException dbEx)
                    {
                        // Log specific database exceptions with full details
                        _logger.LogError(
                            dbEx,
                            "Database error creating accounting document. ExceptionType: {ExceptionType}, Message: {Message}",
                            dbEx.GetType().Name,
                            dbEx.Message
                        );

                        if (dbEx.InnerException != null)
                        {
                            _logger.LogError(
                                "Inner Exception: {InnerExceptionType} - {InnerExceptionMessage}",
                                dbEx.InnerException.GetType().Name,
                                dbEx.InnerException.Message
                            );

                            // Check for specific SQLite errors
                            var innerMessage = dbEx.InnerException.Message ?? "";
                            if (
                                innerMessage.Contains(
                                    "too large",
                                    StringComparison.OrdinalIgnoreCase
                                )
                                || innerMessage.Contains(
                                    "entity is too large",
                                    StringComparison.OrdinalIgnoreCase
                                )
                                || innerMessage.Contains(
                                    "String or BLOB exceeds",
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                            {
                                var errorMsg =
                                    "حجم داده‌های سند بسیار بزرگ است. لطفاً فایل کوچکتری انتخاب کنید یا فایل را حذف کنید.";
                                _logger.LogWarning(
                                    "Entity too large error detected. File size: {FileSize} bytes",
                                    documentFile?.Length ?? 0
                                );

                                if (isAjax)
                                {
                                    return Json(new { success = false, message = errorMsg });
                                }

                                TempData["ErrorMessage"] = errorMsg;
                                ViewData["Customers"] = _context
                                    .Customers.Where(c => c.IsActive && !c.IsSystem)
                                    .ToList();
                                ViewData["Currencies"] = _context
                                    .Currencies.Where(c => c.IsActive)
                                    .ToList();
                                ViewData["BankAccounts"] = _context.BankAccounts.ToList();
                                return View(accountingDocument);
                            }
                        }

                        throw; // Re-throw if not handled
                    }

                    // Send notifications through central hub (idempotent - failures don't affect main operation)
                    var currentUser = await _userManager.GetUserAsync(User);
                    if (currentUser != null)
                    {
                        try
                        {
                            await _notificationHub.SendAccountingDocumentNotificationAsync(
                                accountingDocument,
                                NotificationEventType.AccountingDocumentCreated,
                                currentUser.Id
                            );
                        }
                        catch (Exception notifEx)
                        {
                            // Idempotent: Log but don't fail the operation
                            _logger.LogWarning(
                                notifEx,
                                "Failed to send notification for document {DocumentId}, but document was created successfully. ExceptionType: {ExceptionType}",
                                accountingDocument.Id,
                                notifEx.GetType().Name
                            );
                        }
                    }

                    _logger.LogInformation(
                        "Accounting document created successfully. ID: {DocumentId}, User: {User}",
                        accountingDocument.Id,
                        User.Identity?.Name
                    );

                    if (isAjax)
                    {
                        return Json(
                            new
                            {
                                success = true,
                                message = "سند حسابداری با موفقیت ثبت شد.",
                                documentId = accountingDocument.Id,
                                redirectUrl = Url.Action("Index", "AccountingDocuments"),
                            }
                        );
                    }

                    TempData["SuccessMessage"] = "سند حسابداری با موفقیت ثبت شد.";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    // Enhanced error logging with full exception details
                    _logger.LogError(
                        ex,
                        "Error creating accounting document. User: {User}, ExceptionType: {ExceptionType}, Message: {Message}",
                        User.Identity?.Name,
                        ex.GetType().Name,
                        ex.Message
                    );

                    // Log inner exception if exists
                    if (ex.InnerException != null)
                    {
                        _logger.LogError(
                            "Inner Exception: {InnerExceptionType} - {InnerExceptionMessage}",
                            ex.InnerException.GetType().Name,
                            ex.InnerException.Message
                        );
                    }

                    // Log stack trace for debugging
                    _logger.LogError("Stack Trace: {StackTrace}", ex.StackTrace);

                    if (isAjax)
                    {
                        return Json(
                            new
                            {
                                success = false,
                                message = "خطا در ثبت سند حسابداری. لطفاً دوباره تلاش کنید.",
                                error = ex.Message,
                            }
                        );
                    }

                    TempData["ErrorMessage"] = "خطا در ثبت سند حسابداری. لطفاً دوباره تلاش کنید.";
                }
            }

            // Return validation errors for AJAX requests
            if (isAjax)
            {
                return Json(
                    new
                    {
                        success = false,
                        message = "لطفاً خطاهای اعتبارسنجی را بررسی کنید.",
                        errors = GetModelStateErrors(),
                    }
                );
            }

            ViewData["Customers"] = _context
                .Customers.Where(c => c.IsActive && !c.IsSystem)
                .ToList();
            ViewData["Currencies"] = _context.Currencies.Where(c => c.IsActive).ToList();
            ViewData["BankAccounts"] = _context.BankAccounts.ToList();
            return View(accountingDocument);
        }

        /// <summary>
        /// Helper method to extract ModelState errors for AJAX responses
        /// </summary>
        private List<string> GetModelStateErrors()
        {
            var errors = new List<string>();
            foreach (var modelState in ModelState.Values)
            {
                foreach (var error in modelState.Errors)
                {
                    if (!string.IsNullOrEmpty(error.ErrorMessage))
                    {
                        errors.Add(error.ErrorMessage);
                    }
                }
            }
            return errors;
        }

        // GET: AccountingDocuments/Edit/5
        [HasPermission(Permissions.Documents_Edit)] 
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var accountingDocument = await _context.AccountingDocuments.FindAsync(id);
            if (accountingDocument == null)
            {
                return NotFound();
            }

            ViewData["Customers"] = _context
                .Customers.Where(c => c.IsActive && !c.IsSystem)
                .ToList();
            ViewData["Currencies"] = _context.Currencies.Where(c => c.IsActive).ToList();
            ViewData["BankAccounts"] = _context.BankAccounts.ToList();
            return View(accountingDocument);
        }

        // POST: AccountingDocuments/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [HasPermission(Permissions.Documents_Edit)] 
        public async Task<IActionResult> Edit(
            int id,
            AccountingDocument accountingDocument,
            IFormFile documentFile
        )
        {
            if (id != accountingDocument.Id)
            {
                return NotFound();
            }

            // Remove validation error for documentFile since it's optional
            if (ModelState.ContainsKey("documentFile"))
            {
                ModelState.Remove("documentFile");
            }

            // Validate bank account currency match
            // Check payer bank account
            if (accountingDocument.PayerBankAccountId.HasValue)
            {
                var payerBankAccount = await _context.BankAccounts.FindAsync(
                    accountingDocument.PayerBankAccountId.Value
                );
                if (
                    payerBankAccount != null
                    && payerBankAccount.CurrencyCode != accountingDocument.CurrencyCode
                )
                {
                    ModelState.AddModelError(
                        "PayerBankAccountId",
                        $"ارز حساب بانکی پرداخت کننده ({payerBankAccount.CurrencyCode}) با ارز سند ({accountingDocument.CurrencyCode}) مطابقت ندارد."
                    );
                }
            }

            // Check receiver bank account
            if (accountingDocument.ReceiverBankAccountId.HasValue)
            {
                var receiverBankAccount = await _context
                    .BankAccounts.Include(ba => ba.Currency)
                    .FirstOrDefaultAsync(ba =>
                        ba.Id == accountingDocument.ReceiverBankAccountId.Value
                    );
                if (
                    receiverBankAccount != null
                    && receiverBankAccount.CurrencyId != accountingDocument.CurrencyId
                )
                {
                    var receiverCurrencyCode =
                        receiverBankAccount.Currency != null
                            ? receiverBankAccount.Currency.Code
                            : receiverBankAccount.CurrencyCode;
                    var docCurrencyCode =
                        accountingDocument.Currency != null
                            ? accountingDocument.Currency.Code
                            : accountingDocument.CurrencyCode;
                    ModelState.AddModelError(
                        "ReceiverBankAccountId",
                        $"ارز حساب بانکی دریافت کننده ({receiverCurrencyCode}) با ارز سند ({docCurrencyCode}) مطابقت ندارد."
                    );
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Get the existing document to check for verification status changes
                    var existingDocument = await _context
                        .AccountingDocuments.AsNoTracking()
                        .FirstOrDefaultAsync(d => d.Id == id);

                    if (existingDocument == null)
                    {
                        return NotFound();
                    }

                    // Validate CurrencyId is required (no fallback to CurrencyCode)
                    if (!accountingDocument.CurrencyId.HasValue)
                    {
                        ModelState.AddModelError(
                            "CurrencyId",
                            "CurrencyId الزامی است. لطفاً ارز را انتخاب کنید."
                        );
                        ViewData["Customers"] = _context
                            .Customers.Where(c => c.IsActive && !c.IsSystem)
                            .ToList();
                        ViewData["Currencies"] = _context
                            .Currencies.Where(c => c.IsActive)
                            .ToList();
                        ViewData["BankAccounts"] = _context.BankAccounts.ToList();
                        return View(accountingDocument);
                    }

                    // Handle file upload if a new file is provided
                    if (documentFile != null && documentFile.Length > 0)
                    {
                        // Validate file size (10MB max)
                        if (documentFile.Length > 10 * 1024 * 1024)
                        {
                            ModelState.AddModelError(
                                "documentFile",
                                "حجم فایل نمی‌تواند بیشتر از ۱۰ مگابایت باشد."
                            );
                            ViewData["Customers"] = _context
                                .Customers.Where(c => c.IsActive && !c.IsSystem)
                                .ToList();
                            ViewData["Currencies"] = _context
                                .Currencies.Where(c => c.IsActive)
                                .ToList();
                            ViewData["BankAccounts"] = _context.BankAccounts.ToList();
                            return View(accountingDocument);
                        }

                        // Validate file type
                        var allowedExtensions = new[]
                        {
                            ".pdf",
                            ".jpg",
                            ".jpeg",
                            ".png",
                            ".doc",
                            ".docx",
                        };
                        var fileExtension = Path.GetExtension(documentFile.FileName).ToLower();
                        if (!allowedExtensions.Contains(fileExtension))
                        {
                            ModelState.AddModelError(
                                "documentFile",
                                "فرمت فایل مجاز نیست. فرمت‌های مجاز: PDF, JPG, PNG, DOC, DOCX"
                            );
                            ViewData["Customers"] = _context
                                .Customers.Where(c => c.IsActive && !c.IsSystem)
                                .ToList();
                            ViewData["Currencies"] = _context
                                .Currencies.Where(c => c.IsActive)
                                .ToList();
                            ViewData["BankAccounts"] = _context.BankAccounts.ToList();
                            return View(accountingDocument);
                        }

                        // Read file data into byte array
                        using (var memoryStream = new MemoryStream())
                        {
                            await documentFile.CopyToAsync(memoryStream);
                            accountingDocument.FileData = memoryStream.ToArray();
                        }

                        // Update document properties - save original filename (without path)
                        accountingDocument.FileName = Path.GetFileName(documentFile.FileName);
                        accountingDocument.ContentType = documentFile.ContentType;
                    }

                    // Handle verification status changes
                    if (accountingDocument.IsVerified != existingDocument.IsVerified)
                    {
                        if (accountingDocument.IsVerified)
                        {
                            // Document is being verified
                            accountingDocument.VerifiedAt = DateTime.Now;
                            accountingDocument.VerifiedBy = User.Identity?.Name ?? "System";
                        }
                        else
                        {
                            // Document is being un-verified (admin action)
                            // Note: In a real system, you might want to reverse the balance changes
                            // For now, we'll just update the status
                            accountingDocument.VerifiedAt = null;
                            accountingDocument.VerifiedBy = null;
                        }
                    }

                    // CRITICAL: Save verification status FIRST (independent transaction)
                    // This ensures IsVerified is saved even if history rebuild fails
                    _context.Update(accountingDocument);
                    await _context.SaveChangesAsync();

                    // AFTER saving, update balances through centralized service (includes history recording)
                    // Now the document is saved with IsVerified=true, so rebuild will include it
                    if (accountingDocument.IsVerified && !existingDocument.IsVerified)
                    {
                        await _centralFinancialService.ProcessAccountingDocumentAsync(
                            accountingDocument
                        );
                        // Note: Currency pools are NOT updated on document verification
                        // Currency pools are only affected by actual currency trading operations
                    }

                    // Send appropriate notification based on what changed
                    if (accountingDocument.IsVerified && !existingDocument.IsVerified)
                    {
                        // Document was just confirmed
                        await _adminNotificationService.SendDocumentNotificationAsync(
                            accountingDocument,
                            "confirmed"
                        );
                    }
                    else
                    {
                        // Document was updated
                        await _adminNotificationService.SendDocumentNotificationAsync(
                            accountingDocument,
                            "updated"
                        );
                    }

                    TempData["SuccessMessage"] = "سند حسابداری با موفقیت ویرایش شد.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AccountingDocumentExists(accountingDocument.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }

            ViewData["Customers"] = _context
                .Customers.Where(c => c.IsActive && !c.IsSystem)
                .ToList();
            ViewData["Currencies"] = _context.Currencies.Where(c => c.IsActive).ToList();
            ViewData["BankAccounts"] = _context.BankAccounts.ToList();
            return View(accountingDocument);
        }

        // POST: AccountingDocuments/Confirm/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [HasPermission(Permissions.Documents_Confirm)] 
        public async Task<IActionResult> Confirm(int id)
        {
            // Check if this is an AJAX request
            bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

            try
            {
                // Load the actual entity (not using Select) so we can update only specific fields without affecting FileData
                var accountingDocument = await _context
                    .AccountingDocuments.Include(a => a.PayerCustomer)
                    .Include(a => a.ReceiverCustomer)
                    .Include(a => a.PayerBankAccount)
                    .Include(a => a.ReceiverBankAccount)
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (accountingDocument == null)
                {
                    if (isAjax)
                    {
                        return Json(new { success = false, message = "سند حسابداری یافت نشد." });
                    }
                    TempData["ErrorMessage"] = "سند حسابداری یافت نشد.";
                    return RedirectToAction(nameof(Index));
                }

                // Validate bank account currency match
                // Check payer bank account
                if (
                    accountingDocument.PayerBankAccountId.HasValue
                    && accountingDocument.PayerBankAccount != null
                )
                {
                    if (
                        accountingDocument.PayerBankAccount.CurrencyCode
                        != accountingDocument.CurrencyCode
                    )
                    {
                        var errorMsg =
                            $"ارز حساب بانکی پرداخت کننده ({accountingDocument.PayerBankAccount.CurrencyCode}) با ارز سند ({accountingDocument.CurrencyCode}) مطابقت ندارد.";
                        if (isAjax)
                        {
                            return Json(new { success = false, message = errorMsg });
                        }
                        TempData["ErrorMessage"] = errorMsg;
                        return RedirectToAction("Details", new { id });
                    }
                }

                // Check receiver bank account
                if (
                    accountingDocument.ReceiverBankAccountId.HasValue
                    && accountingDocument.ReceiverBankAccount != null
                )
                {
                    if (
                        accountingDocument.ReceiverBankAccount.CurrencyId
                        != accountingDocument.CurrencyId
                    )
                    {
                        var receiverCurrencyCode =
                            accountingDocument.ReceiverBankAccount.Currency != null
                                ? accountingDocument.ReceiverBankAccount.Currency.Code
                                : accountingDocument.ReceiverBankAccount.CurrencyCode;
                        var docCurrencyCode =
                            accountingDocument.Currency != null
                                ? accountingDocument.Currency.Code
                                : accountingDocument.CurrencyCode;
                        var errorMsg =
                            $"ارز حساب بانکی دریافت کننده ({receiverCurrencyCode}) با ارز سند ({docCurrencyCode}) مطابقت ندارد.";
                        if (isAjax)
                        {
                            return Json(new { success = false, message = errorMsg });
                        }
                        TempData["ErrorMessage"] = errorMsg;
                        return RedirectToAction("Details", new { id });
                    }
                }

                // Only process if not already verified
                if (!accountingDocument.IsVerified)
                {
                    try
                    {
                        // Validate CurrencyId is required - NO FALLBACK TO CurrencyCode!
                        if (!accountingDocument.CurrencyId.HasValue)
                        {
                            var errorMsg =
                                $"سند ID: {accountingDocument.Id} فاقد CurrencyId است. CurrencyId الزامی است. لطفاً Migration Script را اجرا کنید.";
                            if (isAjax)
                            {
                                return Json(new { success = false, message = errorMsg });
                            }
                            TempData["ErrorMessage"] = errorMsg;
                            return RedirectToAction("Details", new { id });
                        }

                        // CRITICAL: Update only verification fields to preserve FileData
                        // This ensures IsVerified is saved and committed to database before rebuild
                        // The rebuild query will then see IsVerified=true and include the document
                        accountingDocument.IsVerified = true;
                        accountingDocument.VerifiedAt = DateTime.Now;
                        accountingDocument.VerifiedBy = User.Identity?.Name ?? "System";

                        // Mark only the changed properties as modified to preserve FileData
                        _context.Entry(accountingDocument).Property(x => x.IsVerified).IsModified =
                            true;
                        _context.Entry(accountingDocument).Property(x => x.VerifiedAt).IsModified =
                            true;
                        _context.Entry(accountingDocument).Property(x => x.VerifiedBy).IsModified =
                            true;

                        await _context.SaveChangesAsync(); // CRITICAL: Save FIRST, then process

                        // AFTER saving IsVerified=true, update balances through centralized service
                        // Now the document is saved with IsVerified=true, so rebuild will include it
                        await _centralFinancialService.ProcessAccountingDocumentAsync(
                            accountingDocument,
                            User.Identity?.Name ?? "System"
                        );

                        // Send notifications through central hub
                        var currentUser = await _userManager.GetUserAsync(User);
                        if (currentUser != null)
                        {
                            await _notificationHub.SendAccountingDocumentNotificationAsync(
                                accountingDocument,
                                NotificationEventType.AccountingDocumentVerified,
                                currentUser.Id
                            );
                        }

                        if (isAjax)
                        {
                            return Json(
                                new
                                {
                                    success = true,
                                    message = "سند حسابداری با موفقیت تأیید شد و موجودی‌ها بازمحاسبه شدند.",
                                    documentId = id,
                                    verifiedAt = accountingDocument.VerifiedAt?.ToPersianDateTextify(),
                                }
                            );
                        }
                        TempData["SuccessMessage"] =
                            "سند حسابداری با موفقیت تأیید شد و موجودی‌ها بازمحاسبه شدند.";
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            $"Error verifying document {accountingDocument.Id}: {ex.Message}"
                        );

                        if (isAjax)
                        {
                            return Json(
                                new { success = false, message = $"خطا در تایید سند: {ex.Message}" }
                            );
                        }

                        TempData["ErrorMessage"] = $"خطا در تایید سند: {ex.Message}";
                    }
                }
                else
                {
                    if (isAjax)
                    {
                        return Json(
                            new { success = false, message = "این سند قبلاً تأیید شده است." }
                        );
                    }
                    TempData["InfoMessage"] = "این سند قبلاً تأیید شده است.";
                }
            }
            catch (Exception ex)
            {
                if (isAjax)
                {
                    return Json(
                        new { success = false, message = $"خطا در تأیید سند: {ex.Message}" }
                    );
                }
                TempData["ErrorMessage"] = $"خطا در تأیید سند: {ex.Message}";
            }

            if (isAjax)
            {
                return Json(new { success = false, message = "خطای نامشخص در تأیید سند." });
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: AccountingDocuments/ConfirmAll
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Programmer")]
        public async Task<IActionResult> ConfirmAll()
        {
            try
            {
                // Exclude FileData to prevent memory leak
                var unverifiedDocuments = await _context
                    .AccountingDocuments.Include(a => a.PayerCustomer)
                    .Include(a => a.ReceiverCustomer)
                    .Include(a => a.PayerBankAccount)
                    .Include(a => a.ReceiverBankAccount)
                    .Where(a => !a.IsVerified)
                    .Select(a => new AccountingDocument
                    {
                        Id = a.Id,
                        Type = a.Type,
                        PayerType = a.PayerType,
                        PayerCustomerId = a.PayerCustomerId,
                        PayerBankAccountId = a.PayerBankAccountId,
                        ReceiverType = a.ReceiverType,
                        ReceiverCustomerId = a.ReceiverCustomerId,
                        ReceiverBankAccountId = a.ReceiverBankAccountId,
                        Amount = a.Amount,
                        CurrencyCode = a.CurrencyCode,
                        Title = a.Title,
                        Description = a.Description,
                        DocumentDate = a.DocumentDate,
                        CreatedAt = a.CreatedAt,
                        IsVerified = a.IsVerified,
                        VerifiedAt = a.VerifiedAt,
                        VerifiedBy = a.VerifiedBy,
                        ReferenceNumber = a.ReferenceNumber,
                        FileName = a.FileName,
                        ContentType = a.ContentType,
                        // FileData is excluded to prevent memory leak
                        Notes = a.Notes,
                        IsDeleted = a.IsDeleted,
                        DeletedAt = a.DeletedAt,
                        DeletedBy = a.DeletedBy,
                        IsFrozen = a.IsFrozen,
                        PayerCustomer = a.PayerCustomer,
                        ReceiverCustomer = a.ReceiverCustomer,
                        PayerBankAccount = a.PayerBankAccount,
                        ReceiverBankAccount = a.ReceiverBankAccount,
                    })
                    .OrderBy(a => a.DocumentDate)
                    .ToListAsync();

                if (unverifiedDocuments.Count == 0)
                {
                    TempData["InfoMessage"] = "هیچ سند تایید نشده‌ای یافت نشد.";
                    return RedirectToAction(nameof(Index));
                }

                var confirmationLog = new List<string>();
                var successCount = 0;
                var errorCount = 0;

                // Process each document independently (no transaction wrapper)
                // Each document is saved and committed before processing to ensure IsVerified is visible
                foreach (var document in unverifiedDocuments)
                {
                    try
                    {
                        // Validate bank account currency match
                        bool hasError = false;

                        if (
                            document.PayerBankAccountId.HasValue
                            && document.PayerBankAccount != null
                        )
                        {
                            if (document.PayerBankAccount.CurrencyCode != document.CurrencyCode)
                            {
                                confirmationLog.Add(
                                    $"❌ Document {document.Id}: Currency mismatch for payer bank account"
                                );
                                errorCount++;
                                hasError = true;
                            }
                        }

                        if (
                            document.ReceiverBankAccountId.HasValue
                            && document.ReceiverBankAccount != null
                        )
                        {
                            if (document.ReceiverBankAccount.CurrencyId != document.CurrencyId)
                            {
                                confirmationLog.Add(
                                    $"❌ Document {document.Id}: Currency mismatch for receiver bank account"
                                );
                                errorCount++;
                                hasError = true;
                            }
                        }

                        if (!hasError)
                        {
                            // CRITICAL: Save IsVerified status FIRST and commit immediately
                            // This ensures IsVerified is saved and committed to database before rebuild
                            // The rebuild query will then see IsVerified=true and include the document
                            document.IsVerified = true;
                            document.VerifiedAt = DateTime.Now;
                            document.VerifiedBy = User.Identity?.Name ?? "System";

                            _context.Update(document);
                            await _context.SaveChangesAsync(); // CRITICAL: Save and commit FIRST, then process

                            // AFTER saving IsVerified=true, update balances through centralized service
                            // Now the document is saved with IsVerified=true, so rebuild will include it
                            await _centralFinancialService.ProcessAccountingDocumentAsync(
                                document,
                                User.Identity?.Name ?? "System"
                            );

                            confirmationLog.Add(
                                $"✅ Document {document.Id}: Confirmed successfully ({document.Amount:N2} {document.CurrencyCode})"
                            );
                            confirmationLog.Add(
                                $"   - Payer: Customer {document.PayerCustomerId} gets +{document.Amount}"
                            );
                            confirmationLog.Add(
                                $"   - Receiver: Customer {document.ReceiverCustomerId} gets -{document.Amount}"
                            );
                            successCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        confirmationLog.Add($"❌ Document {document.Id}: Error - {ex.Message}");
                        errorCount++;
                    }
                }

                var summary = new[]
                {
                    $"✅ تعداد اسناد تایید شده: {successCount}",
                    $"❌ تعداد اسناد با خطا: {errorCount}",
                    $"📄 کل اسناد پردازش شده: {unverifiedDocuments.Count}",
                    "",
                    "✅ همه اسناد با منطق صحیح پردازش شدند: پرداخت کننده = +مبلغ، دریافت کننده = -مبلغ",
                };

                TempData["Success"] = string.Join("<br/>", summary);
                TempData["ConfirmationLog"] = string.Join("\n", confirmationLog);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"خطا در تایید همه اسناد: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: AccountingDocuments/GetFile/5
        public async Task<IActionResult> GetFile(int id)
        {
            var document = await _context.AccountingDocuments.FirstOrDefaultAsync(d => d.Id == id);

            if (
                document == null
                || document.FileData == null
                || string.IsNullOrEmpty(document.FileName)
            )
            {
                return NotFound();
            }

            return File(
                document.FileData,
                document.ContentType ?? "application/octet-stream",
                document.FileName
            );
        }

        // POST: AccountingDocuments/ProcessOcr
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessOcr(IFormFile imageFile)
        {
            try
            {
                if (imageFile == null || imageFile.Length == 0)
                {
                    return Json(new { success = false, message = "فایل انتخاب نشده است." });
                }

                // Validate file type (only images)
                var allowedTypes = new[]
                {
                    "image/jpeg",
                    "image/jpg",
                    "image/png",
                    "image/bmp",
                    "image/gif",
                };
                if (!allowedTypes.Contains(imageFile.ContentType.ToLower()))
                {
                    return Json(
                        new { success = false, message = "فقط فایل‌های تصویری پشتیبانی می‌شوند." }
                    );
                }

                // Validate file size (max 10MB)
                if (imageFile.Length > 10 * 1024 * 1024)
                {
                    return Json(
                        new
                        {
                            success = false,
                            message = "حجم فایل نمی‌تواند بیشتر از 10 مگابایت باشد.",
                        }
                    );
                }

                // Convert to byte array
                byte[] imageData;
                using (var memoryStream = new MemoryStream())
                {
                    await imageFile.CopyToAsync(memoryStream);
                    imageData = memoryStream.ToArray();
                }

                // Process with OCR
                var ocrResult = await _ocrService.ProcessAccountingDocumentAsync(imageData);

                if (ocrResult.Success)
                {
                    return Json(
                        new
                        {
                            success = true,
                            message = "OCR با موفقیت انجام شد.",
                            data = new
                            {
                                rawText = ocrResult.RawText,
                                amount = ocrResult.Amount,
                                referenceId = ocrResult.ReferenceId,
                                date = ocrResult.Date,
                                accountNumber = ocrResult.AccountNumber,
                            },
                        }
                    );
                }
                else
                {
                    return Json(
                        new
                        {
                            success = false,
                            message = ocrResult.ErrorMessage ?? "خطا در پردازش OCR",
                        }
                    );
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "خطای داخلی سرور: " + ex.Message });
            }
        }

        // POST: AccountingDocuments/PreviewTransactionEffects
        [HttpPost]
        public async Task<IActionResult> PreviewTransactionEffects(
            [FromBody] AccountingDocument accountingDocument
        )
        {
            try
            {
                _logger.LogInformation(
                    $"[PreviewTransactionEffects] Called for Amount={accountingDocument.Amount}, CurrencyId={accountingDocument.CurrencyId}, CurrencyCode={accountingDocument.CurrencyCode}"
                );

                // Validate CurrencyId is required (should come from form now)
                if (!accountingDocument.CurrencyId.HasValue)
                {
                    return Json(
                        new
                        {
                            success = false,
                            message = "CurrencyId الزامی است. لطفاً ارز را انتخاب کنید.",
                        }
                    );
                }

                // Use centralized CentralFinancialService for preview calculation with auto-balance creation
                var previewEffects =
                    await _centralFinancialService.PreviewAccountingDocumentEffectsAsync(
                        accountingDocument
                    );

                // Transform to match the expected UI format
                var customerEffectsList = previewEffects
                    .CustomerEffects.Select(ce => new
                    {
                        customerName = ce.CustomerName,
                        currency = ce.CurrencyCode,
                        currentBalance = ce.CurrentBalance,
                        change = ce.TransactionAmount,
                        newBalance = ce.NewBalance,
                        role = ce.Role,
                    })
                    .ToList();

                var bankAccountEffectsList = previewEffects
                    .BankAccountEffects.Select(be => new
                    {
                        bankName = be.BankName,
                        accountNumber = be.AccountNumber,
                        currency = be.CurrencyCode,
                        currentBalance = be.CurrentBalance,
                        change = be.TransactionAmount,
                        newBalance = be.NewBalance,
                        role = be.Role,
                    })
                    .ToList();

                return Json(
                    new
                    {
                        success = true,
                        effects = new
                        {
                            customerEffects = customerEffectsList,
                            bankAccountEffects = bankAccountEffectsList,
                        },
                        warnings = previewEffects.Warnings,
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error previewing transaction effects using centralized service"
                );
                return Json(
                    new { success = false, message = "خطا در محاسبه تأثیرات تراکنش: " + ex.Message }
                );
            }
        }

        // POST: AccountingDocuments/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [HasPermission(Permissions.Documents_Delete)] 
        public async Task<IActionResult> Delete(int id)
        {
            bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

            try
            {
                var document = await _context
                    .AccountingDocuments.Where(a => a.Id == id)
                    .Select(a => new AccountingDocument
                    {
                        Id = a.Id,
                        Title = a.Title,
                        IsDeleted = a.IsDeleted,
                        DeletedAt = a.DeletedAt,
                        DeletedBy = a.DeletedBy,
                        IsVerified = a.IsVerified,
                        Amount = a.Amount,
                        CurrencyCode = a.CurrencyCode,
                        CurrencyId = a.CurrencyId, // REQUIRED!
                        PayerType = a.PayerType,
                        PayerCustomerId = a.PayerCustomerId,
                        PayerBankAccountId = a.PayerBankAccountId,
                        ReceiverType = a.ReceiverType,
                        ReceiverCustomerId = a.ReceiverCustomerId,
                        ReceiverBankAccountId = a.ReceiverBankAccountId,
                        // No navigation properties needed
                    })
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (document == null)
                {
                    if (isAjax)
                    {
                        return Json(new { success = false, message = "سند حسابداری یافت نشد." });
                    }
                    TempData["ErrorMessage"] = "سند حسابداری یافت نشد.";
                    return RedirectToAction(nameof(Index));
                }
                if (document.IsDeleted)
                {
                    if (isAjax)
                    {
                        return Json(
                            new { success = false, message = "سند حسابداری قبلاً حذف شده است." }
                        );
                    }
                    TempData["ErrorMessage"] = "سند حسابداری قبلاً حذف شده است.";
                    return RedirectToAction(nameof(Index));
                }

                // Use centralized service to delete with proper financial impact reversal
                var currentUser = await _userManager.GetUserAsync(User);
                await _centralFinancialService.DeleteAccountingDocumentAsync(
                    document,
                    currentUser?.UserName ?? "Admin"
                );

                // Log admin activity
                var adminActivity = new AdminActivity
                {
                    AdminUserId = currentUser?.Id ?? "Unknown",
                    ActivityType = AdminActivityType.UserDeleted, // Using as closest match for document deletion
                    Description = $"Deleted Accounting Document #{document.Id} - {document.Title}",
                    Timestamp = DateTime.UtcNow,
                    IpAddress = Request.HttpContext.Connection.RemoteIpAddress?.ToString(),
                };
                _context.AdminActivities.Add(adminActivity);
                await _context.SaveChangesAsync();

                if (currentUser != null)
                {
                    await _notificationHub.SendAccountingDocumentNotificationAsync(
                        document,
                        NotificationEventType.AccountingDocumentDeleted,
                        currentUser.Id
                    );
                }

                var successMessage =
                    $"سند حسابداری #{document.Id} با موفقیت حذف شد و تأثیرات مالی آن برگردانده شد.";

                if (isAjax)
                {
                    return Json(
                        new
                        {
                            success = true,
                            message = successMessage,
                            redirectUrl = Url.Action(nameof(Index)),
                        }
                    );
                }

                TempData["SuccessMessage"] = successMessage;
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting accounting document {id}");

                if (isAjax)
                {
                    return Json(
                        new { success = false, message = $"خطا در حذف سند حسابداری: {ex.Message}" }
                    );
                }

                TempData["ErrorMessage"] = "خطا در حذف سند حسابداری. لطفاً دوباره تلاش کنید.";
                return RedirectToAction(nameof(Index));
            }
        }

        private bool AccountingDocumentExists(int id)
        {
            return _context.AccountingDocuments.Any(e => e.Id == id);
        }

        /// <summary>
        /// GET: Show view to upload CSV file for importing accounting documents.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> UploadDocumentsCsv()
        {
            var customers = await _context
                .Customers.Where(c => c.IsActive && !c.IsSystem)
                .OrderBy(c => c.FullName)
                .Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.FullName ?? c.Id.ToString(),
                })
                .ToListAsync();
            ViewBag.Customers = customers;
            return View();
        }

        /// <summary>
        /// Upload CSV file to import accounting documents. Uses temp bank accounts per currency for "other side" when reference is unique; duplicate reference in CSV = both sides of one document.
        /// Checks ReferenceNumber from database to avoid re-importing.
        /// </summary>
        [HttpPost]
        [RequestSizeLimit(10_485_760)] // 10 MB
        public async Task<IActionResult> UploadDocumentsCsv(IFormFile csvFile, int customerId)
        {
            var importedCount = 0;
            var skippedCount = 0;
            var duplicateRefs = new List<string>();
            var errors = new List<object>();

            if (csvFile == null || csvFile.Length == 0)
            {
                return Json(
                    new
                    {
                        success = false,
                        message = "فایلی انتخاب نشده است.",
                        importedCount = 0,
                        skippedCount = 0,
                        duplicateRefs,
                        errors,
                    }
                );
            }

            try
            {
                var customer = await _context.Customers.FindAsync(customerId);
                if (customer == null)
                {
                    return Json(
                        new
                        {
                            success = false,
                            message = "مشتری یافت نشد.",
                            importedCount = 0,
                            skippedCount = 0,
                            duplicateRefs,
                            errors,
                        }
                    );
                }

                List<DocCsvRow> rows;
                try
                {
                    using var reader = new StreamReader(csvFile.OpenReadStream(), Encoding.UTF8);
                    rows = ParseDocumentsCsv(reader, errors);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing documents CSV");
                    return Json(
                        new
                        {
                            success = false,
                            message = $"خطا در خواندن فایل: {ex.Message}",
                            importedCount = 0,
                            skippedCount = 0,
                            duplicateRefs,
                            errors,
                        }
                    );
                }

                if (rows.Count == 0)
                {
                    return Json(
                        new
                        {
                            success = true,
                            message = "هیچ ردیف معتبری در فایل یافت نشد.",
                            importedCount = 0,
                            skippedCount = 0,
                            duplicateRefs,
                            errors,
                        }
                    );
                }

                var refIds = rows.Select(r => r.ReferenceId)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct()
                    .ToList();
                // فقط وقتی رد می‌کنیم که همین مشتری با همین رفرنس و همان مبلغ (در نقش پرداخت‌کننده یا دریافت‌کننده) از قبل سند داشته باشد
                var existingDocsSameCustomer = await _context
                    .AccountingDocuments.Where(a =>
                        a.ReferenceNumber != null
                        && refIds.Contains(a.ReferenceNumber)
                        && (a.PayerCustomerId == customerId || a.ReceiverCustomerId == customerId)
                    )
                    .Select(a => new { a.ReferenceNumber, a.Amount })
                    .ToListAsync();

                var groups = rows.GroupBy(r => r.ReferenceId ?? "")
                    .Where(g => !string.IsNullOrWhiteSpace(g.Key))
                    .ToList();

                foreach (var group in groups)
                {
                    var refId = group.Key;
                    var list = group.ToList();
                    DocCsvRow useRow = list[0];
                    bool isReceive =
                        useRow.Type?.Trim().Equals("Receive", StringComparison.OrdinalIgnoreCase)
                        == true;

                    if (list.Count >= 2)
                    {
                        var payRow = list.FirstOrDefault(r =>
                            r.Type?.Trim().Equals("Pay", StringComparison.OrdinalIgnoreCase) == true
                        );
                        var receiveRow = list.FirstOrDefault(r =>
                            r.Type?.Trim().Equals("Receive", StringComparison.OrdinalIgnoreCase)
                            == true
                        );
                        useRow = receiveRow ?? payRow ?? list[0];
                        isReceive =
                            useRow
                                .Type?.Trim()
                                .Equals("Receive", StringComparison.OrdinalIgnoreCase) == true;
                    }

                    int? currencyId = useRow.CurrencyId;
                    string? currencyCode = useRow.CurrencyCode;
                    if (!currencyId.HasValue && !string.IsNullOrWhiteSpace(currencyCode))
                    {
                        // Filter in memory: EF/SQLite cannot translate ToUpperInvariant to SQL
                        var codeUpper = currencyCode.Trim().ToUpperInvariant();
                        var currencies = await _context
                            .Currencies.Where(c => c.Code != null)
                            .ToListAsync();
                        var currency = currencies.FirstOrDefault(c =>
                            (c.Code ?? "").Trim().ToUpperInvariant() == codeUpper
                        );
                        if (currency != null)
                        {
                            currencyId = currency.Id;
                            currencyCode = currency.Code;
                        }
                    }
                    if (!currencyId.HasValue || currencyId.Value <= 0)
                    {
                        errors.Add(new { refId, message = "ارز معتبر نیست." });
                        continue;
                    }

                    var amount = Math.Abs(useRow.Amount);

                    // داپلیکیت واقعی: همین مشتری + همین رفرنس + همان مبلغ (در یک طرف سند) از قبل وجود دارد
                    if (
                        existingDocsSameCustomer.Any(d =>
                            (d.ReferenceNumber ?? "").Trim() == refId
                            && Math.Abs(d.Amount - amount) < 0.01m
                        )
                    )
                    {
                        skippedCount++;
                        duplicateRefs.Add(refId);
                        continue;
                    }

                    BankAccount tempBank;
                    try
                    {
                        tempBank =
                            await _csvImportService.GetOrCreateTempBankAccountForCsvImportAsync(
                                currencyId.Value,
                                currencyCode ?? "IRR"
                            );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to get/create temp bank for CSV import");
                        errors.Add(new { refId, message = ex.Message });
                        continue;
                    }

                    var doc = new AccountingDocument
                    {
                        Type = DocumentType.Havala,
                        CurrencyId = currencyId,
                        CurrencyCode = currencyCode ?? "IRR",
                        Amount = amount,
                        Title = $"ورود CSV - {refId}",
                        Description = useRow.Description ?? useRow.Note,
                        DocumentDate = useRow.Date ?? DateTime.Today,
                        CreatedAt = DateTime.Now,
                        ReferenceNumber = refId,
                        IsVerified = true,
                        IsFrozen = false,
                    };

                    if (isReceive)
                    {
                        doc.PayerType = PayerType.System;
                        doc.PayerBankAccountId = tempBank.Id;
                        doc.ReceiverType = ReceiverType.Customer;
                        doc.ReceiverCustomerId = customerId;
                    }
                    else
                    {
                        doc.PayerType = PayerType.Customer;
                        doc.PayerCustomerId = customerId;
                        doc.ReceiverType = ReceiverType.System;
                        doc.ReceiverBankAccountId = tempBank.Id;
                    }

                    try
                    {
                        _context.Add(doc);
                        await _context.SaveChangesAsync();
                        await _centralFinancialService.ProcessAccountingDocumentAsync(
                            doc,
                            "CSV Import"
                        );
                        importedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error saving document for ref {RefId}", refId);
                        errors.Add(new { refId, message = ex.Message });
                    }
                }

                return Json(
                    new
                    {
                        success = true,
                        message = $"واردات انجام شد. تعداد: {importedCount}، رد شده (موجود در دیتابیس): {skippedCount}",
                        importedCount,
                        skippedCount,
                        duplicateRefs,
                        errors,
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UploadDocumentsCsv");
                return Json(
                    new
                    {
                        success = false,
                        message = $"خطا در ارتباط با سرور: {ex.Message}",
                        importedCount = 0,
                        skippedCount = 0,
                        duplicateRefs = new List<string>(),
                        errors = new List<object>(),
                    }
                );
            }
        }

        private static List<DocCsvRow> ParseDocumentsCsv(StreamReader reader, List<object> errors)
        {
            var rows = new List<DocCsvRow>();
            var lineNum = 0;
            string? headerLine = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(headerLine))
                return rows;
            lineNum++;
            var headers = headerLine!.Split(',').Select(h => h.Trim().ToLowerInvariant()).ToArray();
            int idxDate = Array.FindIndex(headers, h => h == "date");
            int idxType = Array.FindIndex(headers, h => h == "type");
            int idxCurrencyId = Array.FindIndex(headers, h => h == "currencyid");
            int idxCurrencyCode = Array.FindIndex(headers, h => h == "currencycode");
            int idxCurrency = Array.FindIndex(headers, h => h == "currency");
            int idxTransactionId = Array.FindIndex(headers, h => h == "transactionid");
            int idxId = Array.FindIndex(headers, h => h == "id");
            int idxAmount = Array.FindIndex(headers, h => h == "amount");
            int idxAmountIrr = Array.FindIndex(
                headers,
                h => h.Contains("amount") && h.Contains("irr")
            );
            int idxDesc = Array.FindIndex(headers, h => h == "description");
            int idxNote = Array.FindIndex(headers, h => h == "note");

            int refCol = idxTransactionId >= 0 ? idxTransactionId : idxId;
            int amountCol = idxAmount >= 0 ? idxAmount : idxAmountIrr;
            if (idxDate < 0 || idxType < 0 || refCol < 0 || amountCol < 0)
            {
                errors.Add(
                    new
                    {
                        line = lineNum,
                        message = "ستون‌های ضروری (Date, Type, Reference, Amount) یافت نشد.",
                    }
                );
                return rows;
            }

            while (reader.ReadLine() is { } line)
            {
                lineNum++;
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                var parts = SplitCsvLine(line);
                if (parts.Count <= Math.Max(refCol, amountCol))
                    continue;
                var refId = refCol < parts.Count ? NormalizeRefId(parts[refCol]) : "";
                if (string.IsNullOrWhiteSpace(refId))
                    continue;
                DateTime? date = null;
                if (
                    idxDate >= 0
                    && idxDate < parts.Count
                    && DateTime.TryParse(
                        parts[idxDate],
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var d
                    )
                )
                    date = d;
                var type = (idxType >= 0 && idxType < parts.Count) ? parts[idxType] : null;
                int? currencyId = null;
                if (
                    idxCurrencyId >= 0
                    && idxCurrencyId < parts.Count
                    && int.TryParse(
                        parts[idxCurrencyId],
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var cid
                    )
                )
                    currencyId = cid;
                var currencyCode =
                    (idxCurrencyCode >= 0 && idxCurrencyCode < parts.Count)
                        ? parts[idxCurrencyCode]
                        : (
                            (idxCurrency >= 0 && idxCurrency < parts.Count)
                                ? parts[idxCurrency]
                                : null
                        );
                var amountStr =
                    (amountCol >= 0 && amountCol < parts.Count)
                        ? parts[amountCol].Replace(",", "", StringComparison.Ordinal)
                        : "0";
                if (
                    !decimal.TryParse(
                        amountStr,
                        NumberStyles.Number,
                        CultureInfo.InvariantCulture,
                        out var amt
                    )
                )
                    continue;
                var desc =
                    (idxDesc >= 0 && idxDesc < parts.Count)
                        ? parts[idxDesc]
                        : ((idxNote >= 0 && idxNote < parts.Count) ? parts[idxNote] : null);
                rows.Add(
                    new DocCsvRow
                    {
                        ReferenceId = refId,
                        Date = date,
                        Type = type,
                        CurrencyId = currencyId,
                        CurrencyCode = currencyCode?.Trim(),
                        Amount = Math.Abs(amt),
                        Description = desc,
                        Note = desc,
                    }
                );
            }
            return rows;
        }

        private static List<string> SplitCsvLine(string line)
        {
            var list = new List<string>();
            var sb = new StringBuilder();
            var inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }
                if (!inQuotes && c == ',')
                {
                    list.Add(sb.ToString().Trim());
                    sb.Clear();
                    continue;
                }
                sb.Append(c);
            }
            list.Add(sb.ToString().Trim());
            return list;
        }

        private static string NormalizeRefId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";
            return value.Trim();
        }

        private sealed class DocCsvRow
        {
            public string? ReferenceId { get; set; }
            public DateTime? Date { get; set; }
            public string? Type { get; set; }
            public int? CurrencyId { get; set; }
            public string? CurrencyCode { get; set; }
            public decimal Amount { get; set; }
            public string? Description { get; set; }
            public string? Note { get; set; }
        }
    }
}
