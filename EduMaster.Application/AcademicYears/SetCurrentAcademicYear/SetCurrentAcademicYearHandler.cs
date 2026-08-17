using EduMaster.Application.Abstractions;
using EduMaster.Application.AcademicYears.Repositories;

namespace EduMaster.Application.AcademicYears.SetCurrentAcademicYear
{
    public sealed class SetCurrentAcademicYearHandler
    {
        private readonly IAcademicYearRepository _repository;
        private readonly IUnitOfWork _unitOfWork;

        public SetCurrentAcademicYearHandler(
            IAcademicYearRepository repository,
            IUnitOfWork unitOfWork)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
        }

        public async Task<SetCurrentAcademicYearResult> Handle(
            SetCurrentAcademicYearCommand command,
            CancellationToken cancellationToken = default)
        {
            var targetAcademicYear = await _repository.GetByIdAsync(
                command.AcademicYearId,
                cancellationToken);

            if (targetAcademicYear is null)
                throw new ArgumentException("السنة الدراسية المطلوبة غير موجودة.");

            if (targetAcademicYear.IsCurrent)
            {
                return new SetCurrentAcademicYearResult(
                    targetAcademicYear.Id,
                    targetAcademicYear.Name.Value);
            }

            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                var currentAcademicYear = await _repository.GetCurrentAsync(cancellationToken);

                if (currentAcademicYear is not null)
                {
                    currentAcademicYear.SetAsNotCurrent();
                    await _repository.UpdateAsync(currentAcademicYear, cancellationToken);
                }

                targetAcademicYear.SetAsCurrent();
                await _repository.UpdateAsync(targetAcademicYear, cancellationToken);

                await _unitOfWork.CommitAsync(cancellationToken);

                return new SetCurrentAcademicYearResult(
                    targetAcademicYear.Id,
                    targetAcademicYear.Name.Value);
            }
            catch
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }
}