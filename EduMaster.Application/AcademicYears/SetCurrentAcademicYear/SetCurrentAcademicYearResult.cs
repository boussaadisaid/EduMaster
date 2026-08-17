using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EduMaster.Application.AcademicYears.SetCurrentAcademicYear
{
    public sealed record SetCurrentAcademicYearResult(
        int CurrentAcademicYearId,
        string CurrentAcademicYearName);
    
}
