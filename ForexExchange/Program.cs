using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using ForexExchange.Models;
using ForexExchange.Services;
using ForexExchange.Authorization; // Add for custom authorization
using Microsoft.AspNetCore.Authorization; // Add for IAuthorizationHandler
using ForexExchange.Hubs;
using ForexExchange.Services.Notifications;
using ForexExchange.Services.Notifications.Providers;
using Serilog;
using Serilog.Events;
using System.IO;

Directory.CreateDirectory("Logs");

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: Path.Combine("Logs", "all-logs-.txt"),
        rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Bootstrapping application...");

    var builder = WebApplication.CreateBuilder(args);

    // This project uses the non-default file name "appsetting.json" (singular),
    // which ASP.NET Core does not load automatically. Register it explicitly.
    builder.Configuration
        .AddJsonFile("appsetting.json", optional: true, reloadOnChange: true)
        .AddJsonFile(
            $"appsetting.{builder.Environment.EnvironmentName}.json",
            optional: true,
            reloadOnChange: true
        );

    builder.Host.UseSerilog();

    var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"]
        ?? Path.Combine(builder.Environment.ContentRootPath, "Logs", "DataProtectionKeys");

    Directory.CreateDirectory(dataProtectionKeysPath);

    builder.Services.AddDataProtection()
        .SetApplicationName("ForexExchange")
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));

    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders =
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

        // Plesk and other hosting panels often terminate TLS before ASP.NET Core.
        // Accept forwarded headers from the hosting proxy so cookies and redirects
        // see the original public HTTPS request.
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });

    // ------------------------------
    // Controllers
    // ------------------------------
    builder.Services.AddControllersWithViews()
        .AddNewtonsoftJson(options =>
        {
            options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
            options.SerializerSettings.DateFormatString = "yyyy-MM-dd HH:mm:ss";
            options.SerializerSettings.ContractResolver =
                new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver();
        });

    // ------------------------------
    // Database
    // ------------------------------
    builder.Services.AddDbContext<ForexDbContext>(options =>
    {
        var connectionString =
            builder.Configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=ForexExchange.db";

        if (!connectionString.Contains("Cache=Shared", StringComparison.OrdinalIgnoreCase))
            connectionString = connectionString.TrimEnd(';') + ";Cache=Shared";

        options.UseSqlite(connectionString);
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    });

    // ------------------------------
    // Identity
    // ------------------------------
    builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;

        options.User.RequireUniqueEmail = false;
        options.User.AllowedUserNameCharacters =
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";

        options.SignIn.RequireConfirmedEmail = false;

        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(30);
        options.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddEntityFrameworkStores<ForexDbContext>()
    .AddDefaultTokenProviders();

    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.Cookie.Name = ".ForexExchange.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Error/AccessDenied";
        options.LogoutPath = "/Account/Logout";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    });

    builder.Services.Configure<IdentityOptions>(options =>
    {
        options.ClaimsIdentity.UserIdClaimType =
            System.Security.Claims.ClaimTypes.NameIdentifier;
    });

    builder.Services.AddScoped<RoleManager<IdentityRole>>();

    // ------------------------------
    // Authorization - Granular Permissions
    // ------------------------------
    builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
    builder.Services.AddAuthorization(options =>
    {
        // Dynamically add policies for each permission
        var allPermissions = typeof(ForexExchange.Models.Permissions)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy)
            .Where(fi => fi.IsLiteral && !fi.IsInitOnly && fi.FieldType == typeof(string))
            .Select(fi => fi.GetRawConstantValue()?.ToString() ?? string.Empty)
            .ToList();

        foreach (var permission in allPermissions)
        {
            options.AddPolicy(permission, policy => policy.Requirements.Add(new ForexExchange.Authorization.PermissionRequirement(permission)));
        }
    });

    // ------------------------------
    // SignalR
    // ------------------------------
    builder.Services.AddSignalR()
        .AddHubOptions<ForexExchange.Hubs.NotificationHub>(o =>
        {
            o.EnableDetailedErrors = true;
        });

    builder.Services.AddSingleton<IUserIdProvider, CustomUserIdProvider>();
#pragma warning disable CA1416 // Validate platform compatibility

    builder.Services.AddSingleton<FinancialSyncProvider>();
