using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using PosCartItemResponse = KasseAPI_Final.Controllers.CartItemResponse;
using PosCartResponse = KasseAPI_Final.Controllers.CartResponse;
using PosCartTaxSummaryLine = KasseAPI_Final.Controllers.CartTaxSummaryLine;

namespace KasseAPI_Final.Services;

/// <summary>Shared cart → API response mapping (used by CartController and table ops).</summary>
public static class CartResponseBuilder
{
    public static PosCartResponse Build(Cart cart, IReadOnlyDictionary<Guid, Product> products)
    {
        var allLineAmounts = new List<CartMoneyHelper.LineAmounts>();
        var items = (cart.Items ?? Enumerable.Empty<CartItem>()).Select(ci =>
        {
            var prod = products.TryGetValue(ci.ProductId, out var p) ? p : null;
            var taxType = prod?.TaxType ?? 1;
            var productLine = CartMoneyHelper.ComputeLine(ci.UnitPrice, ci.Quantity, taxType);
            var modifierLines = (ci.Modifiers ?? Enumerable.Empty<CartItemModifier>())
                .Select(m => CartMoneyHelper.ComputeLine(m.Price, m.Quantity, taxType))
                .ToList();
            allLineAmounts.Add(productLine);
            allLineAmounts.AddRange(modifierLines);
            var lineGross = productLine.LineGross + modifierLines.Sum(l => l.LineGross);
            var lineNet = productLine.LineNet + modifierLines.Sum(l => l.LineNet);
            var lineTax = productLine.LineTax + modifierLines.Sum(l => l.LineTax);
            var selectedModifiers = (ci.Modifiers ?? Enumerable.Empty<CartItemModifier>())
                .Select(m => new SelectedModifierDto
                {
                    Id = m.ModifierId,
                    Name = m.Name,
                    Price = m.Price,
                    Quantity = m.Quantity,
                    GroupId = m.ModifierGroupId,
                })
                .ToList();
            return new PosCartItemResponse
            {
                Id = ci.Id,
                ProductId = ci.ProductId,
                ProductName = prod?.Name ?? "Unknown Product",
                ProductImage = prod?.ImageUrl,
                Quantity = ci.Quantity,
                UnitPrice = ci.UnitPrice,
                TotalPrice = CartMoneyHelper.Round(lineGross),
                LineNet = CartMoneyHelper.Round(lineNet),
                LineTax = CartMoneyHelper.Round(lineTax),
                Notes = ci.Notes,
                TaxType = productLine.TaxType,
                TaxRate = productLine.TaxRate,
                AppliedPricingRuleId = ci.AppliedPricingRuleId,
                SelectedModifiers = selectedModifiers,
            };
        }).ToList();

        var totals = CartMoneyHelper.ComputeCartTotals(allLineAmounts);
        var taxSummary = totals.TaxSummary.Select(t => new PosCartTaxSummaryLine
        {
            TaxType = t.TaxType,
            TaxRatePct = t.TaxRatePct,
            NetAmount = t.NetAmount,
            TaxAmount = t.TaxAmount,
            GrossAmount = t.GrossAmount,
        }).ToList();

        return new PosCartResponse
        {
            Id = cart.Id,
            CartId = cart.CartId,
            TableNumber = cart.TableNumber,
            WaiterName = cart.WaiterName,
            ActorUserId = cart.UserId,
            CustomerId = cart.CustomerId,
            Notes = cart.Notes,
            Status = cart.Status,
            CreatedAt = cart.CreatedAt,
            ExpiresAt = cart.ExpiresAt,
            Items = items,
            TotalItems = (cart.Items ?? Enumerable.Empty<CartItem>()).Sum(ci => ci.Quantity),
            SubtotalGross = totals.SubtotalGross,
            SubtotalNet = totals.SubtotalNet,
            IncludedTaxTotal = totals.IncludedTaxTotal,
            GrandTotalGross = totals.GrandTotalGross,
            TaxSummary = taxSummary,
        };
    }
}
