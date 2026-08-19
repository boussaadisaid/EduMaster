using EduMaster.Domain.AcademicYears.ValueObjects;
using EduMaster.Domain.Common;

namespace EduMaster.Domain.AcademicYears
{
    public class AcademicYear
    {
        public int Id { get; private set; }
        public YearName Name { get; private set; }
        public DateOnly StartDate { get; private set; }
        public DateOnly EndDate { get; private set; }
        public bool IsCurrent { get; private set; }
        public bool IsActive { get; private set; }
        public DateTime CreatedAtUtc { get; private set; }
        public int? CreatedByUserId { get; private set; }
        public DateTime? UpdatedAtUtc { get; private set; }
        public int? UpdatedByUserId { get; private set; }

        private bool _idSet;

        // Constructor for Create
        private AcademicYear(YearName name, DateOnly startDate, DateOnly endDate, bool isCurrent, bool isActive,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            Name = name;
            StartDate = startDate;
            EndDate = endDate;
            IsCurrent = isCurrent;
            IsActive = isActive;
            CreatedAtUtc = createdAtUtc;
            CreatedByUserId = createdByUserId;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        // Constructor for Load
        private AcademicYear(int id, YearName name, DateOnly startDate, DateOnly endDate, bool isCurrent, bool isActive,
            DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            if (id <= 0)
                throw new DomainException("الـ ID يجب أن يكون أكبر من صفر");

            Name = name;
            StartDate = startDate;
            EndDate = endDate;
            IsCurrent = isCurrent;
            IsActive = isActive;
            Id = id;
            CreatedAtUtc = createdAtUtc;
            CreatedByUserId = createdByUserId;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;

            _idSet = true;
        }

        public static AcademicYear Create(YearName name, DateOnly startDate, DateOnly endDate,
            DateTime createdAtUtc, int? createdByUserId)
        {
            Validate(name, startDate, endDate);
            return new AcademicYear(name, startDate, endDate, false, true, createdAtUtc, createdByUserId, null, null);
        }

        public static AcademicYear Load(int id, YearName name, DateOnly startDate, DateOnly endDate, bool isCurrent,
            bool isActive, DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            Validate(name, startDate, endDate);
            return new AcademicYear(id, name, startDate, endDate, isCurrent, isActive, createdAtUtc,
                createdByUserId, updatedAtUtc, updatedByUserId);
        }

        public void Update(YearName name, DateOnly startDate, DateOnly endDate,
            DateTime updatedAtUtc, int? updatedByUserId)
        {
            Validate(name, startDate, endDate);

            Name = name;
            StartDate = startDate;
            EndDate = endDate;

            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        internal void SetId(int id)
        {
            if (_idSet)
                throw new DomainException("لا يمكن تغيير الـ ID بعد تعيينه");

            if (id <= 0)
                throw new DomainException("الـ ID يجب أن يكون أكبر من صفر");

            Id = id;
            _idSet = true;
        }

        public void SetAsCurrent(DateTime updatedAtUtc, int? updatedByUserId)
        {
            if (!IsActive)
                throw new DomainException("لا يمكن تعيين سنة معطّلة كحالية");

            if (IsCurrent)
                return;

            IsCurrent = true;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        public void SetAsNotCurrent(DateTime updatedAtUtc, int? updatedByUserId)
        {
            if (!IsCurrent)
                return;

            IsCurrent = false;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        public void Deactivate(DateTime updatedAtUtc, int? updatedByUserId)
        {
            if (IsCurrent)
                throw new DomainException("لا يمكن تعطيل السنة الحالية — عيّن سنة أخرى أولاً");

            if (!IsActive)
                return;

            IsActive = false;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        public void Activate(DateTime updatedAtUtc, int? updatedByUserId)
        {
            if (IsActive)
                return;

            IsActive = true;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        private static void Validate(YearName name, DateOnly startDate, DateOnly endDate)
        {
            if (startDate >= endDate)
                throw new DomainException("تاريخ بداية السنة يجب أن يكون أقل من تاريخ نهاية السنة");

            if (startDate.Year != name.StartYear || endDate.Year != name.EndYear)
                throw new DomainException("تاريخ البداية والنهاية يجب أن يوافقا اسم السنة الدراسية");
        }

        public override string ToString() => Name.ToString();
    }
}