(function (window) {
    const message = "عنوان سند نباید شامل نقل‌قول ('), backslash (\\), خط جدید، یا تگ HTML باشد.";
    const pattern = /^[^'\\\r\n<>]*$/;
    const htmlTagPattern = /<\s*\/?\s*[a-zA-Z][^>]*|<[^>]*>/;
    const encodedTagPattern = /&lt;|&gt;/i;

    function isValid(value) {
        if (value == null || value === '') {
            return true;
        }

        return pattern.test(value) &&
            !htmlTagPattern.test(value) &&
            !encodedTagPattern.test(value);
    }

    function sanitize(value) {
        if (value == null || value === '') {
            return '';
        }

        return value
            .replace(/['\\\r\n<>]/g, ' ')
            .replace(/&lt;|&gt;/gi, ' ')
            .replace(/\s+/g, ' ')
            .trim();
    }

    function showFieldError(input, errorMessage) {
        input.classList.add('is-invalid');

        let feedback = input.parentElement.querySelector('.safe-plain-text-error');
        if (!feedback) {
            feedback = document.createElement('div');
            feedback.className = 'text-danger safe-plain-text-error';
            input.parentElement.appendChild(feedback);
        }

        feedback.textContent = errorMessage;
    }

    function clearFieldError(input) {
        input.classList.remove('is-invalid');

        const feedback = input.parentElement.querySelector('.safe-plain-text-error');
        if (feedback) {
            feedback.remove();
        }
    }

    function validateInput(input) {
        const value = input.value;
        if (!isValid(value)) {
            showFieldError(input, message);
            return false;
        }

        clearFieldError(input);
        return true;
    }

    window.SafePlainText = {
        message: message,
        pattern: pattern,
        isValid: isValid,
        sanitize: sanitize,
        validateInput: validateInput,
        clearFieldError: clearFieldError
    };
})(window);
