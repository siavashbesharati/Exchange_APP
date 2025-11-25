-- ============================================================
-- اسکریپت پاکسازی رکوردهای تکراری دستی
-- Clean Duplicate Manual Records Script
-- ============================================================
-- این اسکریپت رکوردهای تکراری دستی را در جداول زیر حذف می‌کند:
-- 1. CustomerBalanceHistory (Manual transactions)
-- 2. BankAccountBalanceHistory (ManualEdit transactions)
-- 3. CurrencyPoolHistory (ManualEdit transactions)
--
-- معیار تکراری بودن: مقدار تراکنش (TransactionAmount) و تاریخ تراکنش (TransactionDate)
-- ============================================================

BEGIN TRANSACTION;

-- ============================================================
-- 1. حذف رکوردهای تکراری در CustomerBalanceHistory
-- Remove duplicate manual records in CustomerBalanceHistory
-- ============================================================
-- معیار تکراری: CustomerId, CurrencyCode, TransactionAmount, TransactionDate
-- فقط رکوردهای Manual (TransactionType = 3)

-- ابتدا بررسی تعداد رکوردهای تکراری
SELECT 
    'CustomerBalanceHistory - Duplicate Count' AS TableName,
    COUNT(*) AS DuplicateRecords
FROM (
    SELECT 
        CustomerId, 
        CurrencyCode, 
        TransactionAmount, 
        TransactionDate,
        COUNT(*) as cnt
    FROM CustomerBalanceHistory
    WHERE TransactionType = 3  -- Manual
      AND IsDeleted = 0
    GROUP BY CustomerId, CurrencyCode, TransactionAmount, TransactionDate
    HAVING COUNT(*) > 1
);

-- حذف تکراری‌ها (نگه داشتن اولین رکورد با کمترین Id)
UPDATE CustomerBalanceHistory
SET 
    IsDeleted = 1,
    DeletedAt = datetime('now'),
    DeletedBy = 'DuplicateCleanupScript'
WHERE Id IN (
    SELECT h2.Id
    FROM CustomerBalanceHistory h2
    WHERE h2.TransactionType = 3  -- Manual
      AND h2.IsDeleted = 0
      AND EXISTS (
          SELECT 1
          FROM CustomerBalanceHistory h1
          WHERE h1.TransactionType = 3  -- Manual
            AND h1.IsDeleted = 0
            AND h1.CustomerId = h2.CustomerId
            AND h1.CurrencyCode = h2.CurrencyCode
            AND h1.TransactionAmount = h2.TransactionAmount
            AND h1.TransactionDate = h2.TransactionDate
            AND h1.Id < h2.Id  -- Keep the record with smaller Id
      )
);

-- ============================================================
-- 2. حذف رکوردهای تکراری در BankAccountBalanceHistory
-- Remove duplicate manual records in BankAccountBalanceHistory
-- ============================================================
-- معیار تکراری: BankAccountId, TransactionAmount, TransactionDate
-- فقط رکوردهای ManualEdit (TransactionType = 2)

-- ابتدا بررسی تعداد رکوردهای تکراری
SELECT 
    'BankAccountBalanceHistory - Duplicate Count' AS TableName,
    COUNT(*) AS DuplicateRecords
FROM (
    SELECT 
        BankAccountId, 
        TransactionAmount, 
        TransactionDate,
        COUNT(*) as cnt
    FROM BankAccountBalanceHistory
    WHERE TransactionType = 2  -- ManualEdit
      AND IsDeleted = 0
    GROUP BY BankAccountId, TransactionAmount, TransactionDate
    HAVING COUNT(*) > 1
);

-- حذف تکراری‌ها (نگه داشتن اولین رکورد با کمترین Id)
UPDATE BankAccountBalanceHistory
SET 
    IsDeleted = 1,
    DeletedAt = datetime('now'),
    DeletedBy = 'DuplicateCleanupScript'
