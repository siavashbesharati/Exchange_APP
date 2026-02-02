using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using System.IO;
using ForexExchange.Models;
using ForexExchange.Services;
using ForexExchange.Hubs;
using ForexExchange.Services.Notifications.Providers;
using ForexExchange.Services.Notifications;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        // CRITICAL: Only log Error, Fatal (crash), and Critical level logs
        // Filter out all Information, Warning, Debug, and Trace logs
        .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Error)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Error) // Disable EF Core database logs
        .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Error)
        .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Error)
        .MinimumLevel.Error() // Only Error, Fatal, and Critical
        .WriteTo.Logger(lc => lc
            .Filter.ByIncludingOnly(logEvent => 
                logEvent.Level >= Serilog.Events.LogEventLevel.Error) // Only Error, Fatal, Critical
            .WriteTo.Console(
                restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Error,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"))
        .WriteTo.Logger(lc => lc
            .Filter.ByIncludingOnly(logEvent => 
                logEvent.Level >= Serilog.Events.LogEventLevel.Error) // Only Error, Fatal, Critical
            .WriteTo.Async(a => a.File(
                path: Path.Combine("Logs", "errors-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 90, // Keep 90 days of error logs
                fileSizeLimitBytes: 10_000_000,
                rollOnFileSizeLimit: true,
                shared: true,
                flushToDiskInterval: TimeSpan.FromSeconds(1),
                restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Error,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")));
});

// Add services to the container.
builder.Services.AddControllersWithViews()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
        options.SerializerSettings.DateFormatString = "yyyy-MM-dd HH:mm:ss";
        options.SerializerSettings.ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver();
    });

// Add Entity Framework with enhanced logging
builder.Services.AddDbContext<ForexDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
                         "Data Source=ForexExchange.db";
    
    // CRITICAL: Add SQLite concurrency settings to connection string
    // WAL mode allows multiple readers and one writer simultaneously
    // Cache=Shared enables shared cache for better concurrency
    if (!connectionString.Contains("Cache=Shared", StringComparison.OrdinalIgnoreCase))
    {
        connectionString = connectionString.TrimEnd(';') + ";Cache=Shared";
    }
    
    options.UseSqlite(connectionString);
    
    // DISABLED: No database logging in production or development
    // Only errors will be logged through Serilog configuration above
    options.EnableSensitiveDataLogging(false);
    options.EnableDetailedErrors(false);
    
    // Disable EF Core command logging completely - no database query logs
    // This prevents heavy logging of all SQL queries
});

// Add Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password requirements
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;

    // User requirements
    options.User.RequireUniqueEmail = false;
    options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+"; // Allow phone numbers

    // Sign in requirements
    options.SignIn.RequireConfirmedEmail = false;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(30);
    options.Lockout.MaxFailedAccessAttempts = 5;
})
.AddEntityFrameworkStores<ForexDbContext>()
.AddDefaultTokenProviders();

// Configure cookie authentication
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Error/AccessDenied";
    options.LogoutPath = "/Account/Logout";
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
});

// Add RoleManager
builder.Services.AddScoped<RoleManager<IdentityRole>>();

// Add HttpClient for OpenRouter API
builder.Services.AddHttpClient();
// Add HttpContextAccessor for admin activity logging
builder.Services.AddHttpContextAccessor();

// Add SignalR with custom user ID provider
builder.Services.AddSignalR().AddHubOptions<ForexExchange.Hubs.NotificationHub>(options =>
{
    // Configure SignalR to use user ID instead of username for identification
    options.EnableDetailedErrors = true;
});

// Register custom user ID provider for SignalR
builder.Services.AddSingleton<IUserIdProvider, CustomUserIdProvider>();

// Configure SignalR to use the user ID claim for user identification
builder.Services.Configure<IdentityOptions>(options =>
{
    // This ensures SignalR can properly identify users by their ID
    options.ClaimsIdentity.UserIdClaimType = System.Security.Claims.ClaimTypes.NameIdentifier;
});

// Add Services
builder.Services.AddScoped<IOcrService, OpenRouterOcrService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IBankStatementService, BankStatementService>();
builder.Services.AddScoped<ICurrencyPoolService, CurrencyPoolService>();
builder.Services.AddScoped<IDataSeedService, DataSeedService>();
// DISABLED: Web scraping service - using null implementation
builder.Services.AddScoped<IWebScrapingService>(provider => new NullWebScrapingService());
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<CustomerDebtCreditService>();
builder.Services.AddScoped<AdminActivityService>();
builder.Services.AddScoped<AdminNotificationService>();
// New balance management services
builder.Services.AddScoped<ICustomerBalanceService, CustomerBalanceService>();
builder.Services.AddScoped<IBankAccountBalanceService, BankAccountBalanceService>();
builder.Services.AddScoped<IShareableLinkService, ShareableLinkService>();
// Customer financial history service
builder.Services.AddScoped<CustomerFinancialHistoryService>();
// Pool financial history service
builder.Services.AddScoped<PoolFinancialHistoryService>();
// Bank account financial history service
builder.Services.AddScoped<BankAccountFinancialHistoryService>();
builder.Services.AddSingleton<ITotpService, TotpService>();
// Central Financial Service - Event Sourcing with Complete Audit Trail
builder.Services.AddScoped<ICentralFinancialService, CentralFinancialService>();
// Order data preparation service - SRP for shared order validation logic
builder.Services.AddScoped<IOrderDataService, OrderDataService>();
// Push notification services
builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();
builder.Services.AddScoped<IVapidService, VapidService>();
// Excel export service
builder.Services.AddScoped<ExcelExportService>();
// File upload service for logo management
builder.Services.AddScoped<IFileUploadService, FileUploadService>();
// Task management service - simplified
builder.Services.AddScoped<ITaskManagementService, TaskManagementService>();
// CSV import service - temp bank accounts for document import only
builder.Services.AddScoped<ICsvImportService, CsvImportService>();

