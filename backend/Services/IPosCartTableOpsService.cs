using KasseAPI_Final.DTOs;
using PosCartResponse = KasseAPI_Final.Controllers.CartResponse;

namespace KasseAPI_Final.Services;

public interface IPosCartTableOpsService
{
    Task<PosCartResponse> SplitItemsAsync(
        string userId,
        SplitCartItemsRequest request,
        CancellationToken cancellationToken = default);

    Task<PosCartResponse> MergeTablesAsync(
        string userId,
        MergeTableCartsRequest request,
        CancellationToken cancellationToken = default);
}
