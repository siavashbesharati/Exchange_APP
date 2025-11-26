# گزارش بررسی منطق Increase/Decrease در Rebuild و متدهای جدید

## خلاصه
این گزارش منطق increase/decrease را در `RebuildAllFinancialBalancesAsync` و متدهای جدید (`UpdateBalancesForOrderAsync` و `UpdateBalancesForDocumentAsync`) بررسی می‌کند.

---

## 1. ORDER PROCESSING

### 1.1 Rebuild کامل (`RebuildAllFinancialBalancesAsync`)

#### Pool History (خطوط 1858-1862):
- **FromCurrency**: 
  - `TransactionAmount = o.FromAmount` (positive)
  - `BalanceAfter = runningBalance + transaction.Amount`
  - **نتیجه**: INCREASE ✓ (Institution receives)
  
- **ToCurrency**: 
  - `TransactionAmount = -o.ToAmount` (negative)
  - `BalanceAfter = runningBalance + transaction.Amount`
  - **نتیجه**: DECREASE ✓ (Institution pays)

#### Customer History (خطوط 2277-2281):
- **FromCurrency**: 
  - `TransactionAmount = -o.FromAmount` (negative)
  - `BalanceAfter = runningBalance + transaction.Amount`
  - **نتیجه**: DECREASE ✓ (Customer pays)
  
- **ToCurrency**: 
  - `TransactionAmount = o.ToAmount` (positive)
  - `BalanceAfter = runningBalance + transaction.Amount`
  - **نتیجه**: INCREASE ✓ (Customer receives)

---

### 1.2 متد جدید (`UpdateBalancesForOrderAsync`)

#### Pool History:
- **FromCurrency** (خطوط 940-943):
  - `TransactionAmount = order.FromAmount` (positive)
  - `BalanceAfter = balanceBeforeFrom + order.FromAmount`
  - **نتیجه**: INCREASE ✓ (مطابق با rebuild)
  
- **ToCurrency** (خطوط 1033, 1054):
  - `TransactionAmount = -order.ToAmount` (negative)
  - `BalanceAfter = balanceBeforeTo - order.ToAmount`
  - **نتیجه**: DECREASE ✓ (مطابق با rebuild)
  - **نکته**: در chain rebuild (خط 1040)، برای records موجود، اگر `PoolTransactionType == "Sell"` و `TransactionAmount > 0` باشد، آن را منفی می‌کنیم.

#### Customer History:
- **FromCurrency** (خطوط 740, 761):
  - `TransactionAmount = -order.FromAmount` (negative)
  - `BalanceAfter = customerBalanceBeforeFrom - order.FromAmount`
  - **نتیجه**: DECREASE ✓ (مطابق با rebuild)
  
- **ToCurrency** (خطوط 836, 857):
  - `TransactionAmount = order.ToAmount` (positive)
  - `BalanceAfter = customerBalanceBeforeTo + order.ToAmount`
  - **نتیجه**: INCREASE ✓ (مطابق با rebuild)

**✅ نتیجه**: منطق Order Processing در متد جدید کاملاً مطابق با rebuild کامل است.

---

## 2. ACCOUNTING DOCUMENT PROCESSING

### 2.1 Rebuild کامل (`RebuildAllFinancialBalancesAsync`)

#### Bank Account History (خطوط 2069-2081):
- **Payer Bank** (خط 2072, 2079):
  - `TransactionAmount = d.Amount` (positive)
  - `BalanceAfter = runningBalance + transaction.Amount`
  - **نتیجه**: INCREASE ✓ (Bank pays out)
  
- **Receiver Bank** (خط 2073, 2081):
  - `TransactionAmount = -(d.Amount)` (negative)
  - `BalanceAfter = runningBalance + transaction.Amount`
  - **نتیجه**: DECREASE ✓ (Bank receives)

#### Customer History (خطوط 2254-2266):
- **Payer Customer** (خط 2257, 2264):
  - `TransactionAmount = d.Amount` (positive)
  - `BalanceAfter = runningBalance + transaction.Amount`
  - **نتیجه**: INCREASE ✓ (Customer receives - deposit)
  
- **Receiver Customer** (خط 2258, 2266):
  - `TransactionAmount = -d.Amount` (negative)
  - `BalanceAfter = runningBalance + transaction.Amount`
  - **نتیجه**: DECREASE ✓ (Customer pays - withdrawal)

---

### 2.2 متد جدید (`UpdateBalancesForDocumentAsync`)

#### Bank Account History:
- **Payer Bank** (خطوط 1407, 1426):
  - `TransactionAmount = document.Amount` (positive)
  - `BalanceAfter = payerBankBalanceBefore + document.Amount`
  - **نتیجه**: INCREASE ✓ (مطابق با rebuild)
  
- **Receiver Bank** (خطوط 1502, 1521):
  - `TransactionAmount = -document.Amount` (negative)
  - `BalanceAfter = receiverBankBalanceBefore - document.Amount`
  - **نتیجه**: DECREASE ✓ (مطابق با rebuild)

