using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Registrierkasse.Models;
using Registrierkasse.Data;

namespace Registrierkasse.Services
{
    public interface ILocalizationService
    {
        Task<string> GetTranslationAsync(string key, string language = "de-DE");
        Task<Dictionary<string, string>> GetAllTranslationsAsync(string language = "de-DE");
        Task<bool> SetUserLanguageAsync(string userId, string language);
    }

    public class LocalizationService : ILocalizationService
    {
        private readonly IMemoryCache _cache;
        private readonly AppDbContext _context;
        private const string CACHE_KEY_PREFIX = "translations_";
        private const int CACHE_DURATION_MINUTES = 60;

        public LocalizationService(IMemoryCache cache, AppDbContext context)
        {
            _cache = cache;
            _context = context;
        }

        public async Task<string> GetTranslationAsync(string key, string language = "de-DE")
        {
            var translations = await GetAllTranslationsAsync(language);
            return translations.TryGetValue(key, out var value) ? value : key;
        }

        public async Task<Dictionary<string, string>> GetAllTranslationsAsync(string language = "de-DE")
        {
            var cacheKey = $"{CACHE_KEY_PREFIX}{language}";
            
            if (_cache.TryGetValue(cacheKey, out Dictionary<string, string> translations))
            {
                return translations;
            }

            var resourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", $"{language}.json");
            
            if (!File.Exists(resourcePath))
            {
                // Fallback to German if requested language file doesn't exist
                resourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "de-DE.json");
            }

            var jsonContent = await File.ReadAllTextAsync(resourcePath);
            var jsonDoc = JsonDocument.Parse(jsonContent);
            
            translations = new Dictionary<string, string>();
            FlattenJson(jsonDoc.RootElement, "", translations);

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));

            _cache.Set(cacheKey, translations, cacheOptions);

            return translations;
        }

        public async Task<bool> SetUserLanguageAsync(string userId, string language)
        {
            var userSettings = await _context.UserSettings.FindAsync(userId);
            if (userSettings == null)
            {
                return false;
            }

            userSettings.Language = language;
            await _context.SaveChangesAsync();
            return true;
        }

        private void FlattenJson(JsonElement element, string prefix, Dictionary<string, string> result)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        var newPrefix = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";
                        FlattenJson(property.Value, newPrefix, result);
                    }
                    break;

                case JsonValueKind.String:
                    result[prefix] = element.GetString();
                    break;

                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    result[prefix] = element.ToString();
                    break;
            }
        }
    }
} 