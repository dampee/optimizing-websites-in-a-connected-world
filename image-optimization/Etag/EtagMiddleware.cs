using Microsoft.Net.Http.Headers;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.Common.ApplicationBuilder;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace image_optimization.Etag;

public class ETagMiddleware
{
    // create custom middleware for Umbraco:
    // https://docs.umbraco.com/umbraco-cms/reference/routing/custom-middleware
    private readonly RequestDelegate _next;
    private readonly IRuntimeState _runtimeState;

    public ETagMiddleware(RequestDelegate next, ILogger<ETagMiddleware> logger,
        IRuntimeState runtimeState)
    {
        _next = next;
        _runtimeState = runtimeState;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // ignore this middleware as long as umbraco hasn't been initialised yet
        if (_runtimeState.Level < RuntimeLevel.Run)
        {
            await _next(context);
            return;
        }

        // Get the body content from the response stream
        var originalResponseBodyStream = context.Response.Body;
        using var responseBody = new System.IO.MemoryStream();
        context.Response.Body = responseBody;

        // Call the next middleware in the pipeline
        await _next(context);

        var responseBodyBytes = responseBody.ToArray();

        // Compute ETag for the response content using SHA-1
        var etag = ComputeETag(responseBodyBytes);

        context.Response.Headers.Add(HeaderNames.ETag, etag);

        // Check the incoming If-None-Match header
        if (context.Request.Headers.TryGetValue(HeaderNames.IfNoneMatch, out var incomingEtag) && incomingEtag == etag)
        {
            context.Response.StatusCode = StatusCodes.Status304NotModified;
            return;
        }

        // Write the original response body to the original response stream
        await originalResponseBodyStream.WriteAsync(responseBodyBytes, 0, responseBodyBytes.Length);
    }

    private string ComputeETag(byte[] content)
    {
        using var hashingAlgo = MD5.Create();
        var hash = hashingAlgo.ComputeHash(content);
        return Convert.ToBase64String(hash);
    }
}

public class EtagMiddlewareStartupFilter : IUmbracoPipelineFilter
{
    public string Name => "Etag";

    public void OnEndpoints(IApplicationBuilder app)
    {
     
    }

    public void OnPostPipeline(IApplicationBuilder app)
    {
        app.UseMiddleware<ETagMiddleware>();   
    }

    public void OnPrePipeline(IApplicationBuilder app)
    {
    }
}

public class ConfigureEtagPipelineOptions : IConfigureOptions<UmbracoPipelineOptions>
{
    public void Configure(UmbracoPipelineOptions options)
    {
        options.AddFilter(new EtagMiddlewareStartupFilter());
    }
}