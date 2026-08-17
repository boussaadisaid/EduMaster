using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Users;

public sealed class LoginHandler
{
    private readonly IUserAccountRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LoginHandler> _logger;

    public LoginHandler(
        IUserAccountRepository users,
        IPasswordHasher hasher,
        IClock clock,
        IUnitOfWork unitOfWork,
        ILogger<LoginHandler> logger)
    {
        _users = users;
        _hasher = hasher;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult<LoggedInUser>> ExecuteAsync(
        LoginRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return OperationResult<LoggedInUser>.Failure("أدخل اسم المستخدم وكلمة المرور.", ErrorType.Validation);

        const string genericError = "بيانات الدخول غير صحيحة.";

        try
        {
            var account = await _users.GetByUsernameAsync(request.Username.Trim(), cancellationToken);
            if (account is null)
                return OperationResult<LoggedInUser>.Failure(genericError, ErrorType.BusinessRule);

            if (!account.IsActive)
                return OperationResult<LoggedInUser>.Failure("هذا الحساب معطّل. راجع الإدارة.", ErrorType.BusinessRule);

            if (account.IsLockedOut)
                return OperationResult<LoggedInUser>.Failure("الحساب مقفل مؤقتاً بعد محاولات فاشلة متكررة. راجع الإدارة.", ErrorType.BusinessRule);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            if (!_hasher.Verify(request.Password, account.PasswordHash))
            {
                account.RegisterFailedLogin();
                await _users.UpdateAsync(account, cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);   // ⭐ بدونه لا يُحفظ العداد أبداً
                _logger.LogWarning("Failed login attempt for username {Username}", request.Username);
                return OperationResult<LoggedInUser>.Failure(genericError, ErrorType.BusinessRule);
            }

            account.RegisterSuccessfulLogin(_clock.UtcNow);
            await _users.UpdateAsync(account, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult<LoggedInUser>.Success(
                new LoggedInUser(account.Id, account.Username, account.PersonId));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Login failed unexpectedly for username {Username}", request.Username);
            return OperationResult<LoggedInUser>.Failure("حدث خطأ غير متوقع أثناء تسجيل الدخول.", ErrorType.Unexpected);
        }
    }
}