using EduMaster.Application.AcademicYears.Repositories;
using EduMaster.Domain.AcademicYears.ValueObjects;

namespace EduMaster.Application.AcademicYears.UpdateAcademicYear
{
    public sealed class UpdateAcademicYearHandler
    {
        private readonly IAcademicYearRepository _repository;

        public UpdateAcademicYearHandler(IAcademicYearRepository repository)
        {
            _repository = repository;
        }

        public async Task<UpdateAcademicYearResult> Handle(
            UpdateAcademicYearCommand command,
            CancellationToken cancellationToken = default)
        {
            var academicYear = await _repository.GetByIdAsync(command.Id, cancellationToken);

            if (academicYear is null)
                throw new ArgumentException("هذه السنة الدراسية غير موجودة.");

            var yearName = new YearName(command.Name);

            academicYear.Update(yearName, command.StartDate, command.EndDate);

            await _repository.UpdateAsync(academicYear, cancellationToken);

            return new UpdateAcademicYearResult(
                academicYear.Id,
                academicYear.Name.Value,
                academicYear.StartDate,
                academicYear.EndDate,
                academicYear.IsCurrent);
        }
    }
}