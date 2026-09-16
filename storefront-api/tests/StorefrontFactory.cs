using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace StorefrontApi.Tests;

/// <summary>
/// Boots the Storefront in-process and replaces the primary HTTP handler of each named
/// client ("catalog" / "orders") with a <see cref="FakeHttpMessageHandler"/>, so requests
/// travel through the real Kiota-generated clients but terminate at the fakes.
/// </summary>
public sealed class StorefrontFactory : WebApplicationFactory<Program>
{
    private FakeHttpMessageHandler? _catalog;
    private FakeHttpMessageHandler? _orders;

    public FakeHttpMessageHandler Catalog => _catalog ?? throw new InvalidOperationException("Not configured.");
    public FakeHttpMessageHandler Orders => _orders ?? throw new InvalidOperationException("Not configured.");

    public StorefrontFactory WithProducers(
        Func<HttpRequestMessage, HttpResponseMessage> catalog,
        Func<HttpRequestMessage, HttpResponseMessage> orders)
    {
        _catalog = new FakeHttpMessageHandler(catalog);
        _orders = new FakeHttpMessageHandler(orders);
        return this;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // BaseUrls are supplied from configuration exactly as they would be from
        // Catalog__BaseUrl / Orders__BaseUrl environment variables in production.
        builder.UseSetting("Catalog:BaseUrl", "http://catalog.test");
        builder.UseSetting("Orders:BaseUrl", "http://orders.test");

        builder.ConfigureServices(services =>
        {
            services.AddHttpClient("catalog")
                .ConfigurePrimaryHttpMessageHandler(() => _catalog!);
            services.AddHttpClient("orders")
                .ConfigurePrimaryHttpMessageHandler(() => _orders!);
        });
    }
}
