using System.Globalization;
using Microsoft.Kiota.Abstractions.Serialization;

namespace StorefrontApi;

/// <summary>A product as surfaced by the Storefront dashboard.</summary>
public record StorefrontProduct(string? Id, string? Name, decimal? Price);

/// <summary>Envelope returned by <c>GET /shop/dashboard</c>.</summary>
public record DashboardResponse(IReadOnlyList<StorefrontProduct> Products);

/// <summary>
/// Request body for <c>POST /shop/checkout</c>. Deliberately the same shape as the
/// Orders producer's order-create DTO — the Storefront does not invent a second
/// DTO family.
/// </summary>
public record CheckoutRequest(string ProductId, int Quantity);

/// <summary>An order as surfaced by the Storefront checkout.</summary>
public record StorefrontOrder(string? Id, string? ProductId, int? Quantity, string? Status, DateTimeOffset? CreatedAt);

/// <summary>
/// The producers model <c>price</c>/<c>quantity</c> as JSON <c>number</c>/<c>string</c>
/// unions, so Kiota emits them as <see cref="UntypedNode"/>. These helpers collapse a
/// node back to a plain CLR value for the Storefront's own response shape.
/// </summary>
internal static class UntypedValues
{
    public static decimal? ToDecimal(UntypedNode? node) => node switch
    {
        UntypedDecimal m => m.GetValue(),
        UntypedDouble d => (decimal)d.GetValue(),
        UntypedFloat f => (decimal)f.GetValue(),
        UntypedLong l => l.GetValue(),
        UntypedInteger i => i.GetValue(),
        UntypedString s when decimal.TryParse(s.GetValue(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) => v,
        _ => null,
    };

    public static int? ToInt(UntypedNode? node) => node switch
    {
        UntypedInteger i => i.GetValue(),
        UntypedLong l => (int)l.GetValue(),
        UntypedDouble d => (int)d.GetValue(),
        UntypedDecimal m => (int)m.GetValue(),
        UntypedFloat f => (int)f.GetValue(),
        UntypedString s when int.TryParse(s.GetValue(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) => v,
        _ => null,
    };
}
