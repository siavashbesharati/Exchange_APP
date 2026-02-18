using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace ForexExchange.Models
{
      public class ForexDbContext : IdentityDbContext<ApplicationUser>
      {
            private bool _pragmaConfigured = false;

            public ForexDbContext(DbContextOptions<ForexDbContext> options) : base(options)
            {
            }

            public ForexDbContext()
            {

            }

            // در فایل ForexDbContext.cs
            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                  optionsBuilder.LogTo(message =>
                  {
                        if (message.Contains("DELETE FROM \"AccountingDocuments\""))
                        {
                              Console.WriteLine("--------------------------------------------------");
                              Console.WriteLine("ALERT: PHYSICAL DELETE DETECTED!");
                              Console.WriteLine(Environment.StackTrace); // این خط به شما می‌گوید دقیقا کدام متد دستور حذف داده
                              Console.WriteLine("--------------------------------------------------");
                        }
                  }, LogLevel.Information);
            }

            public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            {
                  // CRITICAL: Configure SQLite PRAGMA settings once per connection for better concurrency
                  // These settings improve SQLite's ability to handle concurrent reads and writes
                  if (!_pragmaConfigured)
                  {
                        try
                        {
                              await Database.ExecuteSqlRawAsync("PRAGMA journal_mode = WAL;", cancellationToken);
                              await Database.ExecuteSqlRawAsync("PRAGMA busy_timeout = 5000;", cancellationToken);
                              // PRAGMA synchronous = NORMAL often fails in WAL mode (logs "Failed executing DbCommand") - skip it
                              await Database.ExecuteSqlRawAsync("PRAGMA cache_size = -64000;", cancellationToken); // 64MB cache
                              _pragmaConfigured = true;
                        }
                        catch
                        {
                              // Ignore errors - database might not be ready yet
                              // Settings will be applied on next SaveChangesAsync
                        }
                  }

                  return await base.SaveChangesAsync(cancellationToken);
            }

            public DbSet<Customer> Customers { get; set; }
            public DbSet<Order> Orders { get; set; }

            public DbSet<ExchangeRate> ExchangeRates { get; set; }
            public DbSet<Notification> Notifications { get; set; }
            public DbSet<SystemSettings> SystemSettings { get; set; }
            public DbSet<CurrencyPool> CurrencyPools { get; set; }
            public DbSet<Currency> Currencies { get; set; }
            public DbSet<AdminActivity> AdminActivities { get; set; }
            public DbSet<BankAccount> BankAccounts { get; set; }
            public DbSet<BankAccountBalance> BankAccountBalances { get; set; }
            public DbSet<CustomerBalance> CustomerBalances { get; set; }
            public DbSet<AccountingDocument> AccountingDocuments { get; set; }
            public DbSet<ShareableLink> ShareableLinks { get; set; }
            public DbSet<PushSubscription> PushSubscriptions { get; set; }
            public DbSet<PushNotificationLog> PushNotificationLogs { get; set; }
            public DbSet<VapidConfiguration> VapidConfigurations { get; set; }

            // NEW: History Tables for Event Sourcing - Zero Logic Change
            public DbSet<CustomerBalanceHistory> CustomerBalanceHistory { get; set; }
            public DbSet<CurrencyPoolHistory> CurrencyPoolHistory { get; set; }
            public DbSet<BankAccountBalanceHistory> BankAccountBalanceHistory { get; set; }

            // Task Management - Simplified
            public DbSet<TaskItem> TaskItems { get; set; }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                  base.OnModelCreating(modelBuilder);

                  // Customer configurations
                  modelBuilder.Entity<Customer>(entity =>
                  {
                        entity.HasKey(e => e.Id);
                        entity.Property(e => e.FullName).IsRequired().HasMaxLength(100);
                        entity.Property(e => e.PhoneNumber).IsRequired().HasMaxLength(20);
                        entity.HasIndex(e => e.PhoneNumber).IsUnique();
                  });

                  // Order configurations
                  modelBuilder.Entity<Order>(entity =>
                  {
                        entity.HasKey(e => e.Id);
                        entity.HasOne(e => e.Customer)
                        .WithMany(e => e.Orders)
                        .HasForeignKey(e => e.CustomerId)
                        .OnDelete(DeleteBehavior.Restrict);
                  });


                  // ExchangeRate configurations
                  modelBuilder.Entity<ExchangeRate>(entity =>
                  {
                        entity.HasKey(e => e.Id);
                        entity.HasIndex(e => new { e.FromCurrencyId, e.ToCurrencyId, e.IsActive });
                  });

                  // Notification configurations
                  modelBuilder.Entity<Notification>(entity =>
                  {
                        entity.HasKey(e => e.Id);
                        entity.HasOne(e => e.Customer)
                        .WithMany(e => e.Notifications)
                        .HasForeignKey(e => e.CustomerId)
                        .OnDelete(DeleteBehavior.Cascade);
                        entity.HasIndex(e => new { e.CustomerId, e.IsRead });
                        entity.HasIndex(e => e.CreatedAt);
                  });

                  // SystemSettings configurations
                  modelBuilder.Entity<SystemSettings>(entity =>
                  {
                        entity.HasKey(e => e.Id);
                        entity.HasIndex(e => e.SettingKey).IsUnique();
                        entity.Property(e => e.SettingKey).IsRequired().HasMaxLength(100);
                        entity.Property(e => e.SettingValue).IsRequired().HasMaxLength(500);
                        entity.Property(e => e.DataType).HasMaxLength(50);
                        entity.Property(e => e.UpdatedBy).HasMaxLength(100);
                  });

                  // CurrencyPool configurations
                  modelBuilder.Entity<CurrencyPool>(entity =>
                  {
                        entity.HasKey(e => e.Id);
                        entity.HasIndex(e => e.CurrencyId).IsUnique();
                        entity.Property(e => e.Balance).HasColumnType("decimal(18,8)");
                        entity.Property(e => e.TotalBought).HasColumnType("decimal(18,8)");
                        entity.Property(e => e.TotalSold).HasColumnType("decimal(18,8)");
                        entity.Property(e => e.Notes).HasMaxLength(500);
                        entity.HasIndex(e => new { e.CurrencyId, e.IsActive });
                        entity.HasIndex(e => e.LastUpdated);
                        entity.HasIndex(e => e.RiskLevel);

                        entity.HasOne(e => e.Currency)
                      .WithMany(c => c.CurrencyPools)
                      .HasForeignKey(e => e.CurrencyId)
                      .OnDelete(DeleteBehavior.Restrict);
                  });

                  // Currency configurations
                  modelBuilder.Entity<Currency>(entity =>
                  {
                        entity.HasKey(e => e.Id);
                        entity.HasIndex(e => e.Code).IsUnique();
                        entity.Property(e => e.Code).IsRequired().HasMaxLength(3);
                        entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
                        entity.Property(e => e.PersianName).IsRequired().HasMaxLength(50);
                        entity.Property(e => e.Symbol).HasMaxLength(5);
                        entity.HasIndex(e => new { e.IsActive, e.DisplayOrder });
                        // Ignore legacy navigation not mapped on ExchangeRate
                        entity.Ignore(e => e.LegacyRates);
                  });

                  // CustomerBalance configurations
                  modelBuilder.Entity<CustomerBalance>(entity =>
                  {
                        entity.HasKey(e => e.Id);
                        entity.Property(e => e.CurrencyCode).IsRequired().HasMaxLength(3);
                        entity.Property(e => e.Balance).HasColumnType("decimal(18,2)");
                        entity.Property(e => e.Notes).HasMaxLength(500);
                        entity.HasOne(e => e.Customer)
                        .WithMany(c => c.Balances)
                        .HasForeignKey(e => e.CustomerId)
                        .OnDelete(DeleteBehavior.Cascade);
                        entity.HasOne(e => e.Currency)
                        .WithMany(c => c.CustomerBalances)
                        .HasForeignKey(e => e.CurrencyId)
                        .OnDelete(DeleteBehavior.Restrict);
                        // Keep both indexes for backward compatibility during migration
                        entity.HasIndex(e => new { e.CustomerId, e.CurrencyCode }).IsUnique();
                        entity.HasIndex(e => new { e.CustomerId, e.CurrencyId }).IsUnique().HasFilter("[CurrencyId] IS NOT NULL");
                  });

                  // ExchangeRate configurations - Updated for cross-currency support
                  modelBuilder.Entity<ExchangeRate>(entity =>
                  {
                        entity.HasKey(e => e.Id);
                        entity.HasIndex(e => new { e.FromCurrencyId, e.ToCurrencyId, e.IsActive }).IsUnique();
                        entity.Property(e => e.Rate).HasColumnType("decimal(18,8)");
                        entity.Property(e => e.AverageBuyRate).HasColumnType("decimal(18,8)");
                        entity.Property(e => e.AverageSellRate).HasColumnType("decimal(18,8)");
                        entity.Property(e => e.TotalBuyVolume).HasColumnType("decimal(18,8)");
                        entity.Property(e => e.TotalSellVolume).HasColumnType("decimal(18,8)");
                        entity.Property(e => e.UpdatedBy).HasMaxLength(50);

                        // Configure foreign key relationships
                        entity.HasOne(e => e.FromCurrency)
                        .WithMany(c => c.FromCurrencyRates)
                        .HasForeignKey(e => e.FromCurrencyId)
                        .OnDelete(DeleteBehavior.Restrict);

                        entity.HasOne(e => e.ToCurrency)
                        .WithMany(c => c.ToCurrencyRates)
                        .HasForeignKey(e => e.ToCurrencyId)
                        .OnDelete(DeleteBehavior.Restrict);
                  });

                  // AdminActivity configurations
                  modelBuilder.Entity<AdminActivity>(entity =>
                  {
                        entity.HasKey(e => e.Id);
                        entity.Property(e => e.AdminUserId).HasMaxLength(450); // ASP.NET Identity User ID length
                        entity.Property(e => e.AdminUsername).HasMaxLength(256);
                        entity.Property(e => e.Description).HasMaxLength(1000);
                        entity.Property(e => e.Details).HasColumnType("TEXT");
                        entity.Property(e => e.IpAddress).HasMaxLength(45);
                        entity.Property(e => e.UserAgent).HasMaxLength(500);
                        entity.Property(e => e.EntityType).HasMaxLength(100);
                        entity.Property(e => e.OldValue).HasColumnType("TEXT");
                        entity.Property(e => e.NewValue).HasColumnType("TEXT");
                        entity.HasIndex(e => e.AdminUserId);
                        entity.HasIndex(e => e.ActivityType);
                        entity.HasIndex(e => e.Timestamp);
                        entity.HasIndex(e => new { e.AdminUserId, e.Timestamp });
                  });

                  // Order configurations - Updated for cross-currency support
                  modelBuilder.Entity<Order>(entity =>
                  {
                        entity.HasKey(e => e.Id);
                        entity.HasOne(e => e.Customer)
                        .WithMany(e => e.Orders)
                        .HasForeignKey(e => e.CustomerId)
                        .OnDelete(DeleteBehavior.Restrict);
                        entity.Property(e => e.FromAmount).HasColumnType("decimal(18,2)");
                        entity.Property(e => e.Rate).HasColumnType("decimal(18,4)");
                        entity.Property(e => e.ToAmount).HasColumnType("decimal(18,2)");
                        entity.Property(e => e.Notes).HasMaxLength(500);
                        // Map currency relationships
                        entity.HasOne(e => e.FromCurrency)
                      .WithMany(c => c.FromCurrencyOrders)
                      .HasForeignKey(e => e.FromCurrencyId)
                      .OnDelete(DeleteBehavior.Restrict);

                        entity.HasOne(e => e.ToCurrency)
                      .WithMany(c => c.ToCurrencyOrders)
                      .HasForeignKey(e => e.ToCurrencyId)
                      .OnDelete(DeleteBehavior.Restrict);

                        // Useful indexes
                        entity.HasIndex(e => new { e.FromCurrencyId, e.ToCurrencyId });
                        entity.HasIndex(e => e.CreatedAt);
                  });

                  // BankAccount configurations
                  modelBuilder.Entity<BankAccount>(entity =>
                  {
                        entity.HasKey(e => e.Id);
                        entity.HasOne(e => e.Customer)
                        .WithMany(e => e.BankAccounts)
                        .HasForeignKey(e => e.CustomerId)
                        .OnDelete(DeleteBehavior.Restrict);
                        entity.HasOne(e => e.Currency)
                        .WithMany(c => c.BankAccounts)
                        .HasForeignKey(e => e.CurrencyId)
                        .OnDelete(DeleteBehavior.Restrict);
                        entity.Property(e => e.BankName).IsRequired().HasMaxLength(100);
                        entity.Property(e => e.AccountNumber).IsRequired().HasMaxLength(50);
                        entity.Property(e => e.AccountHolderName).IsRequired().HasMaxLength(100);
                        entity.Property(e => e.IBAN).HasMaxLength(34);
                        entity.Property(e => e.CardNumberLast4).HasMaxLength(4);
                        entity.Property(e => e.Branch).HasMaxLength(100);
                        entity.Property(e => e.CurrencyCode).IsRequired().HasMaxLength(3);
                        entity.Property(e => e.Notes).HasMaxLength(500);
                        entity.HasIndex(e => new { e.CustomerId, e.IsActive });
                        entity.HasIndex(e => e.AccountNumber);
                        entity.HasIndex(e => e.IsDefault).HasFilter("[IsDefault] = 1");
                  });

                  // BankAccountBalance configurations
                  modelBuilder.Entity<BankAccountBalance>(entity =>
                  {
                        entity.HasKey(e => e.Id);
                        entity.Property(e => e.CurrencyCode).IsRequired().HasMaxLength(3);
                        entity.Property(e => e.Balance).HasColumnType("decimal(18,2)");
                        entity.Property(e => e.Notes).HasMaxLength(500);
                        entity.HasOne(e => e.BankAccount)
                        .WithMany()
                        .HasForeignKey(e => e.BankAccountId)
                        .OnDelete(DeleteBehavior.Cascade);
                        entity.HasOne(e => e.Currency)
                        .WithMany(c => c.BankAccountBalances)
                        .HasForeignKey(e => e.CurrencyId)
                        .OnDelete(DeleteBehavior.Restrict);
                        // Keep both indexes for backward compatibility during migration
                        entity.HasIndex(e => new { e.BankAccountId, e.CurrencyCode }).IsUnique();
                        entity.HasIndex(e => new { e.BankAccountId, e.CurrencyId }).IsUnique().HasFilter("[CurrencyId] IS NOT NULL");
                  });

                  // AccountingDocument configurations
                  modelBuilder.Entity<AccountingDocument>(entity =>
                  {
                        entity.HasKey(e => e.Id);
                        entity.Property(e => e.CurrencyCode).IsRequired().HasMaxLength(3);
                        entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                        entity.Property(e => e.Title).IsRequired().HasMaxLength(100);
                        entity.Property(e => e.Description).HasMaxLength(500);
                        entity.Property(e => e.ReferenceNumber).HasMaxLength(50);
                        entity.Property(e => e.FileName).HasMaxLength(100);
                        entity.Property(e => e.ContentType).HasMaxLength(50);
                        entity.Property(e => e.VerifiedBy).HasMaxLength(100);
                        entity.Property(e => e.Notes).HasMaxLength(500);

                        // Configure Payer relationships
                        entity.HasOne(e => e.PayerCustomer)
                        .WithMany()
                        .HasForeignKey(e => e.PayerCustomerId)
                        .OnDelete(DeleteBehavior.Restrict);

                        entity.HasOne(e => e.PayerBankAccount)
                        .WithMany()
                        .HasForeignKey(e => e.PayerBankAccountId)
                        .OnDelete(DeleteBehavior.Restrict);

                        // Configure Receiver relationships
                        entity.HasOne(e => e.ReceiverCustomer)
                        .WithMany()
                        .HasForeignKey(e => e.ReceiverCustomerId)
                        .OnDelete(DeleteBehavior.Restrict);

                        entity.HasOne(e => e.ReceiverBankAccount)
                        .WithMany()
                        .HasForeignKey(e => e.ReceiverBankAccountId)
                        .OnDelete(DeleteBehavior.Restrict);

                        // Configure Currency relationship
                        entity.HasOne(e => e.Currency)
                        .WithMany(c => c.CurrencyDocuments)
                        .HasForeignKey(e => e.CurrencyId)
                        .OnDelete(DeleteBehavior.Restrict);

                        // Indexes for performance
                        entity.HasIndex(e => e.PayerCustomerId);
                        entity.HasIndex(e => e.ReceiverCustomerId);
                        entity.HasIndex(e => e.PayerBankAccountId);
                        entity.HasIndex(e => e.ReceiverBankAccountId);
                        entity.HasIndex(e => e.CurrencyId);
                        entity.HasIndex(e => e.DocumentDate);
                        entity.HasIndex(e => e.IsVerified);
                        entity.HasIndex(e => e.Type);
                        entity.HasIndex(e => e.ReferenceNumber);
                  });



                  // ApplicationUser configurations
                  modelBuilder.Entity<ApplicationUser>(entity =>
                  {
                        entity.HasIndex(e => e.PhoneNumber).IsUnique();
                        entity.HasOne(e => e.Customer)
                        .WithOne()
                        .HasForeignKey<ApplicationUser>(e => e.CustomerId)
                        .OnDelete(DeleteBehavior.SetNull);
                  });

                  // NOTE: CurrencyPool and ExchangeRate seeding is now handled by DataSeedService
                  // This provides more flexibility, better error handling, and unified seeding logic



                  // ShareableLink configurations
                  modelBuilder.Entity<ShareableLink>(entity =>
                  {
                        entity.HasKey(e => e.Id);
                        entity.Property(e => e.Token).IsRequired().HasMaxLength(128);
                        entity.Property(e => e.CreatedBy).HasMaxLength(100);
                        entity.Property(e => e.Description).HasMaxLength(200);
                        entity.HasOne(e => e.Customer)
                        .WithMany()
                        .HasForeignKey(e => e.CustomerId)
                        .OnDelete(DeleteBehavior.Cascade);
                        entity.HasIndex(e => e.Token).IsUnique();
                        entity.HasIndex(e => e.CustomerId);
                        entity.HasIndex(e => new { e.IsActive, e.ExpiresAt });
                  });

                  // PushSubscription configurations
                  modelBuilder.Entity<PushSubscription>(entity =>
                  {
                        entity.HasKey(e => e.Id);
                        entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
                        entity.Property(e => e.Endpoint).IsRequired().HasMaxLength(500);
                        entity.Property(e => e.P256dhKey).IsRequired().HasMaxLength(200);
                        entity.Property(e => e.AuthKey).IsRequired().HasMaxLength(200);
                        entity.Property(e => e.UserAgent).HasMaxLength(500);
                        entity.Property(e => e.DeviceType).HasMaxLength(50);

                        // Configure foreign key relationship to ApplicationUser (AspNet Identity)
                        entity.HasOne<ApplicationUser>()
                        .WithMany()
                        .HasForeignKey(e => e.UserId)
                        .HasPrincipalKey(u => u.Id)
                        .OnDelete(DeleteBehavior.Cascade)
                        .HasConstraintName("FK_PushSubscriptions_AspNetUsers_UserId");

                        entity.HasIndex(e => e.UserId);
                        entity.HasIndex(e => e.Endpoint);
                        entity.HasIndex(e => new { e.IsActive, e.UserId });
                  });

                  // PushNotificationLog configurations
                  modelBuilder.Entity<PushNotificationLog>(entity =>
                  {
                        entity.HasKey(e => e.Id);
                        entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                        entity.Property(e => e.Message).IsRequired().HasMaxLength(1000);
                        entity.Property(e => e.Type).HasMaxLength(20);
                        entity.Property(e => e.Data).HasColumnType("TEXT");
                        entity.Property(e => e.ErrorMessage).HasMaxLength(500);

                        // Configure foreign key relationship to PushSubscription
                        entity.HasOne(e => e.PushSubscription)
                        .WithMany()
                        .HasForeignKey(e => e.PushSubscriptionId)
                        .HasPrincipalKey(ps => ps.Id)
                        .OnDelete(DeleteBehavior.Cascade)
                        .HasConstraintName("FK_PushNotificationLogs_PushSubscriptions_PushSubscriptionId");

                        entity.HasIndex(e => e.PushSubscriptionId);
                        entity.HasIndex(e => e.SentAt);
                        entity.HasIndex(e => e.WasSuccessful);
                  });

                  // VapidConfiguration configurations
                  modelBuilder.Entity<VapidConfiguration>(entity =>
                  {
                        entity.HasKey(e => e.Id);
                        entity.Property(e => e.ApplicationId).IsRequired().HasMaxLength(50);
                        entity.Property(e => e.Subject).IsRequired().HasMaxLength(500);
                        entity.Property(e => e.PublicKey).IsRequired().HasMaxLength(500);
                        entity.Property(e => e.PrivateKey).IsRequired().HasMaxLength(500);
                        entity.Property(e => e.Notes).HasMaxLength(1000);
                        entity.HasIndex(e => e.ApplicationId).IsUnique();
                        entity.HasIndex(e => e.IsActive);
                        entity.HasIndex(e => e.CreatedAt);
                  });

                  // =============================================================
                  // NEW: History Tables Configurations - Event Sourcing Pattern
                  // =============================================================

                  // CustomerBalanceHistory configurations
                  modelBuilder.Entity<CustomerBalanceHistory>(entity =>
                  {
                        entity.HasKey(e => e.Id);
                        entity.Property(e => e.CurrencyCode).IsRequired().HasMaxLength(3);
                        entity.Property(e => e.TransactionType).IsRequired().HasMaxLength(50);
                        entity.Property(e => e.BalanceBefore).HasColumnType("decimal(18,4)");
                        entity.Property(e => e.TransactionAmount).HasColumnType("decimal(18,4)");
                        entity.Property(e => e.BalanceAfter).HasColumnType("decimal(18,4)");
                        entity.Property(e => e.Description).HasMaxLength(500);
                        entity.Property(e => e.CreatedBy).HasMaxLength(100);

                        // Indexes for optimal performance
                        entity.HasIndex(e => new { e.CustomerId, e.CurrencyCode, e.TransactionDate, e.Id })
                        .HasDatabaseName("IX_CustomerBalanceHistory_Customer_Currency_Latest");
                        entity.HasIndex(e => new { e.TransactionType, e.ReferenceId })
                        .HasDatabaseName("IX_CustomerBalanceHistory_Reference");
                        entity.HasIndex(e => e.TransactionDate)
                        .HasDatabaseName("IX_CustomerBalanceHistory_Date");
                        // PERFORMANCE OPTIMIZATION: Index for filtering by TransactionType and IsDeleted (used in rebuild)
                        entity.HasIndex(e => new { e.TransactionType, e.IsDeleted })
                        .HasDatabaseName("IX_CustomerBalanceHistory_Type_Deleted");

                        // Foreign key to Customer
                        entity.HasOne(e => e.Customer)
                        .WithMany()
                        .HasForeignKey(e => e.CustomerId)
                        .OnDelete(DeleteBehavior.Restrict);

                        // Foreign key to Currency
                        entity.HasOne(e => e.Currency)
                        .WithMany(c => c.CustomerBalanceHistories)
                        .HasForeignKey(e => e.CurrencyId)
                        .OnDelete(DeleteBehavior.Restrict);

                        // Update indexes to include CurrencyId
                        entity.HasIndex(e => new { e.CustomerId, e.CurrencyId, e.TransactionDate, e.Id })
                        .HasDatabaseName("IX_CustomerBalanceHistory_Customer_CurrencyId_Latest")
                        .HasFilter("[CurrencyId] IS NOT NULL");
                  });

                  // CurrencyPoolHistory configurations
                  modelBuilder.Entity<CurrencyPoolHistory>(entity =>
                  {
                        entity.HasKey(e => e.Id);
                        entity.Property(e => e.CurrencyCode).IsRequired().HasMaxLength(3);
                        entity.Property(e => e.TransactionType).IsRequired().HasMaxLength(50);
                        entity.Property(e => e.BalanceBefore).HasColumnType("decimal(18,4)");
                        entity.Property(e => e.TransactionAmount).HasColumnType("decimal(18,4)");
                        entity.Property(e => e.BalanceAfter).HasColumnType("decimal(18,4)");
                        entity.Property(e => e.PoolTransactionType).HasMaxLength(10);
                        entity.Property(e => e.Description).HasMaxLength(500);
                        entity.Property(e => e.CreatedBy).HasMaxLength(100);

                        // Indexes for optimal performance
                        entity.HasIndex(e => new { e.CurrencyCode, e.TransactionDate, e.Id })
                        .HasDatabaseName("IX_CurrencyPoolHistory_Currency_Latest");
                        entity.HasIndex(e => new { e.TransactionType, e.ReferenceId })
                        .HasDatabaseName("IX_CurrencyPoolHistory_Reference");
                        entity.HasIndex(e => e.TransactionDate)
                        .HasDatabaseName("IX_CurrencyPoolHistory_Date");
                        // PERFORMANCE OPTIMIZATION: Index for filtering by TransactionType and IsDeleted (used in rebuild)
                        entity.HasIndex(e => new { e.TransactionType, e.IsDeleted })
                        .HasDatabaseName("IX_CurrencyPoolHistory_Type_Deleted");

                        // Foreign key to Currency
                        entity.HasOne(e => e.Currency)
                        .WithMany(c => c.CurrencyPoolHistories)
                        .HasForeignKey(e => e.CurrencyId)
                        .OnDelete(DeleteBehavior.Restrict);

                        // Update indexes to include CurrencyId
                        entity.HasIndex(e => new { e.CurrencyId, e.TransactionDate, e.Id })
                        .HasDatabaseName("IX_CurrencyPoolHistory_CurrencyId_Latest")
                        .HasFilter("[CurrencyId] IS NOT NULL");
                  });

                  // BankAccountBalanceHistory configurations
                  modelBuilder.Entity<BankAccountBalanceHistory>(entity =>
                  {
                        entity.HasKey(e => e.Id);
                        entity.Property(e => e.TransactionType).IsRequired().HasMaxLength(50);
                        entity.Property(e => e.BalanceBefore).HasColumnType("decimal(18,4)");
                        entity.Property(e => e.TransactionAmount).HasColumnType("decimal(18,4)");
                        entity.Property(e => e.BalanceAfter).HasColumnType("decimal(18,4)");
                        entity.Property(e => e.Description).HasMaxLength(500);
                        entity.Property(e => e.CreatedBy).HasMaxLength(100);

                        // Indexes for optimal performance
                        entity.HasIndex(e => new { e.BankAccountId, e.TransactionDate, e.Id })
                        .HasDatabaseName("IX_BankAccountBalanceHistory_Account_Latest");
                        entity.HasIndex(e => new { e.TransactionType, e.ReferenceId })
                        .HasDatabaseName("IX_BankAccountBalanceHistory_Reference");
                        entity.HasIndex(e => e.TransactionDate)
                        .HasDatabaseName("IX_BankAccountBalanceHistory_Date");
                        // PERFORMANCE OPTIMIZATION: Index for filtering by TransactionType and IsDeleted (used in rebuild)
                        entity.HasIndex(e => new { e.TransactionType, e.IsDeleted })
                        .HasDatabaseName("IX_BankAccountBalanceHistory_Type_Deleted");

                        // Foreign key to BankAccount
                        entity.HasOne(e => e.BankAccount)
                        .WithMany()
                        .HasForeignKey(e => e.BankAccountId)
                        .OnDelete(DeleteBehavior.Restrict);
                  });

                  // Global Query Filters for Soft Delete
                  // Automatically exclude deleted Orders and AccountingDocuments from all queries
                  modelBuilder.Entity<Order>().HasQueryFilter(o => !o.IsDeleted);
                  modelBuilder.Entity<AccountingDocument>().HasQueryFilter(d => !d.IsDeleted);

                  // NOTE: Currency seeding is now handled by DataSeedService
                  // This provides more flexibility and consistency with other seeding operations

                  // Task Management Configurations - Simplified with User Assignment
                  modelBuilder.Entity<TaskItem>(entity =>
                  {
                        entity.HasKey(e => e.Id);
                        entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                        entity.Property(e => e.Description).HasMaxLength(2000);
                        entity.Property(e => e.CreatedAt).IsRequired();
                        entity.Property(e => e.DueDate);
                        entity.Property(e => e.Status).IsRequired();
                        entity.Property(e => e.AssignedToUserId);

                        // Configure relationship with ApplicationUser
                        entity.HasOne(e => e.AssignedToUser)
                        .WithMany()
                        .HasForeignKey(e => e.AssignedToUserId)
                        .OnDelete(DeleteBehavior.SetNull);
                  });
            }
      }
}
