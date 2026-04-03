using System.Linq;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services.RestoreVerification;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class ExternalDependencyRecoveryRollupCalculatorTests
{
    [Fact]
    public void Compute_when_any_domain_failed_yields_failed()
    {
        var domains = new[]
        {
            new ExternalDependencyDomainEvidence
            {
                Domain = ExternalDependencyRecoveryDomain.SecretsAndConfiguration,
                State = ExternalDependencyProofState.Failed
            },
            new ExternalDependencyDomainEvidence
            {
                Domain = ExternalDependencyRecoveryDomain.TseDeviceVendor,
                State = ExternalDependencyProofState.Passed
            }
        };

        var rollup = ExternalDependencyRecoveryRollupCalculator.Compute(domains, null);
        Assert.Equal(ExternalDependencyProofState.Failed, rollup.OverallState);
    }

    [Fact]
    public void Compute_when_not_implemented_present_yields_not_proven()
    {
        var domains = new[]
        {
            new ExternalDependencyDomainEvidence
            {
                Domain = ExternalDependencyRecoveryDomain.BackupTooling,
                State = ExternalDependencyProofState.NotImplemented
            }
        };

        var rollup = ExternalDependencyRecoveryRollupCalculator.Compute(domains, "n");
        Assert.Equal(ExternalDependencyProofState.NotProven, rollup.OverallState);
    }

    [Fact]
    public void Compute_when_all_passed_yields_passed()
    {
        var domains = Enumerable.Range(0, 5).Select(i => new ExternalDependencyDomainEvidence
        {
            Domain = (ExternalDependencyRecoveryDomain)i,
            State = ExternalDependencyProofState.Passed
        }).ToArray();

        var rollup = ExternalDependencyRecoveryRollupCalculator.Compute(domains, null);
        Assert.Equal(ExternalDependencyProofState.Passed, rollup.OverallState);
    }
}
