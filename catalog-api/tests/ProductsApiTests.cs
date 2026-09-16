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

    [Fact]
    public async Task CreateProduct_adds_product()
    {
        var client = _factory.CreateClient();
        var product = new Product($"sku-created-{Guid.NewGuid():N}", "Created Product", 7.25m);

        var response = await client.PostAsJsonAsync("/products", product);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/products/{product.Id}", response.Headers.Location?.OriginalString);
        var created = await response.Content.ReadFromJsonAsync<Product>();
        Assert.NotNull(created);
        Assert.Equal(product, created);

        var getResponse = await client.GetAsync($"/products/{product.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<Product>();
        Assert.Equal(product, fetched);
    }

    [Fact]
    public async Task CreateProduct_returns_problem_409_for_duplicate_id()
    {
        var client = _factory.CreateClient();
        var product = new Product($"sku-duplicate-{Guid.NewGuid():N}", "Duplicate Product", 4m);
        var firstResponse = await client.PostAsJsonAsync("/products", product);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        var response = await client.PostAsJsonAsync("/products", product);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(409, problem!.Status);
        Assert.Equal("Conflict", problem.Title);
        Assert.Equal($"Product {product.Id} already exists.", problem.Detail);
    }

    [Theory]
    [InlineData("", "Valid Name", 1)]
    [InlineData("sku-invalid", "", 1)]
    [InlineData("sku-invalid", "Valid Name", -1)]
    public async Task CreateProduct_returns_problem_400_for_invalid_product(string id, string name, decimal price)
    {
        var client = _factory.CreateClient();
        var product = new Product(id, name, price);

        var response = await client.PostAsJsonAsync("/products", product);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(400, problem!.Status);
        Assert.Equal("Bad Request", problem.Title);
        Assert.Equal("Product id and name are required, and price must be greater than or equal to 0.", problem.Detail);
    }

    [Fact]
    public async Task DeleteProduct_removes_product()
    {
        var client = _factory.CreateClient();
        var product = new Product($"sku-delete-{Guid.NewGuid():N}", "Delete Product", 2.5m);
        var createResponse = await client.PostAsJsonAsync("/products", product);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var response = await client.DeleteAsync($"/products/{product.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var getResponse = await client.GetAsync($"/products/{product.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteProduct_returns_problem_404_for_missing_id()
    {
        var client = _factory.CreateClient();
        var id = $"sku-missing-{Guid.NewGuid():N}";

        var response = await client.DeleteAsync($"/products/{id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(404, problem!.Status);
        Assert.Equal("Not Found", problem.Title);
        Assert.Equal($"Product {id} was not found.", problem.Detail);
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
