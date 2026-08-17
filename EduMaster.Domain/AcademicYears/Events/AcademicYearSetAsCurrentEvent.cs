using EduMaster.Domain.Common;


namespace EduMaster.Domain.AcademicYears.Events
{
    public class AcademicYearSetAsCurrentEvent : IDomainEvent
    {
        public AcademicYearSetAsCurrentEvent(int academicYearId)
        {
            AcademicYearId = academicYearId;
            OccurredOn = DateTime.UtcNow;
        }

        public int AcademicYearId { get;}
        public DateTime OccurredOn { get;}

        
    }
}
