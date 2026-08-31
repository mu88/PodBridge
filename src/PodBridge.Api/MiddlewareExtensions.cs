namespace PodBridge.Api;

internal static class MiddlewareExtensions
{
    public static WebApplication UseSecurityHeaders(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            context.Response.Headers.XFrameOptions = "DENY";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";

            // img-src allows any https origin because podcast cover art is hotlinked from whatever
            // upstream host the configured GraphQL endpoint returns (not fixed to one domain).
            context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' https:";
            await next();
        });

        return app;
    }
}
