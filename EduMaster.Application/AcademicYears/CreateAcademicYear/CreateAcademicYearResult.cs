using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EduMaster.Application.AcademicYears.CreateAcademicYear
{
    public sealed record CreateAcademicYearResult(
        int Id,
        string Name,
        DateOnly StartDate,
        DateOnly EndDate,
        bool IsCurrent);
   
}
