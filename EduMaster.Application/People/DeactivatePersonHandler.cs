using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.People;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EduMaster.Application.People
{
    public sealed class DeactivatePersonHandler
    {
        private readonly IPersonRepository _persons;
        private readonly IClock _clock;
        private readonly ICurrentUserService _currentUser;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<DeactivatePersonHandler> _logger;

        public DeactivatePersonHandler(
            IPersonRepository persons, IClock clock, ICurrentUserService currentUser,
            IUnitOfWork unitOfWork, ILogger<DeactivatePersonHandler> logger)
        {
            _persons = persons;
            _clock = clock;
            _currentUser = currentUser;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }


        public async Task<OperationResult> ExecuteAsync(DeactivatePersonRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                var person = await _persons.GetByIdAsync(request.Id, cancellationToken);
                if (person is null)
                    return OperationResult.Failure("الشخص غير موجود.", ErrorType.NotFound);

                if (!person.IsActive)
                    return OperationResult.Success();   // معطّل أصلاً — لا كتابة بلا معنى

                // ملاحظة: تعطيل الشخص لا يمس حساب دخوله — الحساب يُدار مستقلاً من بطاقة الحساب
                person.Deactivate(_clock.UtcNow, _currentUser.UserAccountId);

                await _unitOfWork.BeginTransactionAsync(cancellationToken);
                await _persons.UpdateAsync(person, cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);

                return OperationResult.Success();
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Failed to deactivate person {PersonId}", request.Id);
                return OperationResult.Failure("حدث خطأ غير متوقع أثناء تعطيل الشخص.", ErrorType.Unexpected);
            }
        }
    }
}
