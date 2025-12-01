// --- Order Preview Modal Script ---

// Show preview modal with order and effect details
function showPreviewModal(order, effects) {

    function getProp(obj, name) {
        return obj[name] ?? obj[name.charAt(0).toLowerCase() + name.slice(1)];
    }

    // Get customer name from dropdown
    let customerSelect = document.getElementById('customerSelect');
    let customerName = 'نامشخص';
    if (customerSelect) {
        if (typeof $ !== 'undefined' && $(customerSelect).hasClass('select2-hidden-accessible')) {
            let selected = $(customerSelect).select2('data')[0];
            customerName = selected ? selected.text : 'نامشخص';
        } else {
            let idx = customerSelect.selectedIndex;
            customerName = idx > 0 ? customerSelect.options[idx].text : 'نامشخص';
        }
    }

    // Get selected date
    let dateValue = document.getElementById('createdAtInput')?.value || '';
    let formattedDate = 'نامشخص';
    if (dateValue) {
        const date = new Date(dateValue);
        formattedDate = date.toLocaleDateString('fa-IR', {
            year: 'numeric',
            month: 'long',
            day: 'numeric',
            hour: '2-digit',
            minute: '2-digit'
        });
    }

    // Currency labels - use CurrencyCode for display (from DTO)
    let fromCurrencyCode = getProp(effects, 'FromCurrencyCode') || '';
    let toCurrencyCode = getProp(effects, 'ToCurrencyCode') || '';
    let fromCurrencyName = getProp(effects, 'FromCurrencyName') || fromCurrencyCode;
    let toCurrencyName = getProp(effects, 'ToCurrencyName') || toCurrencyCode;

    // CurrencyCode is kept in DTO for display purposes only
    let fromCurrency = fromCurrencyName && fromCurrencyCode ? `${fromCurrencyName} (${fromCurrencyCode})` : (fromCurrencyCode || 'نامشخص');
    let toCurrency = toCurrencyName && toCurrencyCode ? `${toCurrencyName} (${toCurrencyCode})` : (toCurrencyCode || 'نامشخص');

    let displayFromAmount = getProp(effects, 'OrderFromAmount') || order.fromAmount;
    let displayToAmount = getProp(effects, 'OrderToAmount') || order.toAmount;
    let displayRate = order.rate;

    // Order details
    let orderDetailsHtml = `
        <div class="col-md-6">
            <div class="mb-2"><strong>مشتری:</strong> <span class="text-primary">${customerName}</span></div>
            <div class="mb-2"><strong>نوع معامله:</strong> <span class="badge bg-info">خرید/فروش ارز</span></div>
            <div class="mb-2"><strong>دریافت می کنیم : </strong> <span class="text-success fw-bold" dir="ltr">${formatCurrency(displayFromAmount, fromCurrencyCode)} ${fromCurrencyCode}</span></div>
        </div>
        <div class="col-md-6">
            <div class="mb-2"><strong> پرداخت می کنیم : </strong> <span class="text-primary fw-bold" dir="ltr">${formatCurrency(displayToAmount, toCurrencyCode)} ${toCurrencyCode}</span></div>
            <div class="mb-2"><strong>نرخ تبدیل:</strong> <span class="text-warning fw-bold" dir="ltr">${displayRate}</span></div>
            <div class="mb-2"><strong>تاریخ:</strong> <span>${formattedDate}</span></div>
        </div>
    `;
    document.getElementById('previewOrderDetails').innerHTML = orderDetailsHtml;

    // Effects
    let eff = effects;
    let customerEffectsHtml = '';
    if (getProp(eff, 'OldCustomerBalanceFrom') !== undefined) {
        customerEffectsHtml = `
            <div class="mb-3">
                <h6 class="text-primary"><i class="fas fa-user"></i> تأثیرات موجودی مشتری</h6>
                <div class="table-responsive">
                    <table class="table table-sm table-bordered">
                        <thead class="table-light">
                            <tr>
                                <th class="text-center">ارز</th>
                                <th class="text-center">موجودی قبل</th>
                                <th class="text-center">موجودی بعد</th>
                                <th class="text-center">تغییر</th>
                            </tr>
                        </thead>
                        <tbody>
                            <tr>
                                <td class="text-center fw-bold">${fromCurrencyCode}</td>
                                <td class="text-center" dir="ltr">${formatCurrency(getProp(eff, 'OldCustomerBalanceFrom'), fromCurrencyCode)}</td>
                                <td class="text-center fw-bold text-${getProp(eff, 'NewCustomerBalanceFrom') >= 0 ? 'success' : 'danger'}" dir="ltr">${formatCurrency(getProp(eff, 'NewCustomerBalanceFrom'), fromCurrencyCode)}</td>
                                <td class="text-center text-danger" dir="ltr">-${formatCurrency(displayFromAmount, fromCurrencyCode)}</td>
                            </tr>
                            <tr>
                                <td class="text-center fw-bold">${toCurrencyCode}</td>
                                <td class="text-center" dir="ltr">${formatCurrency(getProp(eff, 'OldCustomerBalanceTo'), toCurrencyCode)}</td>
                                <td class="text-center fw-bold text-success" dir="ltr">${formatCurrency(getProp(eff, 'NewCustomerBalanceTo'), toCurrencyCode)}</td>
                                <td class="text-center text-success" dir="ltr">+${formatCurrency(displayToAmount, toCurrencyCode)}</td>
                            </tr>
                        </tbody>
                    </table>
                </div>
            </div>
        `;
    }

    let poolEffectsHtml = '';
    if (getProp(eff, 'OldPoolBalanceFrom') !== undefined) {
        poolEffectsHtml = `
            <div class="mb-3">
                <h6 class="text-warning"><i class="fas fa-piggy-bank"></i> تأثیرات داشبورد ارز</h6>
                <div class="table-responsive">
                    <table class="table table-sm table-bordered">
                        <thead class="table-light">
                            <tr>
                                <th class="text-center">ارز</th>
                                <th class="text-center">موجودی قبل</th>
                                <th class="text-center">موجودی بعد</th>
                                <th class="text-center">تغییر</th>
                            </tr>
                        </thead>
                        <tbody>
                            <tr>
                                <td class="text-center fw-bold">${fromCurrencyCode}</td>
                                <td class="text-center" dir="ltr">${formatCurrency(getProp(eff, 'OldPoolBalanceFrom'), fromCurrencyCode)}</td>
                                <td class="text-center fw-bold text-success" dir="ltr">${formatCurrency(getProp(eff, 'NewPoolBalanceFrom'), fromCurrencyCode)}</td>
                                <td class="text-center text-success" dir="ltr">+${formatCurrency(displayFromAmount, fromCurrencyCode)}</td>
                            </tr>
                            <tr>
                                <td class="text-center fw-bold">${toCurrencyCode}</td>
                                <td class="text-center" dir="ltr">${formatCurrency(getProp(eff, 'OldPoolBalanceTo'), toCurrencyCode)}</td>
                                <td class="text-center fw-bold text-${getProp(eff, 'NewPoolBalanceTo') >= 0 ? 'success' : 'danger'}" dir="ltr">${formatCurrency(getProp(eff, 'NewPoolBalanceTo'), toCurrencyCode)}</td>
                                <td class="text-center text-danger" dir="ltr">-${formatCurrency(displayToAmount, toCurrencyCode)}</td>
                            </tr>
                        </tbody>
                    </table>
                </div>
            </div>
        `;
    }

    let allEffectsHtml = customerEffectsHtml + poolEffectsHtml;
    if (!allEffectsHtml) {
        allEffectsHtml = '<div class="alert alert-warning">هیچ تأثیری بر ترازها محاسبه نشد.</div>';
    }
    document.getElementById('previewOrderEffects').innerHTML = allEffectsHtml;

    var modal = new bootstrap.Modal(document.getElementById('previewModal'));
    modal.show();
}

// --- Preview button ---
document.getElementById('previewEffectsBtn').addEventListener('click', function(e) {
    e.preventDefault();
    const data = getOrderPreviewData();
    fetch('/Orders/PreviewOrderEffects', {
        method: 'POST',
        headers: { 
            'Content-Type': 'application/json', 
            'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value 
        },
        body: JSON.stringify(data)
    })
    .then(async res => {
        if (!res.ok) {
            let msg = 'خطا در دریافت پیش‌نمایش';
            try { msg += ': ' + await res.text(); } catch {}
            document.getElementById('previewOrderDetails').innerHTML = '';
            document.getElementById('previewOrderEffects').innerHTML = `<div class='alert alert-danger'>${msg}</div>`;
            new bootstrap.Modal(document.getElementById('previewModal')).show();
            return;
        }
        return res.json();
    })
    .then(effects => { if (effects) showPreviewModal(data, effects); })
    .catch(err => {
        document.getElementById('previewOrderDetails').innerHTML = '';
        document.getElementById('previewOrderEffects').innerHTML = `<div class='alert alert-danger'>خطای ارتباط با سرور: ${err}</div>`;
        new bootstrap.Modal(document.getElementById('previewModal')).show();
    });
});