#### Customer History:
- **Payer Customer** (خطوط 1203, 1223):
  - `TransactionAmount = document.Amount` (positive)
  - `BalanceAfter = payerBalanceBefore + document.Amount`
  - **نتیجه**: INCREASE ✓ (مطابق با rebuild)
  
- **Receiver Customer** (خطوط 1308, 1328):
  - `TransactionAmount = -document.Amount` (negative)
  - `BalanceAfter = receiverBalanceBefore - document.Amount`
  - **نتیجه**: DECREASE ✓ (مطابق با rebuild)

**✅ نتیجه**: منطق Accounting Document Processing در متد جدید کاملاً مطابق با rebuild کامل است.

---

## 3. بررسی شرایط مختلف

### 3.1 Bank to Bank Transfer
- **Rebuild**: 
  - Payer Bank: `+d.Amount` → INCREASE ✓
  - Receiver Bank: `-d.Amount` → DECREASE ✓
- **متد جدید**: 
  - Payer Bank: `+document.Amount` → INCREASE ✓
  - Receiver Bank: `-document.Amount` → DECREASE ✓
- **✅ مطابق است**

### 3.2 Bank to Customer (Deposit)
- **Rebuild**: 
  - Payer Bank: `+d.Amount` → INCREASE ✓
  - Receiver Customer: `-d.Amount` → DECREASE ✓
- **متد جدید**: 
  - Payer Bank: `+document.Amount` → INCREASE ✓
  - Receiver Customer: `-document.Amount` → DECREASE ✓
- **✅ مطابق است**

### 3.3 Customer to Bank (Withdrawal)
- **Rebuild**: 
  - Payer Customer: `+d.Amount` → INCREASE ✓
  - Receiver Bank: `-d.Amount` → DECREASE ✓
- **متد جدید**: 
  - Payer Customer: `+document.Amount` → INCREASE ✓
  - Receiver Bank: `-document.Amount` → DECREASE ✓
- **✅ مطابق است**

### 3.4 Customer to Customer Transfer
- **Rebuild**: 
  - Payer Customer: `+d.Amount` → INCREASE ✓
  - Receiver Customer: `-d.Amount` → DECREASE ✓
- **متد جدید**: 
  - Payer Customer: `+document.Amount` → INCREASE ✓
  - Receiver Customer: `-document.Amount` → DECREASE ✓
- **✅ مطابق است**

---

## 4. مشکلات شناسایی شده

### ✅ مشکل 1: حل شده - عدم وجود Duplicate Check در `UpdateBalancesForDocumentAsync`
- **مکان**: خطوط 1215-1232 (Payer Customer) و 1320-1337 (Receiver Customer) و 1419-1434 (Payer Bank) و 1515-1529 (Receiver Bank)
- **مشکل**: در `UpdateBalancesForDocumentAsync`، duplicate check وجود نداشت. اگر document دوباره process شود، duplicate records ایجاد می‌شد.
- **راه حل**: ✅ اضافه شده است (مشابه `UpdateBalancesForOrderAsync`).
  - Payer Customer: بررسی می‌کند که آیا record با `ReferenceId == document.Id` و `TransactionType == AccountingDocument` وجود دارد.
  - Receiver Customer: بررسی می‌کند که آیا record با `ReferenceId == document.Id` و `TransactionType == AccountingDocument` وجود دارد.
  - Payer Bank: بررسی می‌کند که آیا record با `ReferenceId == document.Id` و `TransactionType == Document` وجود دارد.
  - Receiver Bank: بررسی می‌کند که آیا record با `ReferenceId == document.Id` و `TransactionType == Document` وجود دارد.

### ✅ مشکل 2: حل شده - ToCurrency در Pool History
- **مکان**: خط 1036-1042
- **مشکل**: در chain rebuild برای ToCurrency، اگر `TransactionAmount` مثبت باشد، باید منفی شود.
- **راه حل**: ✅ اضافه شده است (خط 1036-1042).

---

## 5. خلاصه نهایی

### ✅ منطق Increase/Decrease:
- **Order Processing**: کاملاً مطابق ✓
- **Accounting Document Processing**: کاملاً مطابق ✓
- **Bank to Bank**: کاملاً مطابق ✓
- **Bank to Customer**: کاملاً مطابق ✓
- **Customer to Bank**: کاملاً مطابق ✓
- **Customer to Customer**: کاملاً مطابق ✓

### ✅ مشکلات باقی‌مانده:
هیچ مشکلی باقی نمانده است. همه مشکلات حل شده‌اند.

---

## 6. توصیه‌ها

1. ✅ **Duplicate Check اضافه شده**: در `UpdateBalancesForDocumentAsync`، duplicate check برای همه موارد (Payer Customer, Receiver Customer, Payer Bank, Receiver Bank) اضافه شده است.
2. **تست کامل**: باید تست کامل انجام شود تا مطمئن شویم که:
   - Duplicate records ایجاد نمی‌شوند
   - Chain rebuild درست کار می‌کند
   - Balance calculations درست هستند

