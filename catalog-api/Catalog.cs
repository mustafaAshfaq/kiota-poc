namespace CatalogApi;

/// <summary>A product in the catalog.</summary>
public record Product(string Id, string Name, decimal Price);

/// <summary>Envelope returned by <c>GET /products</c>.</summary>
public record ProductList(IReadOnlyList<Product> Products);
