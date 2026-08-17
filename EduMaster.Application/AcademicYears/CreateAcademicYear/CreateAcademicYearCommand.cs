using System;
using System.Collections.Generic;




namespace EduMaster.Application.AcademicYears.CreateAcademicYear
{
    public sealed record CreateAcademicYearCommand
    (
         string Name,
         DateOnly StartDate,
         DateOnly EndDate
    );
}
