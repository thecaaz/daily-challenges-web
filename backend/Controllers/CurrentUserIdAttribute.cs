using DailyChallenges.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DailyChallenges.Controllers;

/// <summary>
/// Binds an action's <c>int</c> parameter to the authenticated caller's user id.
/// When the token carries no numeric user id, <see cref="RequireCurrentUserIdFilter"/> answers 403 Forbid
/// before the action runs (after model validation, as the per-action checks it replaces did).
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class CurrentUserIdAttribute : ModelBinderAttribute
{
    /// <summary>Not part of the request, so ApiExplorer/Swagger do not list the parameter.</summary>
    public static readonly BindingSource Source =
        new("CurrentUserId", "Current user id", isGreedy: true, isFromRequest: false);

    public CurrentUserIdAttribute() : base(typeof(CurrentUserIdModelBinder))
    {
        BindingSource = Source;
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
        if (RequiresCurrentUserId(context) && context.HttpContext.User.GetUserId() is null)
            context.Result = new ForbidResult();
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }

    private static bool RequiresCurrentUserId(ActionExecutingContext context) =>
        context.ActionDescriptor.Parameters.Any(parameter => parameter.BindingInfo?.BindingSource == CurrentUserIdAttribute.Source);
}
