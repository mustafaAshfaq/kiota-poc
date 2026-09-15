namespace OrdersApi;

/// <summary>Request body for <c>POST /orders</c>.</summary>
public record CreateOrderRequest(string ProductId, int Quantity);

/// <summary>An order accepted by the producer.</summary>
public record Order(string Id, string ProductId, int Quantity, string Status, DateTimeOffset CreatedAt);
