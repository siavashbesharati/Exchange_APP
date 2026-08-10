/**
 * Lightweight preview overlay helpers (no Bootstrap Modal).
 * Reliable vertical scroll + fixed footer on mobile.
 */
(function (window) {
    'use strict';

    function openFxPreview(id) {
        const el = document.getElementById(id);
        if (!el) return;
        el.classList.add('is-open');
        el.removeAttribute('hidden');
        el.setAttribute('aria-hidden', 'false');
        document.body.classList.add('fx-preview-open');
    }

    function closeFxPreview(id) {
        const el = document.getElementById(id);
        if (!el) return;
        el.classList.remove('is-open');
        el.setAttribute('hidden', '');
        el.setAttribute('aria-hidden', 'true');
        // Only unlock body if no other preview is open
        if (!document.querySelector('.fx-preview-overlay.is-open')) {
            document.body.classList.remove('fx-preview-open');
        }
    }

    function wireFxPreview(id) {
        const el = document.getElementById(id);
        if (!el || el.dataset.fxWired === '1') return;
        el.dataset.fxWired = '1';

        el.querySelectorAll('[data-fx-close]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                closeFxPreview(id);
            });
        });

        el.addEventListener('click', function (e) {
            if (e.target === el) closeFxPreview(id);
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        document.querySelectorAll('.fx-preview-overlay').forEach(function (el) {
            wireFxPreview(el.id);
        });
    });

    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Escape') return;
        const open = document.querySelector('.fx-preview-overlay.is-open');
        if (open) closeFxPreview(open.id);
    });

    window.openFxPreview = openFxPreview;
    window.closeFxPreview = closeFxPreview;
    window.wireFxPreview = wireFxPreview;
})(window);
