-- ============================================================
-- اسکریپت بررسی رکوردهای تکراری (بدون حذف)
-- Check Duplicate Records Script (No Deletion)
-- ============================================================
-- این اسکریپت فقط رکوردهای تکراری را نشان می‌دهد بدون حذف
-- ============================================================

-- ============================================================
-- 1. بررسی رکوردهای تکراری در CustomerBalanceHistory
-- ============================================================
SELECT 
    'CustomerBalanceHistory - Duplicates' AS TableName,
    CustomerId,
    CurrencyCode,
    TransactionAmount,
    TransactionDate,
    COUNT(*) AS DuplicateCount,
    GROUP_CONCAT(Id) AS RecordIds,
    MIN(Id) AS KeepId,
    GROUP_CONCAT(CASE WHEN Id != MIN(Id) THEN Id END) AS DeleteIds
FROM CustomerBalanceHistory
WHERE TransactionType = 3  -- Manual
  AND IsDeleted = 0
GROUP BY CustomerId, CurrencyCode, TransactionAmount, TransactionDate
HAVING COUNT(*) > 1
ORDER BY CustomerId, CurrencyCode, TransactionDate;

-- ============================================================
-- 2. بررسی رکوردهای تکراری در BankAccountBalanceHistory
-- ============================================================
SELECT 
    'BankAccountBalanceHistory - Duplicates' AS TableName,
    BankAccountId,
    TransactionAmount,
    TransactionDate,
    COUNT(*) AS DuplicateCount,
    GROUP_CONCAT(Id) AS RecordIds,
    MIN(Id) AS KeepId,
    GROUP_CONCAT(CASE WHEN Id != MIN(Id) THEN Id END) AS DeleteIds
FROM BankAccountBalanceHistory
WHERE TransactionType = 2  -- ManualEdit
  AND IsDeleted = 0
GROUP BY BankAccountId, TransactionAmount, TransactionDate
HAVING COUNT(*) > 1
ORDER BY BankAccountId, TransactionDate;

-- ============================================================
-- 3. بررسی رکوردهای تکراری در CurrencyPoolHistory
-- ============================================================
SELECT 
    'CurrencyPoolHistory - Duplicates' AS TableName,
    CurrencyCode,
    TransactionAmount,
    TransactionDate,
    COUNT(*) AS DuplicateCount,
    GROUP_CONCAT(Id) AS RecordIds,
    MIN(Id) AS KeepId,
    GROUP_CONCAT(CASE WHEN Id != MIN(Id) THEN Id END) AS DeleteIds
FROM CurrencyPoolHistory
WHERE TransactionType = 3  -- ManualEdit
  AND IsDeleted = 0
GROUP BY CurrencyCode, TransactionAmount, TransactionDate
HAVING COUNT(*) > 1
ORDER BY CurrencyCode, TransactionDate;

-- ============================================================
-- خلاصه تعداد تکراری‌ها
-- ============================================================
SELECT 
    'CustomerBalanceHistory' AS TableName,
    COUNT(*) AS TotalDuplicateGroups,
    SUM(cnt - 1) AS TotalRecordsToDelete
FROM (
    SELECT 
        CustomerId, 
        CurrencyCode, 
        TransactionAmount, 
        TransactionDate,
        COUNT(*) as cnt
    FROM CustomerBalanceHistory
    WHERE TransactionType = 3 AND IsDeleted = 0
    GROUP BY CustomerId, CurrencyCode, TransactionAmount, TransactionDate
    HAVING COUNT(*) > 1
)

UNION ALL

SELECT 
    'BankAccountBalanceHistory' AS TableName,
    COUNT(*) AS TotalDuplicateGroups,
    SUM(cnt - 1) AS TotalRecordsToDelete
FROM (
    SELECT 
        BankAccountId, 
        TransactionAmount, 
        TransactionDate,
        COUNT(*) as cnt
    FROM BankAccountBalanceHistory
    WHERE TransactionType = 2 AND IsDeleted = 0
    GROUP BY BankAccountId, TransactionAmount, TransactionDate
    HAVING COUNT(*) > 1
)

UNION ALL

SELECT 
    'CurrencyPoolHistory' AS TableName,
    COUNT(*) AS TotalDuplicateGroups,
    SUM(cnt - 1) AS TotalRecordsToDelete
FROM (
    SELECT 
        CurrencyCode, 
        TransactionAmount, 
        TransactionDate,
        COUNT(*) as cnt
    FROM CurrencyPoolHistory
    WHERE TransactionType = 3 AND IsDeleted = 0
    GROUP BY CurrencyCode, TransactionAmount, TransactionDate
    HAVING COUNT(*) > 1
);

