using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.AdminCashRegisters;
using Moq;

namespace KasseAPI_Final.Tests;

internal static class CashRegisterTestDoubles
{
    internal static ICashRegisterListEnrichmentService NoOpListEnrichment()
    {
        var enrichment = new Mock<ICashRegisterListEnrichmentService>();
        enrichment
            .Setup(e => e.ApplyAsync(
                It.IsAny<IReadOnlyList<CashRegisterDto>>(),
                It.IsAny<IReadOnlyList<CashRegister>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return enrichment.Object;
    }
}