WHERE Id IN (
    SELECT h2.Id
    FROM BankAccountBalanceHistory h2
    WHERE h2.TransactionType = 2  -- ManualEdit
      AND h2.IsDeleted = 0
      AND EXISTS (
          SELECT 1
          FROM BankAccountBalanceHistory h1
          WHERE h1.TransactionType = 2  -- ManualEdit
            AND h1.IsDeleted = 0
            AND h1.BankAccountId = h2.BankAccountId
            AND h1.TransactionAmount = h2.TransactionAmount
            AND h1.TransactionDate = h2.TransactionDate
            AND h1.Id < h2.Id  -- Keep the record with smaller Id
      )
);

-- ============================================================
-- 3. حذف رکوردهای تکراری در CurrencyPoolHistory
-- Remove duplicate manual records in CurrencyPoolHistory
-- ============================================================
-- معیار تکراری: CurrencyCode, TransactionAmount, TransactionDate
-- فقط رکوردهای ManualEdit (TransactionType = 3)

-- ابتدا بررسی تعداد رکوردهای تکراری
SELECT 
    'CurrencyPoolHistory - Duplicate Count' AS TableName,
    COUNT(*) AS DuplicateRecords
FROM (
    SELECT 
        CurrencyCode, 
        TransactionAmount, 
        TransactionDate,
        COUNT(*) as cnt
    FROM CurrencyPoolHistory
    WHERE TransactionType = 3  -- ManualEdit
      AND IsDeleted = 0
    GROUP BY CurrencyCode, TransactionAmount, TransactionDate
    HAVING COUNT(*) > 1
);

-- حذف تکراری‌ها (نگه داشتن اولین رکورد با کمترین Id)
UPDATE CurrencyPoolHistory
SET 
    IsDeleted = 1,
    DeletedAt = datetime('now'),
    DeletedBy = 'DuplicateCleanupScript'
WHERE Id IN (
    SELECT h2.Id
    FROM CurrencyPoolHistory h2
    WHERE h2.TransactionType = 3  -- ManualEdit
      AND h2.IsDeleted = 0
      AND EXISTS (
          SELECT 1
          FROM CurrencyPoolHistory h1
          WHERE h1.TransactionType = 3  -- ManualEdit
            AND h1.IsDeleted = 0
            AND h1.CurrencyCode = h2.CurrencyCode
            AND h1.TransactionAmount = h2.TransactionAmount
            AND h1.TransactionDate = h2.TransactionDate
            AND h1.Id < h2.Id  -- Keep the record with smaller Id
      )
);

-- ============================================================
-- خلاصه نتایج
-- Summary Results
-- ============================================================
SELECT 
    'CustomerBalanceHistory' AS TableName,
    COUNT(*) AS RemainingManualRecords
FROM CustomerBalanceHistory
WHERE TransactionType = 3 AND IsDeleted = 0

UNION ALL

SELECT 
    'BankAccountBalanceHistory' AS TableName,
    COUNT(*) AS RemainingManualRecords
FROM BankAccountBalanceHistory
WHERE TransactionType = 2 AND IsDeleted = 0

UNION ALL

SELECT 
    'CurrencyPoolHistory' AS TableName,
    COUNT(*) AS RemainingManualRecords
FROM CurrencyPoolHistory
WHERE TransactionType = 3 AND IsDeleted = 0;

-- ============================================================
-- بررسی رکوردهای حذف شده
-- Check deleted records
-- ============================================================
SELECT 
    'CustomerBalanceHistory - Deleted' AS TableName,
    COUNT(*) AS DeletedRecords
FROM CustomerBalanceHistory
WHERE TransactionType = 3 AND IsDeleted = 1

UNION ALL

SELECT 
    'BankAccountBalanceHistory - Deleted' AS TableName,
    COUNT(*) AS DeletedRecords
FROM BankAccountBalanceHistory
WHERE TransactionType = 2 AND IsDeleted = 1

UNION ALL

SELECT 
    'CurrencyPoolHistory - Deleted' AS TableName,
    COUNT(*) AS DeletedRecords
FROM CurrencyPoolHistory
WHERE TransactionType = 3 AND IsDeleted = 1;

-- ============================================================
-- تایید و اعمال تغییرات
-- ============================================================
-- برای اعمال تغییرات، COMMIT را اجرا کنید
-- برای بازگشت تغییرات، ROLLBACK را اجرا کنید

-- COMMIT;
-- ROLLBACK;

