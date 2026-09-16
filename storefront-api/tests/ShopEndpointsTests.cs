using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace StorefrontApi.Tests;

public class ShopEndpointsTests
{
    private const string CatalogList =
        """{"products":[{"id":"sku-mug","name":"Demo Mug","price":12.5},{"id":"sku-sticker","name":"Demo Sticker","price":3}]}""";

    private const string MugProduct =
        """{"id":"sku-mug","name":"Demo Mug","price":12.5}""";

    private const string NotFoundProblem =
        """{"type":"about:blank","title":"Not Found","status":404,"detail":"Product sku-missing was not found."}""";

    [Fact]
    public async Task Dashboard_returns_products_via_catalog_kiota_client()
    {
        using var factory = new StorefrontFactory().WithProducers(
            catalog: _ => FakeHttpMessageHandler.Json(HttpStatusCode.OK, CatalogList),
            orders: _ => throw new InvalidOperationException("Orders must not be called by the dashboard."));
        var client = factory.CreateClient();

        var response = await client.GetAsync("/shop/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var products = doc.RootElement.GetProperty("products");
        Assert.Equal(2, products.GetArrayLength());
        Assert.Equal("sku-mug", products[0].GetProperty("id").GetString());
        Assert.Equal("Demo Mug", products[0].GetProperty("name").GetString());
        Assert.Equal(12.5m, products[0].GetProperty("price").GetDecimal());

        // The Storefront reached Catalog through the generated client.
        var catalogRequest = Assert.Single(factory.Catalog.Requests);
        Assert.Equal(HttpMethod.Get, catalogRequest.Method);
        Assert.Equal("http://catalog.test/products", catalogRequest.RequestUri!.ToString());
    }

    [Fact]
    public async Task Dashboard_returns_502_problem_when_catalog_fails()
    {
        using var factory = new StorefrontFactory().WithProducers(
            catalog: _ => new HttpResponseMessage(HttpStatusCode.InternalServerError),
            orders: _ => throw new InvalidOperationException("Orders must not be called by the dashboard."));
        var client = factory.CreateClient();

        var response = await client.GetAsync("/shop/dashboard");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Checkout_creates_order_when_product_exists()
    {
        var orderResponse =
            """{"id":"ord-1","productId":"sku-mug","quantity":2,"status":"accepted","createdAt":"2026-09-14T12:00:00Z"}""";

        using var factory = new StorefrontFactory().WithProducers(
            catalog: _ => FakeHttpMessageHandler.Json(HttpStatusCode.OK, MugProduct),
            orders: _ => FakeHttpMessageHandler.Json(HttpStatusCode.Created, orderResponse));
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/shop/checkout", new { productId = "sku-mug", quantity = 2 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ord-1", doc.RootElement.GetProperty("id").GetString());
        Assert.Equal("sku-mug", doc.RootElement.GetProperty("productId").GetString());
        Assert.Equal(2, doc.RootElement.GetProperty("quantity").GetInt32());
        Assert.Equal("accepted", doc.RootElement.GetProperty("status").GetString());

        // Catalog get-by-id then Orders create, both via the generated clients.
        var catalogRequest = Assert.Single(factory.Catalog.Requests);
        Assert.Equal("http://catalog.test/products/sku-mug", catalogRequest.RequestUri!.ToString());
        var ordersRequest = Assert.Single(factory.Orders.Requests);
        Assert.Equal(HttpMethod.Post, ordersRequest.Method);
        Assert.Equal("http://orders.test/orders", ordersRequest.RequestUri!.ToString());
    }

    [Fact]
    public async Task Checkout_returns_404_and_does_not_call_orders_for_unknown_sku()
    {
        using var factory = new StorefrontFactory().WithProducers(
            catalog: _ => FakeHttpMessageHandler.Json(
                HttpStatusCode.NotFound, NotFoundProblem, "application/problem+json"),
            orders: _ => throw new InvalidOperationException("Orders must not be called for an unknown SKU."));
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/shop/checkout", new { productId = "sku-missing", quantity = 1 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(404, problem!.Status);

        // Catalog was consulted; Orders was never called.
        Assert.Single(factory.Catalog.Requests);
        Assert.Empty(factory.Orders.Requests);
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/ready")]
    public async Task Probes_return_200(string path)
    {
        using var factory = new StorefrontFactory().WithProducers(
            catalog: _ => FakeHttpMessageHandler.Json(HttpStatusCode.OK, CatalogList),
            orders: _ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
