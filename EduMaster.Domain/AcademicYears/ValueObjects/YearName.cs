using EduMaster.Domain.Common;
using System.Text.RegularExpressions;

namespace EduMaster.Domain.AcademicYears.ValueObjects
{
    public sealed class YearName
    {
        public string Value { get; }
        public int StartYear { get; }
        public int EndYear { get; }

        private static readonly Regex YearNameRegex = new Regex(@"^(20\d{2})-(20\d{2})$");

        public YearName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new DomainException("يجب ادخال السنة الدراسية.");

            var trimmed = value.Trim();

            var match = YearNameRegex.Match(trimmed);
            if (!match.Success)
                throw new DomainException("الصيغة المطلوبة مثل: 2025-2026");

            StartYear = int.Parse(match.Groups[1].Value);
            EndYear = int.Parse(match.Groups[2].Value);

            if (EndYear != StartYear + 1)
                throw new DomainException("يجب أن تكون السنة الدراسية من سنتين متتاليتين.");

            Value = trimmed;
        }

        public override bool Equals(object? obj) => obj is YearName other && other.Value == Value;

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value;
    }
}