// Central notification system
builder.Services.AddScoped<INotificationHub>(serviceProvider =>
{
    var context = serviceProvider.GetRequiredService<ForexDbContext>();
    var logger = serviceProvider.GetRequiredService<ILogger<ForexExchange.Services.Notifications.NotificationHub>>();
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var environment = serviceProvider.GetRequiredService<IWebHostEnvironment>();

    var hub = new ForexExchange.Services.Notifications.NotificationHub(context, logger, configuration, environment);

    // Register providers
    var signalRProvider = serviceProvider.GetRequiredService<SignalRNotificationProvider>();
    var pushProvider = serviceProvider.GetRequiredService<PushNotificationProvider>();
    var smsProvider = serviceProvider.GetRequiredService<SmsNotificationProvider>();
    var emailProvider = serviceProvider.GetRequiredService<EmailNotificationProvider>();
    var telegramProvider = serviceProvider.GetRequiredService<TelegramNotificationProvider>();

    hub.RegisterProvider(signalRProvider);
    hub.RegisterProvider(pushProvider);
    hub.RegisterProvider(smsProvider);
    hub.RegisterProvider(emailProvider);
    hub.RegisterProvider(telegramProvider);

    return hub;
});

// Notification providers - register as individual services, not as INotificationProvider
builder.Services.AddScoped<SignalRNotificationProvider>();
builder.Services.AddScoped<PushNotificationProvider>();
builder.Services.AddScoped<SmsNotificationProvider>();
builder.Services.AddScoped<EmailNotificationProvider>();
builder.Services.AddScoped<TelegramNotificationProvider>();
builder.Services.AddScoped<ICurrencyConversionService, CurrencyConversionService>();

// Background services - simplified (TaskSchedulerService removed)

var app = builder.Build();

// Auto-apply migrations and seed data
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var dbContext = services.GetRequiredService<ForexDbContext>();

        // Check if there are pending migrations
        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
        if (pendingMigrations.Any())
        {
            logger.LogInformation("Found {Count} pending migrations. Applying...", pendingMigrations.Count());
            foreach (var migration in pendingMigrations)
            {
                logger.LogInformation("Pending migration: {Migration}", migration);
            }

            try
            {
                // Apply all pending migrations with detailed error handling
                await dbContext.Database.MigrateAsync();
                logger.LogInformation("All migrations applied successfully");
            }
            catch (Exception migrationEx)
            {
                logger.LogError(migrationEx, "Failed to apply migrations automatically. Manual intervention may be required.");

                // Option 1: Force re-creation of database (USE WITH CAUTION - DATA LOSS!)
                // Uncomment the lines below ONLY if you want to recreate the database from scratch
                // logger.LogWarning("RECREATING DATABASE FROM SCRATCH - ALL DATA WILL BE LOST!");
                // await dbContext.Database.EnsureDeletedAsync();
                // await dbContext.Database.MigrateAsync();
                // await dataSeedService.SeedDataAsync();
                // logger.LogWarning("Database recreated successfully");

                // Option 2: Continue without migrations (not recommended for production)
                logger.LogWarning("Continuing application startup without applying migrations. This may cause runtime errors.");
                // throw; // Uncomment to stop application startup on migration failure
            }
        }
        else
        {
            logger.LogInformation("Database is up to date. No pending migrations found");
        }



        // // Seed initial data
        var dataSeedService = services.GetRequiredService<IDataSeedService>();
        await dataSeedService.SeedDataAsync();

        logger.LogInformation("Application startup completed successfully");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while migrating or seeding the database");
        throw; // Re-throw to prevent app from starting with incomplete database
    }
}


// Global exception handler middleware - catches all unhandled exceptions
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Unhandled exception: {ExceptionType} - {Message} | Path: {Path}", 
            ex.GetType().Name, ex.Message, context.Request.Path);
        
        if (ex.InnerException != null)
        {
            logger.LogError("Inner Exception: {InnerExceptionType} - {InnerExceptionMessage}", 
                ex.InnerException.GetType().Name, ex.InnerException.Message);
        }
        
        // Log stack trace for debugging
        logger.LogError("Stack Trace: {StackTrace}", ex.StackTrace);
        
        throw; // Re-throw to let exception handler middleware handle it
    }
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    // Production error handling
    app.UseExceptionHandler("/Error");
    app.UseStatusCodePagesWithReExecute("/Error/{0}");

    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
else
{
    // Development error handling
    app.UseDeveloperExceptionPage();
    // Also add status code pages for development testing
    app.UseStatusCodePagesWithReExecute("/Error/{0}");
}

app.UseHttpsRedirection();
app.UseRouting();

// Add authentication middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=ExchangeRates}/{action=Index}/{id?}");

// Map SignalR hubs
app.MapHub<ForexExchange.Hubs.NotificationHub>("/notificationHub");

try
{
    Log.Information("Application starting up");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
