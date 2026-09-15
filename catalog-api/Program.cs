using System.Reflection;
using CatalogApi;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Seeded, in-memory catalog. Restart re-seeds; there is no persistence.
var products = new List<Product>
{
    new("sku-mug", "Demo Mug", 12.5m),
    new("sku-sticker", "Demo Sticker", 3m),
};

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

app.MapGet("/products", () => TypedResults.Ok(new ProductList(products)))
    .WithName("ListProducts");

app.MapGet("/products/{id}", Results<Ok<Product>, ProblemHttpResult> (string id) =>
    {
        var product = products.FirstOrDefault(p => p.Id == id);
        return product is null
            ? TypedResults.Problem(
                detail: $"Product {id} was not found.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                type: "https://tools.ietf.org/html/rfc9110#section-15.5.5")
            : TypedResults.Ok(product);
    })
    .WithName("GetProduct")
    .ProducesProblem(StatusCodes.Status404NotFound);

app.MapGet("/health", () => TypedResults.Ok()).WithName("Health");
app.MapGet("/ready", () => TypedResults.Ok()).WithName("Ready");

app.Run();

/// <summary>Exposed so the test project can drive the app via WebApplicationFactory.</summary>
public partial class Program;
