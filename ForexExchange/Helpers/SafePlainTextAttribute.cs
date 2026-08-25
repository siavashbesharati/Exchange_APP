using System.ComponentModel.DataAnnotations;

namespace ForexExchange.Helpers
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
    public sealed class SafePlainTextAttribute : ValidationAttribute
    {
        public SafePlainTextAttribute()
            : base(SafePlainTextHelper.ValidationErrorMessage)
        {
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is not string text || string.IsNullOrEmpty(text))
            {
                return ValidationResult.Success;
            }

            var error = SafePlainTextHelper.GetValidationError(text);
            if (error == null)
            {
                return ValidationResult.Success;
            }

            return new ValidationResult(error, new[] { validationContext.MemberName ?? string.Empty });
        }
    }
}
