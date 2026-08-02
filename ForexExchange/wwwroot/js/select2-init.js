/**
 * Select2 Global Initialization Script
 * This script automatically converts all select elements to searchable Select2 dropdowns
 * with proper RTL support for Persian/Farsi interface
 */

$(document).ready(function() {
    
    // Check if Select2 is disabled for this page
    if (window.disableSelect2 === true) {
        return;
    }
    
    // Initialize Select2 on all select elements
    initializeSelect2();
    
    // Re-initialize Select2 when new content is loaded dynamically
    $(document).on('DOMNodeInserted', function(e) {
        // Skip if Select2 is disabled
        if (window.disableSelect2 === true) {
            return;
        }
        
        if ($(e.target).find('select').length || $(e.target).is('select')) {
            setTimeout(function() {
                initializeSelect2();
            }, 100);
        }
    });
});

/**
 * Initialize Select2 on all select elements that aren't already initialized
 */
function initializeSelect2() {
    // Check if Select2 is disabled for this page
    if (window.disableSelect2 === true) {
        return;
    }
    
    
    $('select:not(.select2-hidden-accessible):not(.no-select2)').each(function() {
        var $select = $(this);
        
        // Skip if already initialized
        if ($select.hasClass('select2-hidden-accessible')) {
            return;
        }
        
        // Skip if marked to not use Select2
        if ($select.hasClass('no-select2')) {
            return;
        }
        
        
        // Default Select2 configuration
        var config = {
            language: {
                errorLoading: function () {
                    return 'امکان بارگذاری نتایج وجود ندارد.';
                },
                inputTooLong: function (args) {
                    var overChars = args.input.length - args.maximum;
                    return 'لطفاً ' + overChars + ' کاراکتر را حذف نمایید';
                },
                inputTooShort: function (args) {
                    var remainingChars = args.minimum - args.input.length;
                    return 'لطفاً تعداد ' + remainingChars + ' کاراکتر یا بیشتر وارد نمایید';
                },
                loadingMore: function () {
                    return 'در حال بارگذاری نتایج بیشتر...';
                },
                maximumSelected: function (args) {
                    return 'شما تنها می‌توانید ' + args.maximum + ' آیتم را انتخاب نمایید';
                },
                noResults: function () {
                    return 'هیچ نتیجه‌ای یافت نشد';
                },
                searching: function () {
                    return 'در حال جستجو...';
                },
                removeAllItems: function () {
                    return 'همه موارد را حذف کنید';
                }
            },
            dir: 'rtl',     // Right-to-left text direction
            width: '100%',  // Full width to match Bootstrap form controls
            allowClear: !$select.prop('required'), // Allow clearing if not required
            placeholder: getPlaceholder($select),
            escapeMarkup: function(markup) { 
                return markup; // Allow HTML in options
            },
            templateResult: formatResult,
            templateSelection: formatSelection,
            dropdownAutoWidth: true,
            minimumResultsForSearch: 5 // Show search box only if more than 5 options
        };
        
        // Handle multiple selects
        if ($select.prop('multiple')) {
            config.closeOnSelect = false;
        }

        // Keep dropdown above Bootstrap modal overlays
        var $modal = $select.closest('.modal');
        if ($modal.length) {
            config.dropdownParent = $modal;
        }
        
        // Initialize Select2
        try {
            $select.select2(config);
            
            // For AccountingDocuments pages, trigger original change events after Select2 initialization
            if (window.location.pathname.includes('/AccountingDocuments/')) {
                $select.on('select2:select select2:clear', function() {
                    // Trigger the original change event for compatibility with existing JS
                    var event = new Event('change', { bubbles: true });
                    this.dispatchEvent(event);
                });
            }
            
        } catch (error) {
        }
    });
}

/**
 * Get appropriate placeholder text for select element
 */
function getPlaceholder($select) {
    // Check for existing placeholder
    var placeholder = $select.data('placeholder') || $select.attr('placeholder');
    if (placeholder) {
        return placeholder;
    }
    
    // Check for first option as placeholder
    var firstOption = $select.find('option:first');
    if (firstOption.length && (firstOption.val() === '' || firstOption.val() === null)) {
        return firstOption.text();
    }
    
    // Default placeholder based on element attributes
    if ($select.prop('multiple')) {
        return 'انتخاب کنید...';
    } else if ($select.prop('required')) {
        return 'انتخاب کنید...';
    } else {
        return 'انتخاب کنید (اختیاری)...';
    }
}

