# Financial Service Debugging Log Guide

## Overview
This guide explains the logging system for debugging financial document processing and balance rebuild issues.

## Where to Find Logs

### Error Log File (Errors, Crashes, Critical Only)
- **Location:** `Logs/errors-YYYY-MM-DD.txt`
- **Contains:** ONLY Error, Fatal (crash), and Critical level logs
- **Retention:** 90 days
- **Size Limit:** 10 MB per file
- **Note:** All Information, Warning, Debug, and Trace logs are disabled to reduce log file size

### Database Logs
- **Status:** DISABLED
- **Reason:** Database query logs are disabled to reduce log file size
- **Note:** Only database errors will be logged (through the error log file above)

### How to Access
1. Navigate to the `Logs` folder in your project root
2. Open `errors-YYYY-MM-DD.txt` (replace YYYY-MM-DD with today's date)
3. This file contains only critical errors, crashes, and exceptions
4. All other logs (Information, Warning, Debug) are disabled

## Log Entry Types

### 🔵 Blue Circle - Main Process Flow
- **`🔵 [ProcessAccountingDocumentAsync]`** - Main entry point for processing accounting documents
- **`🔵 [UpdateBalancesForDocumentAsync]`** - Fast balance update process

### 🟡 Yellow Circle - History Processing
- **`🟡 [ProcessCustomerBalanceHistoryForDocument]`** - Customer balance history processing
- **`🟡 [ProcessBankBalanceHistoryForDocument]`** - Bank account balance history processing

### 🟢 Green Circle - Rebuild Operations
- **`🟢 [RebuildCustomerBalanceChain]`** - Rebuilding customer balance chain
- **`🟢 [RebuildBankBalanceChain]`** - Rebuilding bank account balance chain

### 🔴 Red Circle - Errors
- **`🔴 [Error]`** - Critical errors that need attention

---

## Expected Log Flow for Document Verification

When you verify an accounting document, you should see logs in this order:

### Step 1: Document Processing Starts
```
🔵 [ProcessAccountingDocumentAsync] START - Document ID: 2387, IsVerified: True
🔵 [ProcessAccountingDocumentAsync] Document reloaded: Id=2387, IsVerified=True, IsDeleted=False, IsFrozen=False
🔵 [ProcessAccountingDocumentAsync] PayerType=Customer, PayerCustomerId=2, PayerBankAccountId=null
🔵 [ProcessAccountingDocumentAsync] ReceiverType=System, ReceiverCustomerId=null, ReceiverBankAccountId=1
🔵 [ProcessAccountingDocumentAsync] CurrencyId=5, Amount=500
```

### Step 2: Balance Update Starts
```
🔵 [UpdateBalancesForDocumentAsync] ═══ START ═══ DocumentId=2387, IsVerified=True, IsDeleted=False
🔵 [UpdateBalancesForDocumentAsync] PayerType=Customer, PayerCustomerId=2, PayerBankAccountId=null
🔵 [UpdateBalancesForDocumentAsync] ReceiverType=System, ReceiverCustomerId=null, ReceiverBankAccountId=1
🔵 [UpdateBalancesForDocumentAsync] CurrencyId=5, Amount=500
```

### Step 3A: Customer History Processing (if PayerType=Customer)
```
[UpdateBalancesForDocument] Processing payer customer 2 for document 2387 (+500)
🟡 [ProcessCustomerBalanceHistoryForDocument] ═══ START ═══ DocumentId=2387, CustomerId=2, CurrencyId=5, Amount=500, Role=پرداخت کننده, IsVerified=True
🟡 [ProcessCustomerBalanceHistoryForDocument] History record added and saved. Calling RebuildCustomerBalanceChain(CustomerId=2, CurrencyId=5, CurrencyCode=OMR, EnsureDocumentId=2387)...
🟡 [ProcessCustomerBalanceHistoryForDocument] Calling RebuildCustomerBalanceChain(CustomerId=2, CurrencyId=5, CurrencyCode=OMR, EnsureDocumentId=2387)
```

### Step 3B: Bank History Processing (if ReceiverType=System)
```
[UpdateBalancesForDocument] Processing receiver bank account 1 for document 2387 (-500)
🟡 [ProcessBankBalanceHistoryForDocument] ═══ START ═══ DocumentId=2387, BankAccountId=1, Amount=-500, Role=دریافت کننده, IsVerified=True
🟡 [ProcessBankBalanceHistoryForDocument] History record added and saved. Calling RebuildBankBalanceChain(BankAccountId=1, EnsureDocumentId=2387)...
🟡 [ProcessBankBalanceHistoryForDocument] Calling RebuildBankBalanceChain(BankAccountId=1, EnsureDocumentId=2387)
```

### Step 4A: Customer Balance Rebuild
```
🟢 [RebuildCustomerBalanceChain] ═══ START ═══ CustomerId=2, CurrencyId=5, CurrencyCode=OMR, EnsureDocumentId=2387
🟢 [RebuildCustomerBalanceChain] Query result: Found X verified documents for customer 2, currency 5
🟢 [RebuildCustomerBalanceChain] Document IDs found: 123, 456, 2387
🟢 [RebuildCustomerBalanceChain] Looking for ensureDocumentId: 2387
🟢 [RebuildCustomerBalanceChain] Document 2387 found in initial query: True for customer 2
🟢 [RebuildCustomerBalanceChain] Explicitly querying document 2387 from DB...
🟢 [RebuildCustomerBalanceChain] Explicit query result for 2387: Found=True
🟢 [RebuildCustomerBalanceChain] Document details: Id=2387, IsVerified=True, IsDeleted=False
🟢 [RebuildCustomerBalanceChain] PayerType=Customer, PayerCustomerId=2
🟢 [RebuildCustomerBalanceChain] ReceiverType=System, ReceiverCustomerId=null
🟢 [RebuildCustomerBalanceChain] CurrencyId=5, Amount=500
[RebuildCustomerBalanceChain] Customer 2 involvement: IsPayer=True, IsReceiver=False
[RebuildCustomerBalanceChain] Processing 15 documents for customer 2, currency 5
[RebuildCustomerBalanceChain] ✅ Added PAYER transaction for document 2387: customer 2 gets +500 OMR
[RebuildCustomerBalanceChain] ✅ Document 2387 successfully included in transaction items for customer 2
[RebuildCustomerBalanceChain] Total transaction items: 30 (Orders: 10, Documents: 15, Manual: 5)
[RebuildCustomerBalanceChain] ✅ Updated balance for customer 2, currency 5: 1000 → 1500 (change: 500)
🟡 [ProcessCustomerBalanceHistoryForDocument] ✅ RebuildCustomerBalanceChain COMPLETED for CustomerId=2, CurrencyId=5
```

### Step 4B: Bank Balance Rebuild
```
🟢 [RebuildBankBalanceChain] ═══ START ═══ BankAccountId=1, EnsureDocumentId=2387
[RebuildBankBalanceChain] Document 2387 found in initial query: True for bank account 1
[RebuildBankBalanceChain] Explicitly loaded document 2387: Found=True, IsVerified=True
✅ Explicitly included document 2387 in rebuild for bank account 1
🟡 [ProcessBankBalanceHistoryForDocument] ✅ RebuildBankBalanceChain COMPLETED for BankAccountId=1
```

### Step 5: Completion
```
Fixed fast balance update completed for Document 2387
🔵 [ProcessAccountingDocumentAsync] ✅ COMPLETED - Document 2387 processing finished successfully
```

---

## Common Issues and What to Look For

### Issue 1: Document Not Found in Rebuild Query

**Symptoms:**
```
🟢 [RebuildCustomerBalanceChain] Document 2387 found in initial query: False
🟡 [RebuildCustomerBalanceChain] Document 2387 NOT FOUND in explicit query!
🟡 [RebuildCustomerBalanceChain] Document 2387 EXISTS but: IsVerified=False, IsDeleted=False, CurrencyId=5
```

**Meaning:** The document exists but `IsVerified=False`. The document was not saved with `IsVerified=True` before the rebuild was called.

**Solution:** Check the controller - ensure `SaveChangesAsync()` is called BEFORE `ProcessAccountingDocumentAsync()`.

---

### Issue 2: Document Not Included in Transaction Items

**Symptoms:**
```
[RebuildCustomerBalanceChain] ⚠️ Document 2387 NOT included in transaction items for customer 2 - this may cause missing history!
```

**Meaning:** The document was found in the query but not added to the transaction items list. This could mean:
- The customer ID doesn't match (document is for a different customer)
- The currency ID doesn't match
- The document is not verified

**Solution:** Check the document's `PayerCustomerId`, `ReceiverCustomerId`, and `CurrencyId` values.

---

### Issue 3: Customer History Not Processed

**Symptoms:**
- You see `🔵 [UpdateBalancesForDocumentAsync]` logs
- You see `🟡 [ProcessBankBalanceHistoryForDocument]` logs
- You DON'T see `🟡 [ProcessCustomerBalanceHistoryForDocument]` logs

**Meaning:** The document's `PayerType` or `ReceiverType` is not `Customer`, OR the `PayerCustomerId`/`ReceiverCustomerId` is null.

**Solution:** Check the `🔵 [UpdateBalancesForDocumentAsync]` logs to see:
- `PayerType=Customer` but `PayerCustomerId=null` → Document is missing customer ID
- `PayerType=System` → This is correct, customer history won't be processed

---

### Issue 4: Rebuild Not Called

**Symptoms:**
- You see `🟡 [ProcessCustomerBalanceHistoryForDocument] ═══ START ═══`
- You DON'T see `🟢 [RebuildCustomerBalanceChain] ═══ START ═══`

**Meaning:** The rebuild method is not being called after adding the history record.

**Solution:** Check if there's an exception between adding the history record and calling the rebuild.

---

### Issue 5: Document State Mismatch

**Symptoms:**
```
🔵 [ProcessAccountingDocumentAsync] Document reloaded: Id=2387, IsVerified=True
🔵 [UpdateBalancesForDocumentAsync] ═══ START ═══ DocumentId=2387, IsVerified=False
```

**Meaning:** The document was reloaded with `IsVerified=True`, but when `UpdateBalancesForDocumentAsync` is called, it shows `IsVerified=False`. This suggests the document object is stale.

**Solution:** The document should be reloaded with `AsNoTracking()` to ensure fresh data.

---

## Document Types and Expected Behavior

### Type 1: Customer to System (Customer pays, System receives)
- **PayerType:** Customer
- **ReceiverType:** System
- **Expected Logs:**
  - `🟡 [ProcessCustomerBalanceHistoryForDocument]` for payer customer
  - `🟡 [ProcessBankBalanceHistoryForDocument]` for receiver bank account
  - `🟢 [RebuildCustomerBalanceChain]` for payer customer
  - `🟢 [RebuildBankBalanceChain]` for receiver bank account

### Type 2: System to Customer (System pays, Customer receives)
- **PayerType:** System
- **ReceiverType:** Customer
- **Expected Logs:**
  - `🟡 [ProcessBankBalanceHistoryForDocument]` for payer bank account
  - `🟡 [ProcessCustomerBalanceHistoryForDocument]` for receiver customer
  - `🟢 [RebuildBankBalanceChain]` for payer bank account
  - `🟢 [RebuildCustomerBalanceChain]` for receiver customer

### Type 3: Customer to Customer (Customer pays, Customer receives)
- **PayerType:** Customer
- **ReceiverType:** Customer
- **Expected Logs:**
  - `🟡 [ProcessCustomerBalanceHistoryForDocument]` for payer customer (amount: +)
  - `🟡 [ProcessCustomerBalanceHistoryForDocument]` for receiver customer (amount: -)
  - `🟢 [RebuildCustomerBalanceChain]` for payer customer
  - `🟢 [RebuildCustomerBalanceChain]` for receiver customer

### Type 4: System to System (Bank to Bank)
- **PayerType:** System
- **ReceiverType:** System
- **Expected Logs:**
  - `🟡 [ProcessBankBalanceHistoryForDocument]` for payer bank account
  - `🟡 [ProcessBankBalanceHistoryForDocument]` for receiver bank account
  - `🟢 [RebuildBankBalanceChain]` for payer bank account
  - `🟢 [RebuildBankBalanceChain]` for receiver bank account

---

## How to Use This Guide

1. **Verify a document** in your application
2. **Copy all logs** that contain:
   - `🔵` (blue circle)
   - `🟡` (yellow circle)
   - `🟢` (green circle)
   - `🔴` (red circle)
3. **Compare your logs** with the "Expected Log Flow" section above
4. **Identify missing logs** - if a log is missing, check the corresponding "Common Issues" section
5. **Share the logs** with the issue description

---

## Quick Checklist

When debugging, check:

- [ ] `🔵 [ProcessAccountingDocumentAsync]` shows `IsVerified=True`
- [ ] `🔵 [UpdateBalancesForDocumentAsync]` shows correct `PayerType` and `ReceiverType`
- [ ] If `PayerType=Customer`, you see `🟡 [ProcessCustomerBalanceHistoryForDocument]`
- [ ] If `ReceiverType=Customer`, you see `🟡 [ProcessCustomerBalanceHistoryForDocument]`
- [ ] You see `🟢 [RebuildCustomerBalanceChain]` for each customer involved
- [ ] You see `🟢 [RebuildBankBalanceChain]` for each bank account involved
- [ ] `🟢 [RebuildCustomerBalanceChain]` shows `Document X found in initial query: True`
- [ ] `🟢 [RebuildCustomerBalanceChain]` shows `✅ Document X successfully included in transaction items`

---

## Example: Complete Successful Flow

```
[23:45:20 INF] 🔵 [ProcessAccountingDocumentAsync] START - Document ID: 2387, IsVerified: True
[23:45:20 INF] 🔵 [ProcessAccountingDocumentAsync] Document reloaded: Id=2387, IsVerified=True, IsDeleted=False
[23:45:20 INF] 🔵 [ProcessAccountingDocumentAsync] PayerType=Customer, PayerCustomerId=2
[23:45:20 INF] 🔵 [ProcessAccountingDocumentAsync] ReceiverType=System, ReceiverBankAccountId=1
[23:45:20 INF] 🔵 [UpdateBalancesForDocumentAsync] ═══ START ═══ DocumentId=2387, IsVerified=True
[23:45:20 INF] [UpdateBalancesForDocument] Processing payer customer 2 for document 2387 (+500)
[23:45:20 INF] 🟡 [ProcessCustomerBalanceHistoryForDocument] ═══ START ═══ DocumentId=2387, CustomerId=2, CurrencyId=5, Amount=500
[23:45:20 INF] 🟡 [ProcessCustomerBalanceHistoryForDocument] Calling RebuildCustomerBalanceChain(CustomerId=2, CurrencyId=5, EnsureDocumentId=2387)
[23:45:20 INF] 🟢 [RebuildCustomerBalanceChain] ═══ START ═══ CustomerId=2, CurrencyId=5, EnsureDocumentId=2387
[23:45:20 INF] 🟢 [RebuildCustomerBalanceChain] Query result: Found 15 verified documents for customer 2, currency 5
[23:45:20 INF] 🟢 [RebuildCustomerBalanceChain] Document 2387 found in initial query: True
[23:45:20 INF] [RebuildCustomerBalanceChain] ✅ Added PAYER transaction for document 2387: customer 2 gets +500 OMR
[23:45:20 INF] [RebuildCustomerBalanceChain] ✅ Document 2387 successfully included in transaction items for customer 2
[23:45:20 INF] [RebuildCustomerBalanceChain] ✅ Updated balance for customer 2, currency 5: 1000 → 1500
[23:45:20 INF] 🟡 [ProcessCustomerBalanceHistoryForDocument] ✅ RebuildCustomerBalanceChain COMPLETED
[23:45:20 INF] [UpdateBalancesForDocument] Processing receiver bank account 1 for document 2387 (-500)
[23:45:20 INF] 🟡 [ProcessBankBalanceHistoryForDocument] ═══ START ═══ DocumentId=2387, BankAccountId=1, Amount=-500
[23:45:20 INF] 🟡 [ProcessBankBalanceHistoryForDocument] Calling RebuildBankBalanceChain(BankAccountId=1, EnsureDocumentId=2387)
[23:45:20 INF] 🟢 [RebuildBankBalanceChain] ═══ START ═══ BankAccountId=1, EnsureDocumentId=2387
[23:45:20 INF] 🟡 [ProcessBankBalanceHistoryForDocument] ✅ RebuildBankBalanceChain COMPLETED
[23:45:20 INF] 🔵 [ProcessAccountingDocumentAsync] ✅ COMPLETED - Document 2387 processing finished successfully
```

---

## Notes

- All timestamps are in your local timezone
- Log levels: `INF` = Information, `WRN` = Warning, `ERR` = Error
- If you see warnings (`🟡` or `WRN`), investigate them - they often indicate the root cause
- If you see errors (`🔴` or `ERR`), the process likely failed and needs fixing

