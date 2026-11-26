using DailyChallenges.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DailyChallenges.Controllers;

/// <summary>What an action answers when the token carries no numeric user id.</summary>
public enum MissingUserIdResponse
{
    /// <summary>403 Forbid.</summary>
    Forbidden,

    /// <summary>401 Unauthorized.</summary>
    Unauthorized
}

/// <summary>
/// Binds an action's <c>int</c> parameter to the authenticated caller's user id.
/// When the token carries no numeric user id, <see cref="RequireCurrentUserIdFilter"/> answers with
/// <see cref="WhenMissing"/> (403 by default) before the action runs (after model validation, as the
/// per-action checks it replaces did).
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class CurrentUserIdAttribute : ModelBinderAttribute
{
    /// <summary>Not part of the request, so ApiExplorer/Swagger do not list the parameter.</summary>
    public static readonly BindingSource Source =
        new("CurrentUserId", "Current user id", isGreedy: true, isFromRequest: false);

    /// <summary>The same binding for parameters whose action answers 401 when the id is missing.</summary>
    public static readonly BindingSource UnauthorizedWhenMissingSource =
        new("CurrentUserIdOrUnauthorized", "Current user id", isGreedy: true, isFromRequest: false);

    private MissingUserIdResponse _whenMissing = MissingUserIdResponse.Forbidden;

    public CurrentUserIdAttribute() : base(typeof(CurrentUserIdModelBinder))
    {
        BindingSource = Source;
    }

    public MissingUserIdResponse WhenMissing
    {
        get => _whenMissing;
        set
        {
            _whenMissing = value;
            BindingSource = value == MissingUserIdResponse.Unauthorized ? UnauthorizedWhenMissingSource : Source;
        }
    }
}

public sealed class CurrentUserIdModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        if (bindingContext.HttpContext.User.GetUserId() is int userId)
            bindingContext.Result = ModelBindingResult.Success(userId);
        return Task.CompletedTask;
    }
}

/// <summary>Registered globally; acts only on actions that take a <see cref="CurrentUserIdAttribute"/> parameter.</summary>
public sealed class RequireCurrentUserIdFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.HttpContext.User.GetUserId() is null)
            context.Result = ResultWhenUserIdMissing(context);
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }

    /// <summary>Null for actions that do not take the current user id, so they run as usual.</summary>
    private static IActionResult? ResultWhenUserIdMissing(ActionExecutingContext context)
    {
        var sources = context.ActionDescriptor.Parameters.Select(parameter => parameter.BindingInfo?.BindingSource).ToList();
        if (sources.Contains(CurrentUserIdAttribute.UnauthorizedWhenMissingSource)) return new UnauthorizedResult();
        if (sources.Contains(CurrentUserIdAttribute.Source)) return new ForbidResult();
        return null;
    }
}