/**
 * Format result display in dropdown
 */
function formatResult(result) {
    if (!result.id) {
        return result.text;
    }
    
    // Add icon support if data-icon attribute exists
    var $result = $(result.element);
    var icon = $result.data('icon');
    
    if (icon) {
        return $('<span><i class="' + icon + ' me-2"></i>' + result.text + '</span>');
    }
    
    return result.text;
}

/**
 * Format selection display in select box
 */
function formatSelection(selection) {
    return selection.text;
}

/**
 * Refresh Select2 instances (useful for dynamic content)
 */
function refreshSelect2() {
    $('.select2-hidden-accessible').each(function() {
        $(this).select2('destroy');
    });
    initializeSelect2();
}

/**
 * Add custom CSS to integrate Select2 with Bootstrap styling
 */
function addCustomSelect2Styles() {
    var css = `
        <style>
        /* Select2 Bootstrap 5 Integration */
        .select2-container .select2-selection--single {
            height: 38px !important;
            border: 1px solid #dee2e6 !important;
            border-radius: 0.375rem !important;
            font-size: 1rem;
        }
        
        .select2-container--default .select2-selection--single .select2-selection__rendered {
            line-height: 36px !important;
            padding-left: 12px !important;
            padding-right: 20px !important;
            color: #495057;
        }
        
        .select2-container--default .select2-selection--single .select2-selection__arrow {
            height: 36px !important;
            right: 1px !important;
        }
        
        .select2-container .select2-selection--multiple {
            border: 1px solid #dee2e6 !important;
            border-radius: 0.375rem !important;
            min-height: 38px !important;
        }
        
        .select2-container--default .select2-selection--multiple .select2-selection__choice {
            background-color: #0d6efd !important;
            border: 1px solid #0d6efd !important;
            color: white !important;
            border-radius: 0.25rem !important;
            margin: 2px !important;
        }
        
        .select2-container--default .select2-selection--multiple .select2-selection__choice__remove {
            color: white !important;
            margin-left: 5px !important;
        }
        
        .select2-dropdown {
            border: 1px solid #dee2e6 !important;
            border-radius: 0.375rem !important;
            box-shadow: 0 0.5rem 1rem rgba(0, 0, 0, 0.15) !important;
            z-index: 2000 !important;
        }

        /* Select2 inside Bootstrap modal must sit above modal (1055) and backdrop */
        .modal {
            overflow: visible !important;
        }
        .modal .modal-dialog,
        .modal .modal-content,
        .modal .modal-body {
            overflow: visible !important;
        }
        .modal .select2-container {
            z-index: 1065 !important;
        }
        .modal .select2-container--open {
            z-index: 2000 !important;
        }
        .modal .select2-dropdown {
            z-index: 2000 !important;
        }
        .select2-container--open {
            z-index: 2000 !important;
        }
        
        .select2-container--default .select2-search--dropdown .select2-search__field {
            border: 1px solid #dee2e6 !important;
            border-radius: 0.25rem !important;
            padding: 0.375rem 0.75rem !important;
            font-size: 1rem;
            direction: rtl;
        }
        
        /* RTL Support */
        .select2-container[dir="rtl"] .select2-selection--single .select2-selection__rendered {
            text-align: right;
        }
        
        .select2-container[dir="rtl"] .select2-selection--single .select2-selection__arrow {
            left: 1px;
            right: auto;
        }
        
        /* Focus states */
        .select2-container--default.select2-container--focus .select2-selection--single,
        .select2-container--default.select2-container--focus .select2-selection--multiple {
            border-color: #86b7fe !important;
            box-shadow: 0 0 0 0.25rem rgba(13, 110, 253, 0.25) !important;
        }
        
        /* Validation states */
        .is-invalid + .select2-container .select2-selection {
            border-color: #dc3545 !important;
        }
        
        .is-valid + .select2-container .select2-selection {
            border-color: #198754 !important;
        }
        </style>
    `;
    
    if ($('#select2-custom-styles').length === 0) {
        $('head').append(css.replace('<style>', '<style id="select2-custom-styles">'));
    }
}

// Add custom styles when document is ready
$(document).ready(function() {
    addCustomSelect2Styles();
});

// Global function to reinitialize Select2 (can be called from other scripts)
window.reinitializeSelect2 = refreshSelect2;
