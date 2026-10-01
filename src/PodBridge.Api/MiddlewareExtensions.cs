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

            // Scalar's API reference UI renders via dynamically injected inline styles and loads its
            // font from a CDN, which the strict app-wide policy below would block - relax it only for
            // that path, since it's a local developer tool rather than a security-sensitive page.
            // img-src allows any https origin because podcast cover art is hotlinked from whatever
            // upstream host the configured GraphQL endpoint returns (not fixed to one domain).
            context.Response.Headers.ContentSecurityPolicy = context.Request.Path.StartsWithSegments("/scalar", StringComparison.Ordinal)
                ? "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com; img-src 'self' https: data:; connect-src 'self'"
                : "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' https:";
            await next();
        });

        return app;
    }
}
