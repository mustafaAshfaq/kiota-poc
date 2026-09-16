using System.Collections.Concurrent;
using System.Reflection;
using CatalogApi;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Seeded, in-memory catalog. Restart re-seeds; there is no persistence.
var products = new ConcurrentDictionary<string, Product>([
    new KeyValuePair<string, Product>("sku-mug", new("sku-mug", "Demo Mug", 12.5m)),
    new KeyValuePair<string, Product>("sku-sticker", new("sku-sticker", "Demo Sticker", 3m)),
]);

builder.Services.AddOpenApi("v1", options =>
{
    // info.version comes from the csproj <Version>, exposed as the informational
    // version attribute. Strip any build metadata (e.g. "+<sha>").
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        var informational = typeof(Program).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var version = informational is null ? "1.0.0" : informational.Split('+')[0];
        document.Info.Version = version;
        return Task.CompletedTask;
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/products", () => TypedResults.Ok(new ProductList(products.Values.ToList())))
    .WithName("ListProducts");

app.MapPost("/products", Results<Created<Product>, ProblemHttpResult> (Product product) =>
    {
        if (string.IsNullOrWhiteSpace(product.Id) || string.IsNullOrWhiteSpace(product.Name) || product.Price < 0)
        {
            return TypedResults.Problem(
                detail: "Product id and name are required, and price must be greater than or equal to 0.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                type: "https://tools.ietf.org/html/rfc9110#section-15.5.1");
        }

        if (!products.TryAdd(product.Id, product))
        {
            return TypedResults.Problem(
                detail: $"Product {product.Id} already exists.",
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict",
                type: "https://tools.ietf.org/html/rfc9110#section-15.5.10");
        }

        return TypedResults.Created($"/products/{product.Id}", product);
    })
    .WithName("CreateProduct")
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status409Conflict);

app.MapGet("/products/{id}", Results<Ok<Product>, ProblemHttpResult> (string id) =>
    {
        return products.TryGetValue(id, out var product)
            ? TypedResults.Ok(product)
            : TypedResults.Problem(
                detail: $"Product {id} was not found.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                type: "https://tools.ietf.org/html/rfc9110#section-15.5.5");
    })
    .WithName("GetProduct")
    .ProducesProblem(StatusCodes.Status404NotFound);

app.MapDelete("/products/{id}", Results<NoContent, ProblemHttpResult> (string id) =>
    {
        if (!products.TryRemove(id, out _))
        {
            return TypedResults.Problem(
                detail: $"Product {id} was not found.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                type: "https://tools.ietf.org/html/rfc9110#section-15.5.5");
        }

        return TypedResults.NoContent();
    })
    .WithName("DeleteProduct")
    .ProducesProblem(StatusCodes.Status404NotFound);


app.MapGet("/health", () => TypedResults.Ok()).WithName("Health");
app.MapGet("/ready", () => TypedResults.Ok()).WithName("Ready");

app.Run();

/// <summary>Exposed so the test project can drive the app via WebApplicationFactory.</summary>
public partial class Program;
