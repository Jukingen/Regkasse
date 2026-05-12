using System.Security.Claims;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Filters;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KasseAPI_Final.Tests;

public class RequireDisclaimerAcknowledgmentFilterTests
{
    private static ActionExecutingContext CreateExecutingContext(HttpContext http)
    {
        var actionCtx = new ActionContext(http, new RouteData(), new ActionDescriptor());
        return new ActionExecutingContext(
            actionCtx,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller: new object());
    }

    private static ActionExecutionDelegate NextFor(ActionExecutingContext executing, Action? mark = null) =>
        async () =>
        {
            mark?.Invoke();
            await Task.CompletedTask;
            return new ActionExecutedContext(
                executing,
                new List<IFilterMetadata>(),
                executing.Controller);
        };

    private static RequireDisclaimerAcknowledgmentFilter CreateFilter(FiscalExportOptions? options = null) =>
        new(Options.Create(options ?? new FiscalExportOptions()),
            new DisclaimerService(),
            NullLogger<RequireDisclaimerAcknowledgmentFilter>.Instance);

    [Fact]
    public async Task MissingHeader_WhenRequired_SetsForbidden_AndDoesNotRunNext()
    {
        var httpCtx = new DefaultHttpContext();
        httpCtx.User = new ClaimsPrincipal(new ClaimsIdentity(Array.Empty<Claim>(), authenticationType: "mock"));

        var executing = CreateExecutingContext(httpCtx);

        var ranNext = false;
        var filter = CreateFilter(new FiscalExportOptions
        {
            RequireDisclaimerAcknowledgment = true,
            LogFailedAttempts = false,
        });

        await filter.OnActionExecutionAsync(executing, NextFor(executing, () => ranNext = true));

        Assert.False(ranNext);
        var obj = Assert.IsType<ObjectResult>(executing.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, obj.StatusCode);
        var dto = Assert.IsType<FiscalExportDisclaimerRequiredResponseDto>(obj.Value);
        Assert.Equal("disclaimer_required", dto.Error);
        Assert.Contains("rechtlichen Hinweis", dto.Message, StringComparison.Ordinal);
        Assert.Equal(FiscalExportDisclaimerPaths.RelativeDisclaimerUrl, dto.DisclaimerUrl);
        Assert.NotNull(dto.Disclaimer);
        Assert.False(string.IsNullOrWhiteSpace(dto.Disclaimer.De));
        Assert.False(string.IsNullOrWhiteSpace(dto.Disclaimer.En));
    }

    [Fact]
    public async Task AcknowledgedHeader_RunsNext()
    {
        var httpCtx = new DefaultHttpContext();
        httpCtx.Request.Headers[FiscalExportDisclaimerHeaders.AcknowledgedHeaderName] = "TRUE";
        var executing = CreateExecutingContext(httpCtx);

        var ranNext = false;
        await CreateFilter().OnActionExecutionAsync(executing, NextFor(executing, () => ranNext = true));

        Assert.Null(executing.Result);
        Assert.True(ranNext);
    }

    [Fact]
    public async Task RequirementDisabled_AlwaysPasses()
    {
        var httpCtx = new DefaultHttpContext();
        var executing = CreateExecutingContext(httpCtx);

        var ranNext = false;
        var filter = CreateFilter(new FiscalExportOptions { RequireDisclaimerAcknowledgment = false });

        await filter.OnActionExecutionAsync(executing, NextFor(executing, () => ranNext = true));

        Assert.Null(executing.Result);
        Assert.True(ranNext);
    }
}
