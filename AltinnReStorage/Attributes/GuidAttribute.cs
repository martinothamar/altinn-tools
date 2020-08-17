using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace AltinnReStorage.Attributes
{
    /// <summary>
    /// Guid valiation attribute.
    /// </summary>
    public class GuidAttribute : ValidationAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GuidAttribute"/> class.
        /// </summary>
        public GuidAttribute() : base("The value of {0} must be a valid guid.")
        {
        }

        /// <summary>
        /// Validator.
        /// </summary>
        protected override ValidationResult IsValid(object value, ValidationContext context)
        {
            if (value == null || !Guid.TryParse(value.ToString(), out _))
            {
                return new ValidationResult(FormatErrorMessage(context.DisplayName));
            }

            return ValidationResult.Success;
        }
    }
}
