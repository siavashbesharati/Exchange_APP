using ForexExchange.Extensions;
using ForexExchange.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ForexExchange.Services
{
    public interface IDataSeedService
    {
        Task SeedDataAsync();
    }

    public class DataSeedService : IDataSeedService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ForexDbContext _context;
        private readonly ILogger<DataSeedService> _logger;
        private readonly IWebScrapingService _webScrapingService;
        private readonly ICentralFinancialService _centralFinancialService;

        public DataSeedService(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ForexDbContext context,
            ILogger<DataSeedService> logger,
            IWebScrapingService webScrapingService,
            ICentralFinancialService centralFinancialService
        )
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _logger = logger;
            _webScrapingService = webScrapingService;
            _centralFinancialService = centralFinancialService;
        }

        public async Task SeedDataAsync()
        {
            try
            {
                // Create roles
                await CreateRolesAsync();

                // Create admin user
                await CreateAdminUserAsync();

                // Seed Role Permissions
                await SeedRolePermissionsAsync();

                await CreatCurencies(); // usd ,toman , AED,OMR,EURO,LiRA

                // Initialize currency pools first (required for financial operations)
                await SeedCurrencyPoolsAsync();

                // Seed exchange rates first
                await SeedExchangeRatesAsync();

                await CreateSystemCustomerAsync();

                // Create bank accounts for system customer (one per currency)
                await SeedSystemBankAccountsAsync();

                await SeedTestCustomersAsync();

                // Seed default system settings including branding
                await SeedDefaultSettingsAsync();

                // // Initialize customer balances using CentralFinancialService (with complete audit trail)
                // await SeedCustomerBalancesAsync();

                // // Seed 20-30 orders per customer using CentralFinancialService (dual-currency impact + history)
                // await SeedCustomerOrdersAsync();

                // // Seed 20-30 accounting documents per customer using CentralFinancialService (proper balance updates + history)
                // await SeedCustomerAccountingDocumentsAsync();

                _logger.LogInformation("Data seeding completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while seeding data");
                throw;
            }
        }

        /// <summary>
        /// Seed default currencies if missing - unified seeding from ForexDbContext
        /// </summary>
        private async Task CreatCurencies()
        {
            if (await _context.Currencies.AnyAsync())
            {
                _logger.LogInformation("Currencies already exist, skipping seeding");
                return;
            }

            // Unified currency defaults (matching ForexDbContext order and IDs)
            var now = DateTime.Now;
            var defaults = new List<Currency>
            {
                new()
                {
                    Code = "IRR",
                    Name = "Iranian Toman",
                    PersianName = "تومان",
                    Symbol = "﷼",
                    IsActive = true,
                    DisplayOrder = 1,
                    CreatedAt = now,
                    RatePriority = 8,
                },
                new()
                {
                    Code = "USD",
                    Name = "US Dollar",
                    PersianName = "دلار آمریکا",
                    Symbol = "$",
                    IsActive = true,
                    DisplayOrder = 4,
                    CreatedAt = now,
                    RatePriority = 3,
                },
                new()
                {
                    Code = "EUR",
                    Name = "Euro",
                    PersianName = "یورو",
                    Symbol = "€",
                    IsActive = true,
                    DisplayOrder = 5,
                    CreatedAt = now,
                    RatePriority = 2,
                },
                new()
                {
                    Code = "AED",
                    Name = "UAE Dirham",
                    PersianName = "درهم امارات",
                    Symbol = "د.إ",
                    IsActive = true,
                    DisplayOrder = 3,
                    CreatedAt = now,
                    RatePriority = 4,
                },
                new()
                {
                    Code = "OMR",
                    Name = "Omani Rial",
                    PersianName = "ریال عمان",
                    Symbol = "ر.ع.",
                    IsActive = true,
                    DisplayOrder = 2,
                    CreatedAt = now,
                    RatePriority = 1,
                },
                new()
                {
                    Code = "TRY",
                    Name = "Turkish Lira",
                    PersianName = "لیر ترکیه",
                    Symbol = "₺",
                    IsActive = true,
                    DisplayOrder = 6,
                    CreatedAt = now,
                    RatePriority = 6,
                },
                new()
                {
                    Code = "CNY",
                    Name = "Chinese Yuan",
                    PersianName = "یوان چین",
                    Symbol = "¥",
                    IsActive = true,
                    DisplayOrder = 7,
                    CreatedAt = now,
                    RatePriority = 5,
                },
            };

            _context.Currencies.AddRange(defaults);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Successfully seeded {defaults.Count} currencies");
        }

        private async Task CreateRolesAsync()
        {
            // Get all UserRole enum values
            var userRoles = Enum.GetValues<UserRole>();

            foreach (var userRole in userRoles)
            {
                var roleName = userRole.ToString();

                if (!await _roleManager.RoleExistsAsync(roleName))
                {
                    await _roleManager.CreateAsync(new IdentityRole(roleName));
                    _logger.LogInformation(
                        $"Role '{roleName}' ({userRole.GetDisplayName()}) created successfully"
                    );
                }
            }
        }

        private async Task CreateAdminUserAsync()
        {
            // Define multiple admin users with their phone numbers as passwords
            var adminUsers = new[]
            {
                new
                {
                    Phone = "00989195410188",
                    Email = "siavash@iranexpedia.ir",
                    FullName = " توسعه دهنده ",
                    Password = "roberto2025@#$ASD",
                    Role = UserRole.Programmer,
                },
                new
                {
                    Phone = "00989120674032",
                    Email = "exsora@iranexpedia.ir",
                    FullName = "سیاوش",
                    Password = "admindemo1",
                    Role = UserRole.Admin,
                },
            };

            foreach (var adminData in adminUsers)
            {
                // Check if admin exists by email first (for existing installations)
                var adminUser = await _userManager.FindByEmailAsync(adminData.Email);

                // If found with email username, update it to use phone number
                if (adminUser != null && adminUser.UserName == adminData.Email)
                {
                    adminUser.UserName = adminData.Phone;
                    var updateResult = await _userManager.UpdateAsync(adminUser);
                    if (updateResult.Succeeded)
                    {
                        _logger.LogInformation(
                            $"Updated existing admin user to use phone number as username: {adminData.Phone}"
                        );
                    }
                    continue;
                }

                // Check if admin exists by phone number
                if (adminUser == null)
                {
                    adminUser = await _userManager.FindByNameAsync(adminData.Phone);
                }

                if (adminUser == null)
                {
                    adminUser = new ApplicationUser
                    {
                        UserName = adminData.Phone, // Use phone as username for login
                        Email = adminData.Email,
                        FullName = adminData.FullName,
                        Role = adminData.Role,
                        IsActive = true,
                        CreatedAt = DateTime.Now,
                        EmailConfirmed = true,
                        PhoneNumber = adminData.Phone,
                    };

                    var result = await _userManager.CreateAsync(adminUser, adminData.Password);

                    if (result.Succeeded)
                    {
                        // Use the enum value as role name
                        var roleName = adminData.Role.ToString();
                        await _userManager.AddToRoleAsync(adminUser, roleName);
                        _logger.LogInformation(
                            $"Admin user created successfully with username: {adminData.Phone}, email: {adminData.Email}, and role: {roleName}"
                        );
                    }
                    else
                    {
                        var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                        _logger.LogError(
                            $"Failed to create admin user {adminData.Phone}: {errors}"
                        );
                    }
                }
                else
                {
                    _logger.LogInformation($"Admin user {adminData.Phone} already exists");
                }
            }
        }

        /// <summary>
        /// Create system customer for exchange operations
        /// ایجاد مشتری سیستم برای عملیات صرافی
        /// </summary>
        private async Task CreateSystemCustomerAsync()
        {
            var systemCustomer = await _context.Customers.FirstOrDefaultAsync(c => c.IsSystem);

            if (systemCustomer == null)
            {
                systemCustomer = new Customer
                {
                    FullName = "ISSystem",
                    PhoneNumber = "0000000000",
                    Email = "system@exchange.local",
                    NationalId = "0000000000",
                    Address = "سیستم داخلی",
                    IsActive = true,
                    IsSystem = true,
                    CreatedAt = DateTime.Now,
                };

                _context.Customers.Add(systemCustomer);
                await _context.SaveChangesAsync();

                _logger.LogInformation("System customer created successfully");
            }
            else
            {
                _logger.LogInformation("System customer already exists");
            }
        }

        /// <summary>
        /// Create bank accounts for system customer - one per active currency
        /// ایجاد حساب‌های بانکی برای مشتری سیستم - یکی برای هر ارز فعال
        /// </summary>
        private async Task SeedSystemBankAccountsAsync()
        {
            try
            {
                var systemCustomer = await _context.Customers.FirstOrDefaultAsync(c => c.IsSystem);
                if (systemCustomer == null)
                {
                    _logger.LogError("System customer not found for bank account seeding");
                    return;
                }

                // Check if system bank accounts already exist
                var existingSystemBankAccounts = await _context
                    .BankAccounts.Where(ba => ba.CustomerId == systemCustomer.Id)
                    .CountAsync();

                if (existingSystemBankAccounts > 0)
                {
                    _logger.LogInformation(
                        $"{existingSystemBankAccounts} system bank accounts already exist, skipping bank account seeding"
                    );
                    return;
                }

                var currencies = await _context.Currencies.Where(c => c.IsActive).ToListAsync();

                var random = new Random();
                var bankNames = new[]
                {
                    "بانک ملی",
                    "بانک صادرات",
                    "بانک تجارت",
                    "بانک کشاورزی",
                    "بانک پارسیان",
                    "بانک پاسارگاد",
                };
                var totalAccountsCreated = 0;

                _logger.LogInformation(
                    $"Creating system bank accounts for {currencies.Count} active currencies"
                );

                foreach (var currency in currencies)
                {
                    var bankName = bankNames[random.Next(bankNames.Length)];
                    var accountNumber = $"SYS{currency.Code}{random.Next(100000, 999999)}";
                    var iban =
                        $"IR{random.Next(10, 99)}{random.Next(1000, 9999)}{random.Next(100000000, 999999999)}";
                    var initialBalance = (decimal)(random.NextDouble() * 500000 + 100000); // Random balance 100K-600K

                    var bankAccount = new BankAccount
                    {
                        CustomerId = systemCustomer.Id,
                        BankName = bankName,
                        AccountNumber = accountNumber,
                        AccountHolderName = $"سیستم صرافی - {currency.Name}",
                        IBAN = iban,
                        Branch = "شعبه مرکزی",
                        CurrencyCode = currency.Code,
                        IsActive = true,
                        IsDefault = currency.Code == "IRR", // Make base currency account default
                        AccountBalance = Math.Round(initialBalance, 2),
                        CreatedAt = DateTime.Now,
                        Notes =
                            $"حساب سیستم برای ارز {currency.Name} - ایجاد شده توسط DataSeedService",
                    };

                    _context.BankAccounts.Add(bankAccount);
                    totalAccountsCreated++;

                    _logger.LogInformation(
                        $"Created system bank account for {currency.Code}: {accountNumber} with balance {initialBalance:N2}"
                    );
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation(
                    $"Successfully created {totalAccountsCreated} system bank accounts"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed system bank accounts");
                throw;
            }
        }

        private async Task SeedExchangeRatesAsync()
        {
            try
            {
                // Check if exchange rates already exist
                var existingRatesCount = await _context.ExchangeRates.CountAsync();
                if (existingRatesCount > 0)
                {
                    _logger.LogInformation(
                        $"{existingRatesCount} exchange rates already exist, skipping rate seeding"
                    );
                    return;
                }

                // Get currencies
                var omrCurrency = await _context.Currencies.FirstOrDefaultAsync(c =>
                    c.Code == "OMR"
                );
                var eurCurrency = await _context.Currencies.FirstOrDefaultAsync(c =>
                    c.Code == "EUR"
                );
                var usdCurrency = await _context.Currencies.FirstOrDefaultAsync(c =>
                    c.Code == "USD"
                );
                var aedCurrency = await _context.Currencies.FirstOrDefaultAsync(c =>
                    c.Code == "AED"
                );
                var cnyCurrency = await _context.Currencies.FirstOrDefaultAsync(c =>
                    c.Code == "CNY"
                );
                var tryCurrency = await _context.Currencies.FirstOrDefaultAsync(c =>
                    c.Code == "TRY"
                );
                var irrCurrency = await _context.Currencies.FirstOrDefaultAsync(c =>
                    c.Code == "IRR"
                );

                if (omrCurrency == null)
                {
                    _logger.LogError("OMR currency not found in database");
                    return;
                }

                var exchangeRates = new List<ExchangeRate>();
                var now = DateTime.Now;

                // OMR to other currencies (OMR as base)
                if (eurCurrency != null)
                {
                    exchangeRates.Add(
                        new ExchangeRate
                        {
                            FromCurrencyId = omrCurrency.Id,
                            ToCurrencyId = eurCurrency.Id,
                            Rate = 2.22m,
                            IsActive = true,
                            UpdatedAt = now,
                            UpdatedBy = "DataSeed-System",
                        }
                    );
                }

                if (usdCurrency != null)
                {
                    exchangeRates.Add(
                        new ExchangeRate
                        {
                            FromCurrencyId = omrCurrency.Id,
                            ToCurrencyId = usdCurrency.Id,
                            Rate = 2.60m,
                            IsActive = true,
                            UpdatedAt = now,
                            UpdatedBy = "DataSeed-System",
                        }
                    );
                }

                if (aedCurrency != null)
                {
                    exchangeRates.Add(
                        new ExchangeRate
                        {
                            FromCurrencyId = omrCurrency.Id,
                            ToCurrencyId = aedCurrency.Id,
                            Rate = 9.55m,
                            IsActive = true,
                            UpdatedAt = now,
                            UpdatedBy = "DataSeed-System",
                        }
                    );
                }

                if (cnyCurrency != null)
                {
                    exchangeRates.Add(
                        new ExchangeRate
                        {
                            FromCurrencyId = omrCurrency.Id,
                            ToCurrencyId = cnyCurrency.Id,
                            Rate = 18.51m,
                            IsActive = true,
                            UpdatedAt = now,
                            UpdatedBy = "DataSeed-System",
                        }
                    );
                }

                if (tryCurrency != null)
                {
                    exchangeRates.Add(
                        new ExchangeRate
                        {
                            FromCurrencyId = omrCurrency.Id,
                            ToCurrencyId = tryCurrency.Id,
                            Rate = 108.44m,
                            IsActive = true,
                            UpdatedAt = now,
                            UpdatedBy = "DataSeed-System",
                        }
                    );
                }

                if (irrCurrency != null)
                {
                    exchangeRates.Add(
                        new ExchangeRate
                        {
                            FromCurrencyId = omrCurrency.Id,
                            ToCurrencyId = irrCurrency.Id,
                            Rate = 291500m,
                            IsActive = true,
                            UpdatedAt = now,
                            UpdatedBy = "DataSeed-System",
                        }
                    );
                }

                if (exchangeRates.Any())
                {
                    _context.ExchangeRates.AddRange(exchangeRates);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation(
                        $"Successfully seeded {exchangeRates.Count} OMR-based exchange rates"
                    );
                }
                else
                {
                    _logger.LogWarning(
                        "No exchange rates were created - currencies may be missing"
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed OMR-based exchange rates");
                throw;
            }
        }

        /// <summary>
        /// Seed 5-10 test customers (as requested by user)
        /// </summary>
        private async Task SeedTestCustomersAsync()
        {
            try
            {
                // Check if customers already exist (excluding system customer)
                var existingCustomerCount = await _context
                    .Customers.Where(c => !c.IsSystem)
                    .CountAsync();

                if (existingCustomerCount >= 5)
                {
                    _logger.LogInformation(
                        $"{existingCustomerCount} customers already exist, skipping customer seeding"
                    );
                    return;
                }

                var random = new Random();

                // Persian male names
                var persianMaleNames = new[]
                {
                    "علی",
                    "محمد",
                    "حسن",
                    "حسین",
                    "احمد",
                    "مهدی",
                    "رضا",
                    "امیر",
                    "سعید",
                    "محسن",
                    "حامد",
                    "مسعود",
                    "فرهاد",
                    "بهروز",
                    "کیوان",
                    "آرمان",
                    "پوریا",
                    "آرین",
                    "سینا",
                    "دانیال",
                };

                // Persian female names
                var persianFemaleNames = new[]
                {
                    "فاطمه",
                    "زهرا",
                    "مریم",
                    "آیدا",
                    "نرگس",
                    "پریسا",
                    "سارا",
                    "نازنین",
                    "مینا",
                    "شیما",
                    "الهام",
                    "نیلوفر",
                    "مهسا",
                    "طاهره",
                    "زینب",
                    "نگار",
                    "ریحانه",
                    "سمیرا",
                    "لیلا",
                    "مهناز",
                };

                var persianLastNames = new[]
                {
                    "احمدی",
                    "محمدی",
                    "حسینی",
                    "رضایی",
                    "موسوی",
                    "کریمی",
                    "حسنی",
                    "صادقی",
                    "مرادی",
                    "علوی",
                    "قاسمی",
                    "بابایی",
                    "نوری",
                    "صالحی",
                    "طاهری",
                    "کاظمی",
                    "جعفری",
                    "رحیمی",
                    "فروغی",
                    "کامرانی",
                };

                var customers = new List<Customer>();
                var now = DateTime.Now;

                // Create 5-10 customers (random number)
                var customerCount = random.Next(5, 11);
                _logger.LogInformation(
                    $"Creating {customerCount} test customers with proper gender assignment"
                );

                for (int i = 1; i <= customerCount; i++)
                {
                    // Randomly choose gender first
                    bool isMale = random.Next(0, 2) == 0;

                    // Select appropriate name based on gender
                    string firstName;
                    if (isMale)
                    {
                        firstName = persianMaleNames[random.Next(persianMaleNames.Length)];
                    }
                    else
                    {
                        firstName = persianFemaleNames[random.Next(persianFemaleNames.Length)];
                    }

                    var lastName = persianLastNames[random.Next(persianLastNames.Length)];

                    var customer = new Customer
                    {
                        FullName = $"{firstName} {lastName}",
                        PhoneNumber = $"0912{random.Next(1000000, 9999999)}",
                        Email = $"customer{i}@test.com",
                        NationalId =
                            $"{random.Next(100, 999)}{random.Next(100000, 999999)}{random.Next(100, 999)}",
                        Address = $"تهران، خیابان {random.Next(1, 50)}، پلاک {random.Next(1, 200)}",
                        IsActive = true,
                        IsSystem = false,
                        Gender = isMale, // true for male, false for female
                        CreatedAt = now.AddDays(-random.Next(1, 365)), // Random creation date within last year
                    };

                    customers.Add(customer);
                }

                _context.Customers.AddRange(customers);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    $"Created {customerCount} test customers successfully with proper gender assignment"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while seeding test customers");
                throw;
            }
        }

        /// <summary>
        /// Initialize currency pools with starting balances - unified from ForexDbContext
        /// </summary>
        private async Task SeedCurrencyPoolsAsync()
        {
            try
            {
                var existingPools = await _context.CurrencyPools.CountAsync();
                if (existingPools > 0)
                {
                    _logger.LogInformation(
                        $"{existingPools} currency pools already exist, skipping pool seeding"
                    );
                    return;
                }

                var currencies = await _context.Currencies.OrderBy(c => c.Id).ToListAsync();

                var pools = new List<CurrencyPool>();

                foreach (var currency in currencies)
                {
                    var pool = new CurrencyPool
                    {
                        CurrencyId = currency.Id,
                        CurrencyCode = currency.Code,
                        Balance = 0,
                        TotalBought = 0,
                        TotalSold = 0,
                        ActiveBuyOrderCount = 0,
                        ActiveSellOrderCount = 0,
                        RiskLevel = PoolRiskLevel.Low,
                        IsActive = true,
                        LastUpdated = DateTime.UtcNow,
                        Notes = $"{currency.Name} pool - initial setup",
                    };
                    pools.Add(pool);
                }

                _context.CurrencyPools.AddRange(pools);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Successfully created {pools.Count} currency pools");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while seeding currency pools");
                throw;
            }
        }

        /// <summary>
        /// Seed default system settings including website branding
        /// </summary>
        private async Task SeedDefaultSettingsAsync()
        {
            try
            {
                var now = DateTime.Now;
                var settingsToAdd = new List<SystemSettings>();

                // Define all default settings with their keys
                var defaultSettingsData = new Dictionary<
                    string,
                    (string Value, string Description, string DataType)
                >
                {
                    // Website Branding Settings
                    {
                        SettingKeys.WebsiteName,
                        ("سامانه معاملات اکسورا", "نام وب‌سایت", "string")
                    },
                    { SettingKeys.CompanyName, ("گروه اکسورا", "نام شرکت", "string") },
                    {
                        SettingKeys.CompanyWebsite,
                        ("https://Exsora.iranexpedia.ir", "وب‌سایت شرکت", "string")
                    },
                };

                // Check each setting and add only if it doesn't exist
                foreach (var (settingKey, (value, description, dataType)) in defaultSettingsData)
                {
                    var existingSetting = await _context.SystemSettings.FirstOrDefaultAsync(s =>
                        s.SettingKey == settingKey
                    );

                    if (existingSetting == null)
                    {
                        settingsToAdd.Add(
                            new SystemSettings
                            {
                                SettingKey = settingKey,
                                SettingValue = value,
                                Description = description,
                                DataType = dataType,
                                IsActive = true,
                                CreatedAt = now,
                                UpdatedAt = now,
                                UpdatedBy = "DataSeedService",
                            }
                        );
                    }
                }

                if (settingsToAdd.Any())
                {
                    _context.SystemSettings.AddRange(settingsToAdd);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation(
                        $"Successfully seeded {settingsToAdd.Count} new default system settings"
                    );
                }
                else
                {
                    _logger.LogInformation(
                        "All default system settings already exist, skipping settings seeding"
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while seeding default system settings");
                throw;
            }
        }

        /// <summary>
        /// Seeds default permissions for different user roles.
        /// </summary>
        private async Task SeedRolePermissionsAsync()
        {
            _logger.LogInformation("Seeding role permissions...");

            var allPermissions = new List<string>
            {
                // Order Management
                Permissions.Order_View,
                Permissions.Order_Detail,
                Permissions.Order_Create,
                Permissions.Order_Edit,
                Permissions.Order_Delete,
                // Document Management
                Permissions.Documents_View,
                Permissions.Documents_Detail,
                Permissions.Documents_Create,
                Permissions.Documents_Edit,
                Permissions.Documents_Delete,
                Permissions.Documents_Confirm,
                // Reports
                Permissions.Reports,
                Permissions.Customer_Reports,
                Permissions.All_Customers_Balances,
                Permissions.Bank_Account_Reports,
                Permissions.Admin_Reports,
                Permissions.Pool_Reports,
                Permissions.Order_Reports,
                Permissions.Document_Reports,
                Permissions.Pool_Summary_Reports,
                Permissions.Customer_BankHistory_Report,
                Permissions.Expenses_Report,
                // Tasks
                Permissions.Tasks_View,
                Permissions.Tasks_Detail,
                Permissions.Tasks_Create,
                Permissions.Tasks_Edit,
                Permissions.Tasks_Delete,
                // Advance Management
                Permissions.Advance_Management,
                // Bank Accounts
                Permissions.Bank_Accounts_View,
                Permissions.Bank_Accounts_Detail,
                Permissions.Bank_Accounts_Create,
                Permissions.Bank_Accounts_Edit,
                Permissions.Bank_Accounts_Delete,
                // Customers
                Permissions.Customers_View,
                Permissions.Customers_Detail,
                Permissions.Customers_Create,
                Permissions.Customers_Edit,
                Permissions.Customers_Delete,
                // Exchange Rates
                Permissions.Exchange_Rates_Management,
                // Profile
                Permissions.Profile_View,
                // Admin Management
                Permissions.Manage_Admins,
                // Database Management
                Permissions.Database_Management,
            };

            var rolePermissionsMap = new Dictionary<UserRole, List<string>>
            {
                { UserRole.Programmer, allPermissions }, // Programmer has all permissions
                {
                    UserRole.Admin,
                    new List<string>
                    {
                        Permissions.Documents_View,
                        Permissions.Documents_Create,
                        Permissions.Documents_Edit,
                        Permissions.Documents_Delete,
                        
                    }
                },
                {
                    UserRole.Operator,
                    new List<string>
                    {
                        Permissions.Documents_View,
                        Permissions.Documents_Create,
                        // Operators can create documents but not edit or delete them initially
                        // Operators can view users but not create, edit, change roles, or delete them
                    }
                },
                { UserRole.Customer, new List<string>() }, // Customers have no admin permissions
            };

            var permissionsToAdd = new List<RolePermission>();

            foreach (var entry in rolePermissionsMap)
            {
                var role = entry.Key;
                var permissionsForRole = entry.Value;

                foreach (var permissionName in permissionsForRole)
                {
                    var exists = await _context.RolePermissions.AnyAsync(rp =>
                        rp.RoleName == role.ToString() && rp.PermissionName == permissionName
                    );

                    if (!exists)
                    {
                        permissionsToAdd.Add(
                            new RolePermission
                            {
                                RoleName = role.ToString(),
                                PermissionName = permissionName.ToString(),
                            }
                        );
                    }
                }
            }

            if (permissionsToAdd.Any())
            {
                _context.RolePermissions.AddRange(permissionsToAdd);
                await _context.SaveChangesAsync();
                _logger.LogInformation(
                    $"Successfully seeded {permissionsToAdd.Count} new role permissions."
                );
            }
            else
            {
                _logger.LogInformation("All role permissions already exist, skipping seeding.");
            }
        }
    }
}
