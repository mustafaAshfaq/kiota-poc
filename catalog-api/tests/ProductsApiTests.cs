using System.Net;
using System.Net.Http.Json;
using CatalogApi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CatalogApi.Tests;

public class ProductsApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ProductsApiTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task ListProducts_returns_seeded_products()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProductList>();
        Assert.NotNull(body);
        Assert.Contains(body!.Products, p => p.Id == "sku-mug" && p.Name == "Demo Mug" && p.Price == 12.5m);
        Assert.Contains(body.Products, p => p.Id == "sku-sticker" && p.Name == "Demo Sticker" && p.Price == 3m);
    }

    [Fact]
    public async Task GetProduct_returns_single_product()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/products/sku-mug");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var product = await response.Content.ReadFromJsonAsync<Product>();
        Assert.NotNull(product);
        Assert.Equal("sku-mug", product!.Id);
        Assert.Equal("Demo Mug", product.Name);
        Assert.Equal(12.5m, product.Price);
    }

    [Fact]
    public async Task GetProduct_returns_problem_404_for_missing_id()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/products/sku-missing");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(404, problem!.Status);
        Assert.Equal("Not Found", problem.Title);
        Assert.Equal("Product sku-missing was not found.", problem.Detail);
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/ready")]
    public async Task Probes_return_200(string path)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
