using EduMaster.Domain.Common;
using System.Text.RegularExpressions;



namespace EduMaster.Domain.People.ValueObjects
{
    public class Phone
    {
        public string Value { get; }

        public Phone(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new DomainException("رقم الهاتف لا يمكن ان يكون فارغا.");

            var trimmed = value.Trim();

            if(!IsValid(trimmed))
                throw new DomainException("رقم الهاتف غير صالح.");

            Value = trimmed;
        }

        private bool IsValid(string phone)
        {
            return Regex.IsMatch(phone, @"^\d{10}$");
        }

        public override bool Equals(object? obj)
        {
            if (obj is null) return false;

            if (obj is Phone other)
                return other.Value == Value;

            return false;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString() => Value;
    }
}