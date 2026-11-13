using System.Runtime.CompilerServices;
using QuestPDF.Infrastructure;

namespace KasseAPI_Final.Tests;

/// <summary>
/// QuestPDF 2026.9+ ignores system fonts and stops when a family or glyph is missing.
/// Production PDFs still ask for Arial, Courier New, or Consolas, including a few symbols.
/// Tests keep that request and fall back to the bundled Lato face so a runner
/// without those fonts can still generate a document.
/// </summary>
internal static class QuestPdfTestFontFallback
{
    [ModuleInitializer]
    internal static void UseBundledFallbackFont()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.ThrowOnMissingFontFamilies = false;
        QuestPDF.Settings.ThrowOnMissingTextGlyphs = false;
    }
}
