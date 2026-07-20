using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Website;

namespace KasseAPI_Final.Sites;

/// <summary>
/// Dynamic tenant website HTML (shared platform). Uses live catalog API data —
/// not the one-click static snapshot under /media/sites.
/// </summary>
public sealed class TenantWebsiteService : ITenantWebsiteService
{
    private readonly IPublicTenantCatalogService _catalog;

    public TenantWebsiteService(IPublicTenantCatalogService catalog)
    {
        _catalog = catalog;
    }

    public async Task<string?> GetWebsiteHtmlAsync(
        string slug,
        string? templateId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return null;

        var profile = await _catalog.GetProfileAsync(slug, ct);
        if (profile is null)
            return null;

        var menu = await _catalog.GetMenuAsync(slug, ct);
        if (menu is null)
            return null;

        var template = NormalizeTemplate(templateId);
        return WebsiteHtmlRenderer.Render(profile, menu, template);
    }

    private static string NormalizeTemplate(string? templateId)
    {
        var id = (templateId ?? "modern").Trim().ToLowerInvariant();
        return id is "classic" or "minimal" or "modern" ? id : "modern";
    }
}

internal static class WebsiteHtmlRenderer
{
    public static string Render(
        PublicTenantProfileDto tenant,
        PublicTenantMenuDto menu,
        string templateId)
    {
        var enc = HtmlEncoder.Default;
        var name = enc.Encode(tenant.DisplayName);
        var description = enc.Encode(
            string.IsNullOrWhiteSpace(tenant.Description)
                ? $"{tenant.DisplayName} — Speisekarte"
                : tenant.Description!);
        var primary = enc.Encode(tenant.PrimaryColor);
        var accent = enc.Encode(tenant.AccentColor);
        var address = enc.Encode(tenant.Address ?? string.Empty);
        var phone = enc.Encode(tenant.Phone ?? string.Empty);
        var email = enc.Encode(tenant.Email ?? string.Empty);
        var logo = string.IsNullOrWhiteSpace(tenant.LogoUrl)
            ? string.Empty
            : $"<img class=\"logo\" src=\"{enc.Encode(tenant.LogoUrl)}\" alt=\"{name}\" />";

        var menuHtml = BuildMenuHtml(menu, enc);
        var statusHtml = BuildOrderStatusHtml(tenant, enc);
        var css = BuildCss(templateId, primary, accent);
        var contactBits = new StringBuilder();
        if (!string.IsNullOrEmpty(address))
            contactBits.Append("<p class=\"address\">").Append(address).Append("</p>");
        if (!string.IsNullOrEmpty(phone))
            contactBits.Append("<p class=\"phone\"><a href=\"tel:").Append(phone).Append("\">").Append(phone).Append("</a></p>");
        if (!string.IsNullOrEmpty(email))
            contactBits.Append("<p class=\"email\"><a href=\"mailto:").Append(email).Append("\">").Append(email).Append("</a></p>");
        if (contactBits.Length == 0)
            contactBits.Append("<p class=\"empty\">Keine Kontaktdaten hinterlegt.</p>");

        return $$"""
            <!DOCTYPE html>
            <html lang="de">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1" />
              <title>{{name}}</title>
              <meta name="description" content="{{description}}" />
              <style>{{css}}</style>
            </head>
            <body class="template-{{enc.Encode(templateId)}}" data-slug="{{enc.Encode(tenant.Slug)}}"
                  data-is-open="{{(tenant.RestaurantIsOpen ? "true" : "false")}}"
                  data-can-order="{{(tenant.AcceptingOnlineOrders ? "true" : "false")}}">
              <header class="hero">
                {{logo}}
                <h1>{{name}}</h1>
                <p class="tagline">{{description}}</p>
              </header>
              <main>
                {{statusHtml}}
                <section class="menu" aria-label="Speisekarte">
                  <h2>Speisekarte</h2>
                  {{menuHtml}}
                </section>
                <section class="contact" aria-label="Kontakt">
                  <h2>Kontakt</h2>
                  {{contactBits.ToString()}}
                </section>
              </main>
              <footer>
                <p>{{name}} · Regkasse</p>
              </footer>
            </body>
            </html>
            """;
    }

