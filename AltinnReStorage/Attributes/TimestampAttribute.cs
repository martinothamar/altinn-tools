using System;
using System.ComponentModel.DataAnnotations;

namespace AltinnReStorage.Attributes
{
    /// <summary>
    /// Timestamp valiation attribute.
    /// </summary>
    public class TimestampAttribute : ValidationAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TimestampAttribute"/> class.
        /// </summary>
        public TimestampAttribute() : base("The value of {0} must be a valid timestamp.")
        {
        }

        /// <summary>
        /// Validator.
        /// </summary>
        protected override ValidationResult IsValid(object value, ValidationContext context)
        {
            if (value == null || !DateTime.TryParse(value.ToString(), out _))
            {
                return new ValidationResult(FormatErrorMessage(context.DisplayName));
            }

            return ValidationResult.Success;
        }
    }
}
