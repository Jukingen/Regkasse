using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Preorder;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PreorderStatusTransitionsTests
{
    [Theory]
    [InlineData(PreorderStatuses.Pending, PreorderStatuses.Ready, false, true)]
    [InlineData(PreorderStatuses.Pending, PreorderStatuses.Collected, false, true)]
    [InlineData(PreorderStatuses.Ready, PreorderStatuses.Collected, false, true)]
    [InlineData(PreorderStatuses.Collected, PreorderStatuses.Pending, false, false)]
    [InlineData(PreorderStatuses.Cancelled, PreorderStatuses.Ready, false, false)]
    [InlineData(PreorderStatuses.Pending, PreorderStatuses.Cancelled, false, false)]
    [InlineData(PreorderStatuses.Pending, PreorderStatuses.Cancelled, true, true)]
    [InlineData(PreorderStatuses.Ready, PreorderStatuses.Cancelled, true, true)]
    [InlineData(PreorderStatuses.Collected, PreorderStatuses.Cancelled, true, true)]
    public void CanTransition_MatchesOperationalRules(string from, string to, bool fiscalCancel, bool expected)
    {
        Assert.Equal(expected, PreorderStatusTransitions.CanTransition(from, to, fiscalCancel));
    }
}
