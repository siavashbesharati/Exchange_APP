/**
 * Currency Code + Symbol display helpers.
 * Renders: CODE + logo/symbol  (logo from /Currencies/Logo/{code})
 */
(function (window, document) {
    'use strict';

    function escapeHtml(value) {
        return String(value ?? '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    /**
     * @param {string} currencyCode
     * @param {{ height?: number, className?: string }} [options]
     * @returns {string} HTML for CODE + symbol
     */
    function renderCurrencyCode(currencyCode, options) {
        if (!currencyCode) return '';
        const code = String(currencyCode).trim().toUpperCase();
        const height = (options && options.height) || 16;
        const className = (options && options.className) || 'currency-code-display';
        const logoUrl = '/Currencies/Logo/' + encodeURIComponent(code);

        return (
            '<span class="' + className + '" data-currency-code="' + escapeHtml(code) + '" data-enhanced="1">' +
            '<span class="currency-code-text">' + escapeHtml(code) + '</span> ' +
            '<img class="currency-logo" src="' + logoUrl + '" alt="" height="' + height + '" ' +
            'style="height:' + height + 'px;width:auto;vertical-align:middle;" ' +
            'onerror="this.style.display=\'none\'" loading="lazy" />' +
            '</span>'
        );
    }

    /**
     * Enhance elements marked with data-currency-code (and not yet enhanced).
     * If the element is empty or only contains the code text, replace with CODE + logo.
     * If it already has richer content, append the logo image once.
     */
    function enhanceCurrencyCodes(root) {
        const scope = root && root.querySelectorAll ? root : document;
        const nodes = scope.querySelectorAll
            ? scope.querySelectorAll('[data-currency-code]:not([data-enhanced="1"])')
            : [];

        nodes.forEach(function (el) {
            const code = (el.getAttribute('data-currency-code') || '').trim().toUpperCase();
            if (!code) return;

            const height = parseInt(el.getAttribute('data-currency-height') || '16', 10) || 16;
            const text = (el.textContent || '').trim().toUpperCase();
            const hasLogoImg = !!el.querySelector('img.currency-logo');

            if (hasLogoImg) {
                el.setAttribute('data-enhanced', '1');
                return;
            }

            // Replace bare code text / empty with full display
            if (!text || text === code) {
                el.innerHTML = renderCurrencyCode(code, { height: height });
                // renderCurrencyCode already sets data-enhanced on inner span; mark outer too
                el.setAttribute('data-enhanced', '1');
                // If we nested a span inside, unwrap by replacing outer content only once
                if (el.firstElementChild && el.firstElementChild.classList.contains('currency-code-display')) {
                    el.innerHTML = el.firstElementChild.innerHTML;
                }
                return;
            }

            // Append logo next to existing label content
            const img = document.createElement('img');
            img.className = 'currency-logo';
            img.src = '/Currencies/Logo/' + encodeURIComponent(code);
            img.alt = '';
            img.height = height;
            img.style.height = height + 'px';
            img.style.width = 'auto';
            img.style.verticalAlign = 'middle';
            img.style.marginInlineStart = '4px';
            img.loading = 'lazy';
            img.onerror = function () { this.style.display = 'none'; };
            el.appendChild(document.createTextNode(' '));
            el.appendChild(img);
            el.setAttribute('data-enhanced', '1');
        });
    }

    function startObserver() {
        if (!window.MutationObserver) return;
        const observer = new MutationObserver(function (mutations) {
            for (const m of mutations) {
                if (m.addedNodes && m.addedNodes.length) {
                    enhanceCurrencyCodes(document);
                    break;
                }
            }
        });
        observer.observe(document.body, { childList: true, subtree: true });
    }

    window.renderCurrencyCode = renderCurrencyCode;
    window.enhanceCurrencyCodes = enhanceCurrencyCodes;

    document.addEventListener('DOMContentLoaded', function () {
        enhanceCurrencyCodes(document);
        startObserver();
    });
})(window, document);
