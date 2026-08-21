using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.People;

public sealed record SetPersonPhotoRequest(int PersonId, string? SourcePath);   // null = إزالة الصورة

public sealed class SetPersonPhotoHandler
{
    private readonly IPersonRepository _persons;
    private readonly IImageStore _imageStore;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SetPersonPhotoHandler> _logger;

    public SetPersonPhotoHandler(IPersonRepository persons, IImageStore imageStore, IClock clock,
        ICurrentUserService currentUser, IUnitOfWork unitOfWork, ILogger<SetPersonPhotoHandler> logger)
    {
        _persons = persons;
        _imageStore = imageStore;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }



    public async Task<OperationResult> ExecuteAsync(SetPersonPhotoRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var person = await _persons.GetByIdAsync(request.PersonId, cancellationToken);
            if (person is null)
                return OperationResult.Failure("الشخص غير موجود.", ErrorType.NotFound);

            // النسخ قبل المعاملة — null يعني إزالة
            string? storedPhoto = null;
            if (!string.IsNullOrWhiteSpace(request.SourcePath))
            {
                try
                {
                    storedPhoto = await _imageStore.SaveFromPathAsync(request.SourcePath, cancellationToken);
                }
                catch (InvalidOperationException)
                {
                    return OperationResult.Failure("الصورة غير مدعومة أو يتجاوز حجمها 5MB — المسموح: jpg / png.", ErrorType.Validation);
                }
            }

            person.ChangePhoto(storedPhoto, _clock.UtcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _persons.UpdateAsync(person, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult.Success();
        }
        catch (DomainException dex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            return OperationResult.Failure(dex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to set photo for person {PersonId}", request.PersonId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء حفظ الصورة.", ErrorType.Unexpected);
        }
    }
}