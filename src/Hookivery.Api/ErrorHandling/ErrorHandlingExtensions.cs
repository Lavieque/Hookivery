using System.Diagnostics;

namespace Hookivery.Api.ErrorHandling;

internal static class ErrorHandlingExtensions
{
    public static IServiceCollection AddErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
                context.ProblemDetails.Extensions.TryAdd(
                    "traceId",
                    Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
            });

        services.AddExceptionHandler<GlobalExceptionHandler>();
        return services;
    }

    public static WebApplication UseErrorHandling(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        return app;
    }
}
