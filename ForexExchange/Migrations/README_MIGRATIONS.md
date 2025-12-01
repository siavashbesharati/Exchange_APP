# راهنمای استفاده از Migration با CLI

این فایل راهنمای استفاده از Entity Framework Core CLI برای مدیریت migrationها است.

## پیش‌نیازها

ابتدا مطمئن شوید که Entity Framework Core Tools نصب شده است:

```bash
dotnet tool install --global dotnet-ef
```

یا برای به‌روزرسانی:

```bash
dotnet tool update --global dotnet-ef
```

## دستورات اصلی

### 1. ایجاد Migration جدید

برای ایجاد یک migration جدید:

```bash
cd ForexExchange
dotnet ef migrations add <MigrationName> --context ForexDbContext
```

مثال:
```bash
dotnet ef migrations add InitialCreate --context ForexDbContext
```

### 2. مشاهده لیست Migrationها

برای مشاهده لیست migrationهای موجود:

```bash
dotnet ef migrations list --context ForexDbContext
```

### 3. اعمال Migrationها به دیتابیس

برای اعمال migrationهای pending به دیتابیس:

```bash
dotnet ef database update --context ForexDbContext
```

برای اعمال migration تا یک migration خاص:

```bash
dotnet ef database update <MigrationName> --context ForexDbContext
```

### 4. حذف آخرین Migration (قبل از اعمال)

اگر migration را ایجاد کرده‌اید اما هنوز اعمال نکرده‌اید:

```bash
dotnet ef migrations remove --context ForexDbContext
```

### 5. مشاهده SQL Script

برای مشاهده SQL که قرار است اجرا شود (بدون اعمال):

```bash
dotnet ef migrations script --context ForexDbContext
```

برای مشاهده SQL بین دو migration:

```bash
dotnet ef migrations script <FromMigration> <ToMigration> --context ForexDbContext
```

### 6. حذف و بازسازی دیتابیس (⚠️ هشدار: تمام داده‌ها حذف می‌شوند)

```bash
dotnet ef database drop --context ForexDbContext --force
dotnet ef database update --context ForexDbContext
```

## نکات مهم

1. **همیشه قبل از اعمال migration، از دیتابیس backup بگیرید**
2. **Migrationها را در محیط Development تست کنید قبل از Production**
3. **Migrationها را به ترتیب زمانی اعمال کنید**
4. **هرگز migrationهای اعمال شده را حذف نکنید**

## حل مشکل خطای "no such column"

اگر خطای `no such column` دریافت می‌کنید:

1. بررسی کنید که آیا migrationها به درستی اعمال شده‌اند:
   ```bash
   dotnet ef migrations list --context ForexDbContext
   ```

2. بررسی کنید که آیا جدول `__EFMigrationsHistory` در دیتابیس وجود دارد:
   ```sql
   SELECT * FROM __EFMigrationsHistory;
   ```

3. اگر migrationها اعمال نشده‌اند، آن‌ها را اعمال کنید:
   ```bash
   dotnet ef database update --context ForexDbContext
   ```

4. اگر مشکل همچنان وجود دارد، ممکن است نیاز به حذف و بازسازی دیتابیس باشد (⚠️ تمام داده‌ها حذف می‌شوند):
   ```bash
   dotnet ef database drop --context ForexDbContext --force
   dotnet ef database update --context ForexDbContext
   ```

## مثال کامل: ایجاد و اعمال Migration جدید

```bash
# 1. رفتن به پوشه پروژه
cd ForexExchange

# 2. ایجاد migration جدید
dotnet ef migrations add AddNewColumnToTable --context ForexDbContext

# 3. بررسی migration ایجاد شده
dotnet ef migrations list --context ForexDbContext

# 4. مشاهده SQL (اختیاری)
dotnet ef migrations script --context ForexDbContext

# 5. اعمال migration به دیتابیس
dotnet ef database update --context ForexDbContext
```

## بررسی وضعیت Migrationها در دیتابیس

برای بررسی اینکه کدام migrationها اعمال شده‌اند، می‌توانید از SQL استفاده کنید:

```sql
SELECT * FROM __EFMigrationsHistory ORDER BY MigrationId;
```

## حذف Migration History از دیتابیس

اگر می‌خواهید migration history را از دیتابیس حذف کنید (برای شروع از صفر):

### روش 1: حذف جدول Migration History (SQLite)

با استفاده از DB Browser for SQLite یا sqlite3 CLI:

```sql
DELETE FROM __EFMigrationsHistory;
```

یا حذف کامل جدول:

```sql
DROP TABLE IF EXISTS __EFMigrationsHistory;
```

### روش 2: استفاده از PowerShell (Windows)

```powershell
# اتصال به دیتابیس SQLite و حذف migration history
sqlite3 ForexExchange.db "DELETE FROM __EFMigrationsHistory;"
```

یا:

```powershell
sqlite3 ForexExchange.db "DROP TABLE IF EXISTS __EFMigrationsHistory;"
```

### روش 3: حذف کامل دیتابیس و بازسازی

⚠️ **هشدار: تمام داده‌ها حذف می‌شوند**

```bash
# حذف فایل دیتابیس
Remove-Item ForexExchange.db -Force

# یا در Linux/Mac:
# rm ForexExchange.db

# سپس ایجاد migration جدید و اعمال آن
cd ForexExchange
dotnet ef migrations add InitialCreate --context ForexDbContext
dotnet ef database update --context ForexDbContext
```

## شروع از صفر (پس از حذف همه Migrationها)

پس از حذف همه فایل‌های migration و migration history از دیتابیس:

1. **ایجاد migration اولیه جدید:**
   ```bash
   cd ForexExchange
   dotnet ef migrations add InitialCreate --context ForexDbContext
   ```

2. **بررسی migration ایجاد شده:**
   ```bash
   dotnet ef migrations list --context ForexDbContext
   ```

3. **اعمال migration به دیتابیس:**
   ```bash
   dotnet ef database update --context ForexDbContext
   ```

4. **بررسی وضعیت دیتابیس:**
   ```sql
   SELECT * FROM __EFMigrationsHistory;
   ```
