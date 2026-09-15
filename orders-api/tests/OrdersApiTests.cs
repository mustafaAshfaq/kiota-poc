using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using OrdersApi;
using Xunit;

namespace OrdersApi.Tests;

public class OrdersApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OrdersApiTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task CreateOrder_returns_created_accepted_order()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/orders", new CreateOrderRequest("sku-mug", 2));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<Order>();
        Assert.NotNull(order);
        Assert.False(string.IsNullOrWhiteSpace(order!.Id));
        Assert.Equal("sku-mug", order.ProductId);
        Assert.Equal(2, order.Quantity);
        Assert.Equal("accepted", order.Status);
        Assert.NotEqual(default, order.CreatedAt);
        Assert.Equal($"/orders/{order.Id}", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task GetOrder_returns_previously_created_order()
    {
        var client = _factory.CreateClient();

        var created = await client.PostAsJsonAsync("/orders", new CreateOrderRequest("sku-sticker", 5));
        var createdOrder = await created.Content.ReadFromJsonAsync<Order>();
        Assert.NotNull(createdOrder);

        var response = await client.GetAsync($"/orders/{createdOrder!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<Order>();
        Assert.NotNull(order);
        Assert.Equal(createdOrder.Id, order!.Id);
        Assert.Equal("sku-sticker", order.ProductId);
        Assert.Equal(5, order.Quantity);
        Assert.Equal("accepted", order.Status);
    }

    [Fact]
    public async Task GetOrder_returns_problem_404_for_missing_id()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/orders/ord-missing");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(404, problem!.Status);
        Assert.Equal("Not Found", problem.Title);
        Assert.Equal("Order ord-missing was not found.", problem.Detail);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateOrder_returns_problem_400_for_invalid_quantity(int quantity)
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/orders", new CreateOrderRequest("sku-mug", quantity));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(400, problem!.Status);
        Assert.Equal("Bad Request", problem.Title);
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
