using System.Security.Claims;
using DailyChallenges.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Moq;

namespace DailyChallenges.Tests;

public class CurrentUserIdTests
{
    private static ClaimsPrincipal Authenticated(string? nameIdentifier)
    {
        var claims = nameIdentifier == null ? Array.Empty<Claim>() : new[] { new Claim(ClaimTypes.NameIdentifier, nameIdentifier) };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static ActionExecutingContext ExecutingContext(ClaimsPrincipal user, BindingSource? parameterSource)
    {
        var parameters = parameterSource == null
            ? new List<ParameterDescriptor>()
            : new List<ParameterDescriptor> { new() { Name = "userId", BindingInfo = new BindingInfo { BindingSource = parameterSource } } };
        var actionContext = new ActionContext(
            new DefaultHttpContext { User = user },
            new RouteData(),
            new ActionDescriptor { Parameters = parameters });
        return new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), controller: new object());
    }

    private static async Task<ModelBindingResult> BindAsync(ClaimsPrincipal user)
    {
        var context = new Mock<ModelBindingContext>();
        context.SetupGet(c => c.HttpContext).Returns(new DefaultHttpContext { User = user });
        context.SetupProperty(c => c.Result);

        await new CurrentUserIdModelBinder().BindModelAsync(context.Object);

        return context.Object.Result;
    }

    [Fact]
    public async Task Binder_binds_the_numeric_user_id()
    {
        var result = await BindAsync(Authenticated("42"));

        Assert.True(result.IsModelSet);
        Assert.Equal(42, result.Model);
    }

    [Fact]
    public async Task Binder_leaves_the_parameter_unset_without_a_numeric_user_id()
    {
        Assert.False((await BindAsync(Authenticated("not-a-number"))).IsModelSet);
    }

    [Fact]
    public void Filter_forbids_an_action_that_needs_a_user_id_the_token_lacks()
    {
        var context = ExecutingContext(Authenticated(null), CurrentUserIdAttribute.Source);

        new RequireCurrentUserIdFilter().OnActionExecuting(context);

        Assert.IsType<ForbidResult>(context.Result);
    }

    [Fact]
    public void Filter_answers_unauthorized_when_the_parameter_asks_for_it()
    {
        var context = ExecutingContext(Authenticated(null), CurrentUserIdAttribute.UnauthorizedWhenMissingSource);

        new RequireCurrentUserIdFilter().OnActionExecuting(context);

        Assert.IsType<UnauthorizedResult>(context.Result);
    }

    [Fact]
    public void Attribute_switches_to_the_unauthorized_source_when_missing_ids_answer_401()
    {
        var attribute = new CurrentUserIdAttribute { WhenMissing = MissingUserIdResponse.Unauthorized };

        Assert.Same(CurrentUserIdAttribute.UnauthorizedWhenMissingSource, attribute.BindingSource);
        Assert.False(attribute.BindingSource!.IsFromRequest);
    }

    [Fact]
    public void Filter_lets_a_caller_with_a_user_id_through()
    {
        var context = ExecutingContext(Authenticated("7"), CurrentUserIdAttribute.Source);

        new RequireCurrentUserIdFilter().OnActionExecuting(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void Filter_ignores_actions_without_a_current_user_id_parameter()
    {
        var context = ExecutingContext(new ClaimsPrincipal(), parameterSource: null);

        new RequireCurrentUserIdFilter().OnActionExecuting(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void Attribute_uses_a_binding_source_outside_the_request()
    {
        var attribute = new CurrentUserIdAttribute();

        Assert.Same(CurrentUserIdAttribute.Source, attribute.BindingSource);
        Assert.False(CurrentUserIdAttribute.Source.IsFromRequest);
    }
}
