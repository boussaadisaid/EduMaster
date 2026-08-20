using EduMaster.Domain.Common;
using System;
using System.Globalization;



namespace EduMaster.Domain.People.ValueObjects
{
    public sealed class BirthDate
    {
        public DateOnly Value { get; }

        private BirthDate(DateOnly value)
        {
            Value = value;
        }

        /// <summary>إنشاء جديد — القواعد الزمنية تعمل هنا فقط (وقت الإدخال، واليوم يُمرَّر من IClock)</summary>
        public static BirthDate Create(DateOnly value, DateOnly today)
        {
            if (value >= today)
                throw new DomainException("تاريخ الميلاد لا يمكن ان يكون في المستقبل");

            if (today.Year - value.Year > 100)
                throw new DomainException("تاريخ الميلاد غير منطقي");

            return new BirthDate(value);
        }

        /// <summary>تحميل من القاعدة — بلا إعادة تحقق زمني: القاعدة تحققت يوم الكتابة</summary>
        public static BirthDate Load(DateOnly value) => new(value);

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