    /// <summary>Customer-facing open/order banner — website only, never POS.</summary>
    private static string BuildOrderStatusHtml(PublicTenantProfileDto tenant, HtmlEncoder enc)
    {
        var message = enc.Encode(
            string.IsNullOrWhiteSpace(tenant.OrderStatusMessage)
                ? (tenant.AcceptingOnlineOrders ? "Online-Bestellung möglich" : "Heute geschlossen")
                : tenant.OrderStatusMessage);

        if (tenant.AcceptingOnlineOrders)
        {
            return $"""
                <section class="order-status open" role="status" aria-label="Bestellstatus">
                  <strong>Geöffnet</strong>
                  <p>{message}</p>
                </section>
                """;
        }

        var title = tenant.RestaurantIsOpen ? "Bestellung nicht möglich" : "Heute geschlossen";
        return $"""
            <section class="order-status closed" role="status" aria-label="Bestellstatus">
              <strong>{enc.Encode(title)}</strong>
              <p>{message}</p>
              <p class="hint">Die Speisekarte können Sie weiterhin einsehen. Online-Bestellungen sind derzeit deaktiviert.</p>
            </section>
            """;
    }

    private static string BuildMenuHtml(PublicTenantMenuDto menu, HtmlEncoder enc)
    {
        if (menu.Items.Count == 0)
            return "<p class=\"empty\">Aktuell keine Speisen verfügbar.</p>";

        var byCategory = menu.Items
            .GroupBy(i => string.IsNullOrWhiteSpace(i.CategoryName) ? "Weitere" : i.CategoryName!.Trim())
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        var sb = new StringBuilder();
        foreach (var group in byCategory)
        {
            sb.Append("<div class=\"category\">");
            sb.Append("<h3>").Append(enc.Encode(group.Key)).Append("</h3>");
            sb.Append("<ul class=\"menu-list\">");
            foreach (var item in group)
            {
                var price = item.Price.ToString("0.00", CultureInfo.InvariantCulture);
                sb.Append("<li><div class=\"item-main\"><strong>")
                    .Append(enc.Encode(item.Name))
                    .Append("</strong>");
                if (!string.IsNullOrWhiteSpace(item.Description))
                {
                    sb.Append("<span class=\"item-desc\">")
                        .Append(enc.Encode(item.Description))
                        .Append("</span>");
                }
                sb.Append("</div><span class=\"price\">")
                    .Append(WebUtility.HtmlEncode(price))
                    .Append(" €</span></li>");
            }
            sb.Append("</ul></div>");
        }

        return sb.ToString();
    }

