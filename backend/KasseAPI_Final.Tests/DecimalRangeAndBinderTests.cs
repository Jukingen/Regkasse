using System.ComponentModel.DataAnnotations;
using System.Globalization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.ModelBinding;
using KasseAPI_Final.Models.DTOs;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DecimalRangeAndBinderTests
{
    [Fact]
    public void UpdateTenantLimitsRequest_accepts_0_01_under_de_AT()
    {
        using var _ = GermanAustrianCultureScope.Enter();

        var request = new UpdateTenantLimitsRequest
        {
            MaxTransactionAmount = 0.01m,
            DailyMaxRevenue = 0.01m,
        };

        var results = new List<ValidationResult>();
        var ok = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            results,
            validateAllProperties: true);

        Assert.True(ok, string.Join("; ", results.Select(r => r.ErrorMessage)));
        Assert.Empty(results);
    }

    [Fact]
    public void RefundPaymentRequest_accepts_0_01_under_de_AT()
    {
        using var _ = GermanAustrianCultureScope.Enter();

        var request = new RefundPaymentRequest
        {
            Amount = 0.01m,
            Reason = "Teilrückerstattung Test",
            ReasonCode = RefundReasonCode.Overcharged,
        };

        var results = new List<ValidationResult>();
        var ok = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            results,
            validateAllProperties: true);

        Assert.True(ok, string.Join("; ", results.Select(r => r.ErrorMessage)));
    }

    [Fact]
    public void UpdateTenantLimitsRequest_rejects_zero_amount()
    {
        using var _ = GermanAustrianCultureScope.Enter();

        var request = new UpdateTenantLimitsRequest { MaxTransactionAmount = 0m };
        var results = new List<ValidationResult>();
        var ok = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            results,
            validateAllProperties: true);

        Assert.False(ok);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateTenantLimitsRequest.MaxTransactionAmount)));
    }

    [Theory]
    [InlineData("0.01", 0.01)]
    [InlineData("10000.50", 10000.50)]
    public void DecimalModelBinder_parses_invariant_dot_under_de_AT(string input, double expected)
    {
        using var _ = GermanAustrianCultureScope.Enter();

        Assert.True(DecimalModelBinder.TryParseDecimal(input, out var result));
        Assert.Equal((decimal)expected, result);
    }

    [Fact]
    public void Range_string_limits_without_invariant_flag_fail_under_de_AT()
    {
        using var _ = GermanAustrianCultureScope.Enter();

        var attr = new RangeAttribute(typeof(decimal), "0.01", "1000000000");
        var ex = Record.Exception(() => attr.IsValid(10m));

        Assert.NotNull(ex);
    }

    private sealed class GermanAustrianCultureScope : IDisposable
    {
        private readonly CultureInfo _culture;
        private readonly CultureInfo _uiCulture;

        private GermanAustrianCultureScope()
        {
            _culture = CultureInfo.CurrentCulture;
            _uiCulture = CultureInfo.CurrentUICulture;
            var deAt = CultureInfo.GetCultureInfo("de-AT");
            CultureInfo.CurrentCulture = deAt;
            CultureInfo.CurrentUICulture = deAt;
        }

        public static GermanAustrianCultureScope Enter() => new();

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _culture;
            CultureInfo.CurrentUICulture = _uiCulture;
        }
    }
}
