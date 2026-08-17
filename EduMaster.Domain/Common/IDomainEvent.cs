using System;




namespace EduMaster.Domain.Common
{
    public interface IDomainEvent
    {
        DateTime OccurredOn { get;}
    }
}
