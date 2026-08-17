using EduMaster.Domain.Common;
using System;
using System.Text.RegularExpressions;


namespace EduMaster.Domain.People.ValueObjects
{
    public class Email
    {
        public string Value { get; }

        public Email(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new DomainException("البريد الالكتروني لا يمكن أن يكون فارغا");

            var trimmed = value.Trim();

            if(!IsValid(trimmed))
                throw new DomainException("البريد الالكتروني غير صالح");

            Value = trimmed;
            
        }

        private bool IsValid(string email)
        {
            return Regex.IsMatch(email, @"^[^\s@]+@([^\s@]+\.)+[^\s@]+$");
        }

        public override bool Equals(object? obj)
        {
            if(obj != null && obj is Email other)            
                return other.Value  == this.Value;

            return false;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value;
        }
    }
}
