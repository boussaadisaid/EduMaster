using EduMaster.Domain.Common;
using System.Text.RegularExpressions;




namespace EduMaster.Domain.AcademicYears.ValueObjects
{
    public sealed class YearName
    {
        public string Value { get; }

        public YearName(string value)
        {
            if(string.IsNullOrWhiteSpace(value))
                throw new DomainException("يجب ادخال السنة الدراسية.");

            var trimmed = value.Trim();
            if (!Regex.IsMatch(trimmed, @"^(20\d{2})-(20\d{2})$"))
                throw new DomainException("الصيغة المطلوبة: 2025-2026");
            
            Value = trimmed;           
        }

        public override bool Equals(object? obj) =>  obj is YearName other &&  other.Value == Value;

        public override int GetHashCode() => Value.GetHashCode(); 

        public override string ToString() => Value;
    }
}
