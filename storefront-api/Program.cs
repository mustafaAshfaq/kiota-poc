using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Http.HttpClientLibrary;
using StorefrontApi;
using Storefront.Generated.Catalog;
using Storefront.Generated.Orders;
using CatalogProblem = Storefront.Generated.Catalog.Models.ProblemDetails;
using OrdersCreateOrderRequest = Storefront.Generated.Orders.Models.CreateOrderRequest;
using OrdersProblem = Storefront.Generated.Orders.Models.ProblemDetails;

var builder = WebApplication.CreateBuilder(args);

// BaseUrls come from configuration only (env: Catalog__BaseUrl / Orders__BaseUrl).
// The committed OpenAPI documents carry no `servers` entry, so the generated clients
// have no embedded base address — it is supplied here at runtime.
var catalogBaseUrl = RequireBaseUrl("Catalog");
var ordersBaseUrl = RequireBaseUrl("Orders");

// Named HttpClients so tests can swap the primary handler for a fake, exercising the
// generated clients without any real producer.
builder.Services.AddHttpClient("catalog");
builder.Services.AddHttpClient("orders");

builder.Services.AddSingleton(sp =>
{
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("catalog");
    var adapter = new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: http)
    {
        BaseUrl = catalogBaseUrl,
    };
    return new CatalogClient(adapter);
});

builder.Services.AddSingleton(sp =>
{
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("orders");
    var adapter = new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: http)
    {
        BaseUrl = ordersBaseUrl,
    };
    return new OrdersClient(adapter);
});

var app = builder.Build();

// GET /shop/dashboard — Catalog list through the Kiota client only.
app.MapGet("/shop/dashboard",
    async Task<Results<Ok<DashboardResponse>, ProblemHttpResult>> (CatalogClient catalog, CancellationToken ct) =>
    {
        try
        {
            var list = await catalog.Products.GetAsync(cancellationToken: ct);
            var products = (list?.Products ?? [])
                .Select(p => new StorefrontProduct(p.Id, p.Name, UntypedValues.ToDecimal(p.Price)))
                .ToList();
            return TypedResults.Ok(new DashboardResponse(products));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return TypedResults.Problem(
                detail: "The catalog service could not be reached.",
                statusCode: StatusCodes.Status502BadGateway,
                title: "Bad Gateway",
                type: "https://tools.ietf.org/html/rfc9110#section-15.6.3");
        }
    })
    .WithName("ShopDashboard");

// POST /shop/checkout — Catalog get-by-id, then Orders create, both through Kiota.
app.MapPost("/shop/checkout",
    async Task<Results<Created<StorefrontOrder>, ProblemHttpResult>> (
        CheckoutRequest request, CatalogClient catalog, OrdersClient orders, CancellationToken ct) =>
    {
        // 1. Confirm the product exists via Catalog. A 404 short-circuits: return the
        //    ProblemDetails 404 without ever calling Orders.
        try
        {
            _ = await catalog.Products[request.ProductId].GetAsync(cancellationToken: ct);
        }
        catch (CatalogProblem)
        {
            return TypedResults.Problem(
                detail: $"Product {request.ProductId} was not found.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                type: "https://tools.ietf.org/html/rfc9110#section-15.5.5");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return TypedResults.Problem(
                detail: "The catalog service could not be reached.",
                statusCode: StatusCodes.Status502BadGateway,
                title: "Bad Gateway",
                type: "https://tools.ietf.org/html/rfc9110#section-15.6.3");
        }

        // 2. Product found — place the order via Orders.
        var body = new OrdersCreateOrderRequest
        {
            ProductId = request.ProductId,
            Quantity = new UntypedInteger(request.Quantity),
        };

        try
        {
            var order = await orders.Orders.PostAsync(body, cancellationToken: ct);
            var mapped = new StorefrontOrder(
                order?.Id,
                order?.ProductId,
                UntypedValues.ToInt(order?.Quantity),
                order?.Status,
                order?.CreatedAt);
            return TypedResults.Created($"/shop/orders/{order?.Id}", mapped);
        }
        catch (OrdersProblem problem)
        {
            return TypedResults.Problem(
                detail: problem.Detail ?? "The order was rejected.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                type: "https://tools.ietf.org/html/rfc9110#section-15.5.1");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return TypedResults.Problem(
                detail: "The orders service could not be reached.",
                statusCode: StatusCodes.Status502BadGateway,
                title: "Bad Gateway",
                type: "https://tools.ietf.org/html/rfc9110#section-15.6.3");
        }
    })
    .WithName("ShopCheckout");

app.MapGet("/health", () => TypedResults.Ok()).WithName("Health");
app.MapGet("/ready", () => TypedResults.Ok()).WithName("Ready");

app.Run();

string RequireBaseUrl(string service) =>
    builder.Configuration[$"{service}:BaseUrl"]
        ?? throw new InvalidOperationException(
            $"{service}__BaseUrl configuration is required (no embedded server in the OpenAPI contract).");

/// <summary>Exposed so the test project can drive the app via WebApplicationFactory.</summary>
public partial class Program;
