using EduMaster.Application.Common;




namespace EduMaster.Application.Abstractions
{
    public interface IDatabaseInitializer
    {
        Task<OperationResult> InitializeAsync(CancellationToken cancellationToken = default);
    }
}
