using System;
using System.ComponentModel.DataAnnotations;

namespace AltinnReStorage.Attributes
{
    /// <summary>
    /// InstanceId validation attribute.
    /// </summary>
    public class InstanceIdAttribute : ValidationAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InstanceIdAttribute"/> class.
        /// </summary>
        public InstanceIdAttribute() : base("{0} is invalid. A valid instance id has the form [instanceOwner.partyId]/[instanceGuid]")
        {
        }

        /// <summary>
        /// Validator.
        /// </summary>
        protected override ValidationResult IsValid(object value, ValidationContext context)
        {
            if (value == null
                || !(value is string)
                || !value.ToString().Contains('/')
                || !int.TryParse(value.ToString().Split('/')[0], out _)
                || !Guid.TryParse(value.ToString().Split('/')[1], out _))
            {
                return new ValidationResult(FormatErrorMessage(context.DisplayName));
            }

            return ValidationResult.Success;
        }
    }
}
