using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EduMaster.Application.AcademicYears.UpdateAcademicYear
{
    public sealed record UpdateAcademicYearCommand
        (
        int Id,
         string Name,
         DateOnly StartDate,
         DateOnly EndDate
        );

}