#pragma warning restore CA1416 // Validate platform compatibility

    // ------------------------------
    // Core Services
    // ------------------------------

    builder.Services.AddHttpClient();
    builder.Services.AddHttpContextAccessor();

    builder.Services.AddScoped<IOcrService, OpenRouterOcrService>();
    builder.Services.AddScoped<IEmailService, EmailService>();
    builder.Services.AddScoped<IPermissionService, PermissionService>(); // Register Permission Service
    builder.Services.AddScoped<IBankStatementService, BankStatementService>();
    builder.Services.AddScoped<ICurrencyPoolService, CurrencyPoolService>();
    builder.Services.AddScoped<IDataSeedService, DataSeedService>();
    builder.Services.AddScoped<IWebScrapingService>(_ => new NullWebScrapingService());
    builder.Services.AddScoped<ISettingsService, SettingsService>();
    builder.Services.AddScoped<CustomerDebtCreditService>();
    builder.Services.AddScoped<AdminActivityService>();
    builder.Services.AddScoped<AdminNotificationService>();

    builder.Services.AddScoped<ICustomerBalanceService, CustomerBalanceService>();
    builder.Services.AddScoped<IBankAccountBalanceService, BankAccountBalanceService>();
    builder.Services.AddScoped<IShareableLinkService, ShareableLinkService>();
    builder.Services.AddScoped<CustomerFinancialHistoryService>();
    builder.Services.AddScoped<PoolFinancialHistoryService>();
    builder.Services.AddScoped<BankAccountFinancialHistoryService>();

    builder.Services.AddSingleton<ITotpService, TotpService>();
    builder.Services.AddScoped<ICentralFinancialService, CentralFinancialService>();
    builder.Services.AddScoped<IOrderDataService, OrderDataService>();
    builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();
    builder.Services.AddScoped<IVapidService, VapidService>();
    builder.Services.AddScoped<ExcelExportService>();
    builder.Services.AddScoped<IFileUploadService, FileUploadService>();
    builder.Services.AddScoped<ITaskManagementService, TaskManagementService>();
    builder.Services.AddScoped<ICsvImportService, CsvImportService>();
    builder.Services.AddScoped<ICurrencyConversionService, CurrencyConversionService>();

    // ------------------------------
    // Notification Providers
    // ------------------------------
    builder.Services.AddScoped<SignalRNotificationProvider>();
    builder.Services.AddScoped<TelegramNotificationProvider>();

    builder.Services.AddScoped<INotificationHub>(sp =>
    {
        var hub = new ForexExchange.Services.Notifications.NotificationHub(
            sp.GetRequiredService<ForexDbContext>(),
            sp.GetRequiredService<ILogger<ForexExchange.Services.Notifications.NotificationHub>>(),
            sp.GetRequiredService<IConfiguration>(),
            sp.GetRequiredService<IWebHostEnvironment>(),
            sp.GetRequiredService<IHttpContextAccessor>(),
            sp.GetRequiredService<UserManager<ApplicationUser>>());

        hub.RegisterProvider(sp.GetRequiredService<SignalRNotificationProvider>());
        hub.RegisterProvider(sp.GetRequiredService<TelegramNotificationProvider>());

        return hub;
    });

    var app = builder.Build();

    // ------------------------------
    // Global Exception Catcher
    // ------------------------------
    app.Use(async (context, next) =>
    {
        try
        {
            await next();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unhandled exception for {Path}", context.Request.Path);

            try
            {
                var notificationHub =
                    context.RequestServices.GetRequiredService<INotificationHub>();
                var userId = context.User.FindFirst(
                    System.Security.Claims.ClaimTypes.NameIdentifier
                )?.Value;

                await notificationHub.SendSystemNotificationAsync(
                    title: "🚨 خطای مدیریت‌نشده سیستم",
                    message: ex.Message,
                    eventType: NotificationEventType.SystemError,
                    userId: userId,
                    navigationUrl: context.Request.Path,
                    priority: NotificationPriority.Critical,
                    data: new Dictionary<string, object>
                    {
                        ["path"] = context.Request.Path.ToString(),
                        ["method"] = context.Request.Method,
                        ["exceptionType"] = ex.GetType().Name,
                    }
                );
            }
            catch (Exception notificationException)
            {
                Log.Error(
                    notificationException,
                    "Failed to send Telegram notification for unhandled exception"
                );
            }

            throw;
        }
    });

    app.UseForwardedHeaders();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        app.UseStatusCodePagesWithReExecute("/Error/{0}");
        app.UseHsts();
    }
    else
    {
        app.UseDeveloperExceptionPage();
        app.UseStatusCodePagesWithReExecute("/Error/{0}");
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=ExchangeRates}/{action=Index}/{id?}");

    app.MapHub<ForexExchange.Hubs.NotificationHub>("/notificationHub");

#pragma warning disable CA1416 // Validate platform compatibility

//    app.Services.GetService<FinancialSyncProvider>();
#pragma warning restore CA1416 // Validate platform compatibility
    Log.Information("Application started successfully");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
