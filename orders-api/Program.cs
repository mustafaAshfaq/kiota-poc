using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.OpenApi;
using OrdersApi;

var builder = WebApplication.CreateBuilder(args);

// Process-wide, in-memory order store. Restart clears it; there is no persistence.
var orders = new ConcurrentDictionary<string, Order>();
var counter = 0;

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

app.MapPost("/orders", Results<Created<Order>, ProblemHttpResult> (CreateOrderRequest request) =>
    {
        if (request.Quantity < 1)
        {
            return TypedResults.Problem(
                detail: "Quantity must be an integer greater than or equal to 1.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                type: "https://tools.ietf.org/html/rfc9110#section-15.5.1");
        }

        var id = $"ord-{Interlocked.Increment(ref counter)}";
        var order = new Order(id, request.ProductId, request.Quantity, "accepted", DateTimeOffset.UtcNow);
        orders[id] = order;
        return TypedResults.Created($"/orders/{id}", order);
    })
    .WithName("CreateOrder")
    .ProducesProblem(StatusCodes.Status400BadRequest);

app.MapGet("/orders/{id}", Results<Ok<Order>, ProblemHttpResult> (string id) =>
    {
        return orders.TryGetValue(id, out var order)
            ? TypedResults.Ok(order)
            : TypedResults.Problem(
                detail: $"Order {id} was not found.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                type: "https://tools.ietf.org/html/rfc9110#section-15.5.5");
    })
    .WithName("GetOrder")
    .ProducesProblem(StatusCodes.Status404NotFound);

app.MapGet("/health", () => TypedResults.Ok()).WithName("Health");
app.MapGet("/ready", () => TypedResults.Ok()).WithName("Ready");

app.Run();

/// <summary>Exposed so the test project can drive the app via WebApplicationFactory.</summary>
public partial class Program;
