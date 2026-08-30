/**
 * UNIFIED Currency Formatter for Exchange Application
 * 
 * GLOBAL FORMATTING RULES (NO ROUNDING - ONLY TRUNCATION):
 * - IRR: Drop all decimal places (truncate) - Example: 234000.534 → 234,000
 * - Non-IRR: Drop after 2 decimal places (truncate) - Example: 23.4567 → 23.45
 * - Trailing zeros removed: 23.60 → 23.6, 23.00 → 23
 * 
 * This is the ONLY frontend formatting script that should be used globally.
 * Other formatting files have been deprecated:
 * - currency-amount-formatter.js.DEPRECATED
 * - auto-currency-display-formatter.js.DEPRECATED  
 * - currency-form-helper.js.DEPRECATED
 * 
 * Usage: formatCurrency(amount, currencyCode)
 * Example: formatCurrency(23.60, 'USD') → "23.6"
 * Example: formatCurrency(234000.534, 'IRR') → "234,000"
 */

window.ForexCurrencyFormatter = (function() {
    'use strict';

    /**
     * Main currency formatting function - GLOBAL UNIFIED FORMATTER
     * @param {number} amount - The numeric amount to format
     * @param {string} currencyCode - The currency code (e.g., 'IRR', 'USD', 'EUR')
     * @returns {string} Formatted currency string
     */
    function formatCurrency(amount, currencyCode = '') {
        // Handle edge cases
        if (amount === null || amount === undefined || isNaN(amount)) {
            return '0';
        }

        // Convert to number if string
        const numAmount = parseFloat(amount);
        
        // Check if currency is IRR
        const isIRR = currencyCode && currencyCode.toUpperCase() === 'IRR';
        
        let result;
        if (isIRR) {
            // IRR: truncate all decimal places (no rounding)
            const truncatedValue = Math.trunc(numAmount);
            result = new Intl.NumberFormat('en-US', {
                minimumFractionDigits: 0,
                maximumFractionDigits: 0
            }).format(truncatedValue);
        } else {
            // Non-IRR: TRUNCATE to 2 decimal places (NO rounding: 0.9 → 0.9, 23.4567 → 23.45)
            const truncated = Math.trunc(numAmount * 100) / 100;
            result = new Intl.NumberFormat('en-US', {
                minimumFractionDigits: 2,
                maximumFractionDigits: 2
            }).format(truncated);
            
            // Remove trailing zeros after decimal point: 23.60 → 23.6, 23.00 → 23
            if (result.includes('.')) {
                result = result.replace(/\.?0+$/, '');
            }
        }

        return result;
    }

    /**
     * Format currency for display in HTML with currency code
     * @param {number} amount - The numeric amount
     * @param {string} currencyCode - The currency code
     * @returns {string} Formatted string with currency code
     */
    function formatCurrencyWithCode(amount, currencyCode = '') {
        const formattedAmount = formatCurrency(amount, currencyCode);
        if (!currencyCode) return formattedAmount;
        if (typeof window.renderCurrencyCode === 'function') {
            return `${formattedAmount} ${window.renderCurrencyCode(currencyCode, { height: 14 })}`;
        }
        return `${formattedAmount} <span data-currency-code="${String(currencyCode).replace(/"/g, '')}">${currencyCode}</span>`;
    }

    /**
     * Format currency for input fields (clean numeric values)
     * @param {number} amount - The numeric amount
     * @param name="currencyCode">The currency code
     * @returns {string} Formatted string suitable for input fields
     */
    function formatCurrencyForInput(amount, currencyCode = '') {
        if (amount === null || amount === undefined || isNaN(amount)) {
            return '';
        }

        const numAmount = parseFloat(amount);
        const isIRR = currencyCode && currencyCode.toUpperCase() === 'IRR';
        
                if (isIRR) {
            // IRR: truncate all decimal places
            return Math.trunc(numAmount).toString();
        } else {
            // Non-IRR: TRUNCATE to 2 decimal places (NO rounding)
            const truncated = Math.trunc(numAmount * 100) / 100;
            return truncated.toString();
        }
    }

    /**
     * Parse a formatted currency string back to number
     * @param {string} formattedAmount - The formatted currency string
     * @returns {number} Parsed numeric value
     */
    function parseCurrency(formattedAmount) {
        if (!formattedAmount || typeof formattedAmount !== 'string') {
            return 0;
        }
        
        // Remove commas and spaces, parse as float
        const cleaned = formattedAmount.replace(/[,\s]/g, '');
        const parsed = parseFloat(cleaned);
        return isNaN(parsed) ? 0 : parsed;
    }

    /**
     * Validate if amount is properly formatted for currency type
     * @param {number} amount - The amount to validate
     * @param {string} currencyCode - The currency code
     * @returns {boolean} True if valid
     */
    function isValidCurrencyAmount(amount, currencyCode = '') {
        if (isNaN(amount) || amount === null || amount === undefined) {
            return false;
        }

        const isIRR = currencyCode && currencyCode.toUpperCase() === 'IRR';
        
        if (isIRR) {
            // IRR should be whole numbers (after truncation)
            return Math.trunc(parseFloat(amount)) === parseFloat(amount);
        }
        
        return true; // Non-IRR can have decimals
    }

    // Public API
    return {
        format: formatCurrency,
        formatWithCode: formatCurrencyWithCode,
        formatForInput: formatCurrencyForInput,
        parse: parseCurrency,
        isValid: isValidCurrencyAmount,
        
        // Alias for backward compatibility
        formatNumber: formatCurrency
    };
})();

// Global convenience function
window.formatCurrency = window.ForexCurrencyFormatter.format;
window.formatCurrencyWithCode = window.ForexCurrencyFormatter.formatWithCode;

/**
 * Format an exchange rate value.
 * Preserves ALL stored decimal places — never rounds or truncates.
 * Only adds thousand separators and removes trailing zeros.
 * Use everywhere an Order.Rate or ExchangeRate.Rate is displayed.
 *
 * Examples:
 *   formatRate(55000)        → "55,000"
 *   formatRate(1.2340)       → "1,234"  ← NO, stored as 1.234, → "1.234"
 *   formatRate(1234567.89)   → "1,234,567.89"
 *   formatRate(0.00450)      → "0.0045"
 *
 * @param {number|string} rate - The rate value
 * @returns {string} Formatted rate string with thousand separators
 */
window.formatRate = function(rate) {
    if (rate === null || rate === undefined || rate === '') return '';
    const num = parseFloat(rate);
    if (isNaN(num)) return '';

    // Convert to string without scientific notation, preserving full precision
    // Use toPrecision with enough digits (15 max for JS floats) then strip trailing zeros
    let str = num.toPrecision(15);           // e.g. "55000.000000000"
    // Remove trailing zeros after decimal point
    if (str.includes('.')) {
        str = str.replace(/\.?0+$/, '');     // "55000"
    }

    const parts = str.split('.');
    // Add thousand separators to integer part
    parts[0] = parts[0].replace(/\B(?=(\d{3})+(?!\d))/g, ',');
    return parts.join('.');
};
