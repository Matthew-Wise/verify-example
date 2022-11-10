using DiffEngine;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace VerifyTestExamples;

/// <summary>
/// Credit to Lars Erik for this test class - https://blog.aabech.no/archive/unit-testing-the-iis-url-rewrite-module/
/// </summary>
public class UrlRewriting
{
    private static Uri[] TestCases => new[]
    {
        new Uri("http://localhost/abc"),
        new Uri("https://localhost/abc"),
        new Uri("https://localhost/aBc"),
        new Uri("http://localhost/tadabcAda"),
        new Uri("http://localhost/"),
        new Uri("http://localhost/tada")
    };

    private RewriteMiddleware _middleware = null!;
    private RedirectLogger _redirectLogger = null!;

    [SetUp]
    public void Setup()
    {
        DiffTools.UseOrder(DiffTool.Rider, DiffTool.VisualStudio, DiffTool.VisualStudioCode);
        var options = CreateOptions(GetType().Assembly
            .GetManifestResourceStream("VerifyTestExamples.UrlRewrite.Tests.testroutes.xml")!);
        _redirectLogger = new RedirectLogger();
        var loggerFactory = CreateLoggerFactory(_redirectLogger);
        _middleware = CreateMiddleware(loggerFactory, options);
    }

    [Test]
    public async Task Handles_All_Rules()
    {
        List<string> results = new();
        foreach (var uri in TestCases)
        {
            var ctx = CreateHttpContext(uri);
            _redirectLogger.Messages.Clear();
            await _middleware.Invoke(ctx);
            results.Add(uri + " => " + _redirectLogger);
        }

        await Verify(string.Join(Environment.NewLine + "-----" + Environment.NewLine, results));
    }

    private static HttpContext CreateHttpContext(Uri uri)
    {
        var ctx = Mock.Of<HttpContext>();
        var resp = Mock.Of<HttpResponse>();
        var headers = new HeaderDictionary();
        Mock.Get(resp).Setup(x => x.Headers).Returns(headers);
        Mock.Get(ctx).Setup(x => x.Response).Returns(resp);
        var req = Mock.Of<HttpRequest>();
        Mock.Get(ctx).Setup(x => x.Request).Returns(req);
        Mock.Get(req).Setup(x => x.Path).Returns(new PathString(uri.AbsolutePath));
        Mock.Get(req).Setup(x => x.Scheme).Returns(uri.Scheme);
        Mock.Get(req).Setup(x => x.Host).Returns(new HostString(uri.Host, uri.Port));
        Mock.Get(req).Setup(x => x.PathBase).Returns(new PathString("/"));
        Mock.Get(req).Setup(x => x.QueryString).Returns(new QueryString(uri.Query));
        var variableFeature = Mock.Of<IServerVariablesFeature>();
        var features = new FeatureCollection();
        features.Set(variableFeature);
        Mock.Get(ctx).Setup(x => x.Features).Returns(features);
        Mock.Get(variableFeature).Setup(x => x["HTTP_HOST"]).Returns(uri.Host);
        Mock.Get(variableFeature).Setup(x => x["HTTP_URL"]).Returns(uri.AbsolutePath);
        Mock.Get(variableFeature).Setup(x => x["HTTPS"]).Returns(uri.Scheme == "https" ? "on" : "off");
        return ctx;
    }

    private static RewriteMiddleware CreateMiddleware(ILoggerFactory loggerFactory, RewriteOptions options)
    {
        return new RewriteMiddleware(_ => Task.CompletedTask, Mock.Of<IWebHostEnvironment>(), loggerFactory,
            new OptionsWrapper<RewriteOptions>(options));
    }

    private static ILoggerFactory CreateLoggerFactory(ILogger logger)
    {
        var loggerFactory = Mock.Of<ILoggerFactory>();
        Mock.Get(loggerFactory).Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(logger);
        return loggerFactory;
    }

    private static RewriteOptions CreateOptions(Stream stream)
    {
        using var reader = new StreamReader(stream);
        var options = new RewriteOptions().AddIISUrlRewrite(reader);
        options.StaticFileProvider = Mock.Of<IFileProvider>();
        return options;
    }
}

public class RedirectLogger : ILogger
{
    public List<string> Messages { get; } = new();

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var output = formatter(state, exception);
        output = output.Replace("Request is done processing. ", "");
        output = output.Replace("Request is continuing in applying rules. ", "");
        Console.WriteLine(output);
        Messages.Add(output);
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public IDisposable BeginScope<TState>(TState state)
    {
        throw new NotImplementedException();
    }

    public override string ToString()
    {
        return string.Join(Environment.NewLine, Messages);
    }
}