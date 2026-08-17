using EduMaster.Domain.Common;
using System;
using System.Globalization;



namespace EduMaster.Domain.People.ValueObjects
{
    public sealed class BirthDate
    {
        public DateOnly Value { get; }

        public BirthDate(DateOnly value, DateOnly today)
        {
            if(value >= today)
                throw new DomainException("تاريخ الميلاد لا يمكن ان يكون في المستقبل");

            if(today.Year - value.Year > 100)
                throw new DomainException("تاريخ الميلاد غير منطقي");

            Value = value;
        }

        public override bool Equals(object? obj)
        {
            if(obj is BirthDate other)
                return Value.Equals(other.Value); ;

            return false;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString() => Value.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);

    }
}
