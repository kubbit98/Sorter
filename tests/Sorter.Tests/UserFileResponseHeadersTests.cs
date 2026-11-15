using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Sorter.Data;

namespace Sorter.Tests;

public class UserFileResponseHeadersTests
{
    private static IHeaderDictionary HeadersFor(string fileName)
    {
        var httpContext = new DefaultHttpContext();
        UserFileResponseHeaders.Apply(new StaticFileResponseContext(httpContext, new NotFoundFileInfo(fileName)));
        return httpContext.Response.Headers;
    }

    [Theory]
    [InlineData("vector.svg")]
    [InlineData("VECTOR.SVG")]
    public void Svg_GetsRestrictiveContentSecurityPolicy(string fileName)
    {
        var headers = HeadersFor(fileName);

        Assert.Equal(UserFileResponseHeaders.SvgContentSecurityPolicy, headers.ContentSecurityPolicy.ToString());
        Assert.Contains("sandbox", headers.ContentSecurityPolicy.ToString());
        Assert.Equal("nosniff", headers.XContentTypeOptions.ToString());
    }

    [Theory]
    [InlineData("photo.jpg")]
    [InlineData("notes.txt")]
    [InlineData("svg")]
    public void OtherFiles_GetOnlyNoSniff(string fileName)
    {
        var headers = HeadersFor(fileName);

        Assert.Equal(string.Empty, headers.ContentSecurityPolicy.ToString());
        Assert.Equal("nosniff", headers.XContentTypeOptions.ToString());
    }
}
