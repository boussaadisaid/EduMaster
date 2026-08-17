using EduMaster.Domain.Common;
using System.Text.RegularExpressions;

namespace EduMaster.Domain.People.ValueObjects
{
    public class FirstName
    {
        public string Value { get;}
        private static readonly Regex NameRegex = new Regex(@"^\s*[\p{L}]+(?:\s+[\p{L}]+)*\s*$");

        public FirstName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new DomainException("الإسم مطلوب");

            var trimmed = value.Trim();

            if (trimmed.Length <= 1)
                throw new DomainException("يجب ان يحتوي الاسم أكثر من حرف.");

            if (trimmed.Length >= 50)
                throw new DomainException("الإسم طويل جدا.");

            if (!NameRegex.IsMatch(trimmed))
                throw new DomainException("الإسم يجب أن يحتوي على حروف فقط.");

            Value = trimmed;
        }

        public override bool Equals(object? obj)
        {
            return obj is FirstName other && other.Value == Value;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString() => Value;
    }
}
