using System.Net;
using System.Text;

namespace StorefrontApi.Tests;

/// <summary>
/// A stand-in <see cref="HttpMessageHandler"/> that answers requests from a supplied
/// delegate. It lets the tests drive the Kiota-generated clients end to end without any
/// real producer, and records every request so a test can assert a producer was — or was
/// not — called.
/// </summary>
public sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    : HttpMessageHandler
{
    private readonly List<HttpRequestMessage> _requests = [];

    public IReadOnlyList<HttpRequestMessage> Requests => _requests;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _requests.Add(request);
        return Task.FromResult(responder(request));
    }

    public static HttpResponseMessage Json(HttpStatusCode status, string body, string mediaType = "application/json") =>
        new(status)
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType),
        };
}
