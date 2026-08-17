using EduMaster.Domain.AcademicYears.Events;
using EduMaster.Domain.AcademicYears.ValueObjects;
using EduMaster.Domain.Common;




namespace EduMaster.Domain.AcademicYears
{
    public class AcademicYear
    {
        private readonly List<IDomainEvent> _events = new();
        public IReadOnlyList<IDomainEvent> Events => _events.AsReadOnly();

        public int Id { get; private set; }
        public YearName Name { get; private set; }
        public DateOnly StartDate { get; private set; }
        public DateOnly EndDate { get; private set; }
        public bool IsCurrent { get; private set; }

        private bool _idSet = false;

        // for Create
        private AcademicYear(YearName name, DateOnly startDate, DateOnly endDate, bool isCurrent)
        {
            Name = name;
            StartDate = startDate;
            EndDate = endDate;
            IsCurrent = isCurrent;
        }


        // for Load
        private AcademicYear(int id, YearName name, DateOnly startDate, DateOnly endDate, bool isCurrent)
        {
            if (id <= 0)
                throw new DomainException("الـ ID يجب أن يكون أكبر من صفر");

            Name = name;
            StartDate = startDate;
            EndDate = endDate;
            IsCurrent = isCurrent;
            Id = id;
            _idSet = true;
        }

        public static AcademicYear Create(YearName name, DateOnly startDate, DateOnly endDate)
        {
            Validate(name, startDate, endDate);
            return new AcademicYear(name, startDate, endDate, false);
        }

        public static AcademicYear Load(int id, YearName name, DateOnly startDate, DateOnly endDate, bool isCurrent)
        {
            Validate(name, startDate, endDate);
            return new AcademicYear(id, name, startDate, endDate, isCurrent);
        }

        public void Update(YearName name, DateOnly startDate, DateOnly endDate)
        {
            Validate(name, startDate, endDate);

            Name = name;
            StartDate = startDate;
            EndDate = endDate;
        }

        private static void Validate(YearName name, DateOnly startDate, DateOnly endDate)
        {
            if (startDate >= endDate)
                throw new DomainException("تاريخ بداية السنة يجب أن يكون أقل من تاريخ نهاية السنة");

            var parts = name.Value.Split('-');

            if (parts[0] != startDate.Year.ToString() || parts[1] != endDate.Year.ToString())
                throw new DomainException("تاريخ البداية والنهاية يجب أن يوافقا اسم السنة الدراسية");
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

        public void SetAsCurrent()
        {
            if (IsCurrent)
                return;

            IsCurrent = true;
            _events.Add(new AcademicYearSetAsCurrentEvent(Id));
        }

        public void SetAsNotCurrent()
        {
            if (!IsCurrent)
                return;

            IsCurrent = false;
        }

        public override string ToString() => Name.ToString();
    }
}