    private static string BuildCss(string templateId, string primary, string accent) =>
        templateId switch
        {
            "classic" => $$"""
                :root { --bg:#f7f1e8; --fg:#2c1810; --accent:#8b4513; --muted:#6b5344; }
                *{box-sizing:border-box} body{margin:0;font-family:Georgia,serif;background:var(--bg);color:var(--fg);line-height:1.6}
                .hero{padding:3rem 1.5rem;text-align:center;border-bottom:3px double var(--accent)}
                .logo{max-height:96px;margin-bottom:1rem} h1{font-size:2.2rem;margin:0 0 .5rem}
                .tagline{color:var(--muted);max-width:36rem;margin:0 auto}
                main{max-width:42rem;margin:0 auto;padding:2rem 1.5rem}
                h2,h3{color:var(--accent)} .menu-list{list-style:none;padding:0}
                .menu-list li{display:flex;justify-content:space-between;gap:1rem;padding:.45rem 0;border-bottom:1px solid #d4c4b0}
                .item-desc{display:block;font-size:.85rem;color:var(--muted);margin-top:.15rem}
                .price{font-weight:700;white-space:nowrap} footer{text-align:center;padding:2rem;color:var(--muted)}
                a{color:var(--accent)}
                .order-status{margin:0 0 1.25rem;padding:.85rem 1rem;border:1px solid #d4c4b0;border-radius:8px}
                .order-status.open{background:#eef6e8;border-color:#8fbc8f}
                .order-status.closed{background:#f3eee8}.order-status p{margin:.35rem 0 0}.order-status .hint{font-size:.85rem;color:var(--muted)}
                """,
            "minimal" => $$"""
                :root { --bg:#fff; --fg:#111; --accent:{{accent}}; --muted:#666; }
                *{box-sizing:border-box} body{margin:0;font-family:system-ui,sans-serif;background:var(--bg);color:var(--fg);line-height:1.5}
                .hero{padding:3.5rem 1.25rem 1.5rem;max-width:40rem;margin:0 auto}
                .logo{max-height:64px;margin-bottom:1.25rem} h1{font-size:2rem;margin:0 0 .75rem;letter-spacing:-.02em}
                .tagline{color:var(--muted);margin:0} main{max-width:40rem;margin:0 auto;padding:0 1.25rem 3rem}
                h2{font-size:.8rem;text-transform:uppercase;letter-spacing:.08em;color:var(--muted)}
                h3{font-size:1rem;margin:1.5rem 0 .5rem;color:var(--accent)}
                .menu-list{list-style:none;padding:0} .menu-list li{display:flex;justify-content:space-between;gap:1rem;padding:.5rem 0;border-bottom:1px solid #eee}
                .item-desc{display:block;font-size:.85rem;color:var(--muted)} .price{font-weight:600}
                footer{max-width:40rem;margin:0 auto;padding:0 1.25rem 3rem;color:var(--muted);font-size:.85rem}
                a{color:inherit}
                .order-status{margin:0 0 1.25rem;padding:.85rem 1rem;border:1px solid #eee;border-radius:8px}
                .order-status.open{background:#f0fdf4;border-color:#bbf7d0}
                .order-status.closed{background:#f8fafc}.order-status p{margin:.35rem 0 0}.order-status .hint{font-size:.85rem;color:var(--muted)}
                """,
            _ => $$"""
                :root { --bg:#0f172a; --fg:#f8fafc; --accent:{{accent}}; --muted:#94a3b8; --card:#1e293b; --primary:{{primary}}; }
                *{box-sizing:border-box} body{margin:0;font-family:Segoe UI,system-ui,sans-serif;background:radial-gradient(circle at top,#1e293b,var(--bg));color:var(--fg);line-height:1.6;min-height:100vh}
                .hero{padding:3.5rem 1.5rem 2rem;text-align:center}
                .logo{max-height:88px;margin-bottom:1rem;border-radius:12px}
                h1{font-size:clamp(2rem,4vw,2.75rem);margin:0 0 .75rem;color:var(--primary)}
                .tagline{color:var(--muted);max-width:34rem;margin:0 auto}
                main{max-width:42rem;margin:0 auto;padding:1rem 1.5rem 3rem;display:grid;gap:1.25rem}
                section{background:color-mix(in srgb,var(--card) 90%,transparent);border:1px solid #334155;border-radius:16px;padding:1.25rem 1.5rem}
                h2{margin-top:0;color:var(--accent);font-size:1.1rem} h3{color:var(--accent);font-size:1rem}
                .menu-list{list-style:none;padding:0;margin:0}
                .menu-list li{display:flex;justify-content:space-between;gap:1rem;padding:.5rem 0;border-bottom:1px solid #334155}
                .menu-list li:last-child{border-bottom:none}
                .item-desc{display:block;font-size:.85rem;color:var(--muted);margin-top:.15rem}
                .price{font-weight:700;white-space:nowrap} .empty{color:var(--muted)}
                footer{text-align:center;padding:2rem;color:var(--muted);font-size:.9rem}
                a{color:var(--accent)}
                .order-status.open{border-color:rgba(34,197,94,.45);background:rgba(34,197,94,.12)}
                .order-status.closed{border-color:#475569;background:rgba(148,163,184,.12)}
                .order-status p{margin:.35rem 0 0}.order-status .hint{font-size:.85rem;color:var(--muted)}
                """
        };
}
