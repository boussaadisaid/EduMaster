using EduMaster.Application.AcademicYears.Repositories;
using EduMaster.Domain.AcademicYears;
using EduMaster.Domain.AcademicYears.ValueObjects;

namespace EduMaster.Application.AcademicYears.CreateAcademicYear
{
    public sealed class CreateAcademicYearHandler
    {
        private readonly IAcademicYearRepository _repository;

        public CreateAcademicYearHandler(IAcademicYearRepository repository)
        {
            _repository = repository;
        }

        public async Task<CreateAcademicYearResult> Handle(
            CreateAcademicYearCommand command,
            CancellationToken cancellationToken = default)
        {
            var yearName = new YearName(command.Name);

            if (await _repository.ExistsByNameAsync(yearName, cancellationToken))
                throw new Exception("السنة الدراسية موجودة بالفعل.");

            var academicYear = AcademicYear.Create(
                yearName,
                command.StartDate,
                command.EndDate);

            await _repository.AddAsync(academicYear, cancellationToken);

            return new CreateAcademicYearResult(
                academicYear.Id,
                academicYear.Name.Value,
                academicYear.StartDate,
                academicYear.EndDate,
                academicYear.IsCurrent);
        }
    }
}