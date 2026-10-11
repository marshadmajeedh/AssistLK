using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AssistLK.Application.DTOs;

namespace AssistLK.Application.Services;

public interface IProviderBookingQueryService
{
    Task<List<ProviderBookingListDto>> GetForProviderAsync(
        Guid providerId,
        bool completed);
}