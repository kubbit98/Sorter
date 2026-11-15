using Microsoft.AspNetCore.StaticFiles;

namespace Sorter.Data;

/// <summary>
/// Response headers for user files served from the source and destination folders.
/// </summary>
public static class UserFileResponseHeaders
{
    // inside <img> an SVG never runs scripts, but opened directly in a tab it would run them
    // in the app's origin; sandbox isolates it and the other directives block scripts and loading
    public const string SvgContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; img-src data:; sandbox";

    public static void Apply(StaticFileResponseContext context)
    {
        var headers = context.Context.Response.Headers;
        // the declared type must be used as is, e.g. a .txt containing HTML mustn't be rendered as a page
        headers.XContentTypeOptions = "nosniff";
        if (string.Equals(Path.GetExtension(context.File.Name), ".svg", StringComparison.OrdinalIgnoreCase))
        {
            headers.ContentSecurityPolicy = SvgContentSecurityPolicy;
        }
    }
}
