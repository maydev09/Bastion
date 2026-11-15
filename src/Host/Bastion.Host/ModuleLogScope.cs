namespace Bastion.Host;

internal sealed class ModuleLogScope(string moduleName) : IEndpointFilter
{
    public const string Property = "Module";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<ModuleLogScope>>();
        using (logger.BeginScope(new Dictionary<string, object> { [Property] = moduleName }))
        {
            return await next(context);
        }
    }
}
