using EduMaster.Application.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EduMaster.Application.Abstractions
{
    public interface IDatabaseHealthCheck
    {
        Task<OperationResult> CheckAsync(CancellationToken cancellationToken = default);
    }
}
