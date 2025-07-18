using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Registrierkasse_API.Models;
using Registrierkasse_API.Data;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace Registrierkasse_API.Services
{
    /// <summary>
    /// Çok dilli localization servisi - Teknik terimler İngilizce kalır
    /// </summary>
    public class LocalizationService
    {
        private readonly ILogger<LocalizationService> _logger;
        private readonly Dictionary<string, Dictionary<string, string>> _translations;

        public LocalizationService(ILogger<LocalizationService> logger)
        {
            _logger = logger;
            _translations = LoadTranslations();
        }

        /// <summary>
        /// Çeviri yapar - Teknik terimler İngilizce kalır
        /// </summary>
        public string Translate(string key, string language = "de-DE", Dictionary<string, string>? parameters = null)
        {
            try
            {
                // Teknik terimler her zaman İngilizce kalır
                if (IsTechnicalTerm(key))
                {
                    return key;
                }

                // Dil çevirisi
                var translation = GetTranslation(key, language);
                
                // Parametre değiştirme
                if (parameters != null)
                {
                    foreach (var param in parameters)
                    {
                        translation = translation.Replace($"{{{param.Key}}}", param.Value);
                    }
                }

                return translation;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Translation error for key: {key}, language: {language}");
                return key; // Hata durumunda orijinal key'i döndür
            }
        }

        /// <summary>
        /// Teknik terim kontrolü
        /// </summary>
        private bool IsTechnicalTerm(string key)
        {
            var technicalTerms = new[]
            {
                "transaction", "invoice", "receipt", "cancellation", "refund", "void",
                "settlement", "reconciliation", "audit", "compliance", "tse", "finanzonline",
                "rksv", "dsgvo", "api", "endpoint", "database", "migration", "middleware",
                "controller", "service", "repository", "entity", "model", "viewmodel"
            };

            return technicalTerms.Any(term => key.ToLower().Contains(term));
        }

        /// <summary>
        /// Çeviri alır
        /// </summary>
        private string GetTranslation(string key, string language)
        {
            if (_translations.ContainsKey(language) && _translations[language].ContainsKey(key))
            {
                return _translations[language][key];
            }

            // Fallback: Almanca
            if (language != "de-DE" && _translations.ContainsKey("de-DE") && _translations["de-DE"].ContainsKey(key))
            {
                _logger.LogWarning($"Translation not found for key '{key}' in language '{language}', using German fallback");
                return _translations["de-DE"][key];
            }

            return key; // Çeviri bulunamazsa orijinal key'i döndür
        }

        /// <summary>
        /// Çevirileri yükler
        /// </summary>
        private Dictionary<string, Dictionary<string, string>> LoadTranslations()
        {
            return new Dictionary<string, Dictionary<string, string>>
            {
                ["de-DE"] = new Dictionary<string, string>
                {
                    // Genel UI
                    ["common.save"] = "Speichern",
                    ["common.cancel"] = "Abbrechen",
                    ["common.delete"] = "Löschen",
                    ["common.edit"] = "Bearbeiten",
                    ["common.add"] = "Hinzufügen",
                    ["common.search"] = "Suchen",
                    ["common.loading"] = "Laden...",
                    ["common.error"] = "Fehler",
                    ["common.success"] = "Erfolgreich",
                    ["common.confirm"] = "Bestätigen",
                    ["common.back"] = "Zurück",
                    ["common.next"] = "Weiter",
                    ["common.close"] = "Schließen",
                    ["common.yes"] = "Ja",
                    ["common.no"] = "Nein",
                    ["common.ok"] = "OK",
                    
                    // Satış işlemleri
                    ["sales.new_sale"] = "Neuer Verkauf",
                    ["sales.add_item"] = "Artikel hinzufügen",
                    ["sales.remove_item"] = "Artikel entfernen",
                    ["sales.quantity"] = "Menge",
                    ["sales.price"] = "Preis",
                    ["sales.total"] = "Gesamt",
                    ["sales.subtotal"] = "Zwischensumme",
                    ["sales.tax"] = "Steuer",
                    ["sales.discount"] = "Rabatt",
                    ["sales.payment"] = "Zahlung",
                    ["sales.cash"] = "Bargeld",
                    ["sales.card"] = "Karte",
                    ["sales.complete"] = "Abschließen",
                    ["sales.receipt"] = "Beleg",
                    ["sales.invoice"] = "Rechnung",
                    ["sales.print"] = "Drucken",
                    ["sales.email"] = "E-Mail senden",
                    
                    // Hata mesajları
                    ["error.unauthorized"] = "Sie haben keine Berechtigung für diese Aktion.",
                    ["error.network"] = "Netzwerkfehler. Bitte versuchen Sie es erneut.",
                    ["error.server"] = "Serverfehler. Bitte kontaktieren Sie den Administrator.",
                    ["error.validation"] = "Eingabefehler. Bitte überprüfen Sie Ihre Daten.",
                    ["error.not_found"] = "Angeforderte Ressource wurde nicht gefunden.",
                    ["error.timeout"] = "Zeitüberschreitung. Bitte versuchen Sie es erneut.",
                    
                    // Başarı mesajları
                    ["success.sale_completed"] = "Verkauf erfolgreich abgeschlossen.",
                    ["success.receipt_printed"] = "Beleg erfolgreich gedruckt.",
                    ["success.data_saved"] = "Daten erfolgreich gespeichert.",
                    ["success.user_created"] = "Benutzer erfolgreich erstellt.",
                    
                    // Ayarlar
                    ["settings.language"] = "Sprache",
                    ["settings.german"] = "Deutsch",
                    ["settings.english"] = "Englisch",
                    ["settings.turkish"] = "Türkçe",
                    ["settings.theme"] = "Design",
                    ["settings.light"] = "Hell",
                    ["settings.dark"] = "Dunkel",
                    ["settings.auto"] = "Automatisch",
                },
                
                ["en"] = new Dictionary<string, string>
                {
                    // General UI
                    ["common.save"] = "Save",
                    ["common.cancel"] = "Cancel",
                    ["common.delete"] = "Delete",
                    ["common.edit"] = "Edit",
                    ["common.add"] = "Add",
                    ["common.search"] = "Search",
                    ["common.loading"] = "Loading...",
                    ["common.error"] = "Error",
                    ["common.success"] = "Success",
                    ["common.confirm"] = "Confirm",
                    ["common.back"] = "Back",
                    ["common.next"] = "Next",
                    ["common.close"] = "Close",
                    ["common.yes"] = "Yes",
                    ["common.no"] = "No",
                    ["common.ok"] = "OK",
                    
                    // Sales operations
                    ["sales.new_sale"] = "New Sale",
                    ["sales.add_item"] = "Add Item",
                    ["sales.remove_item"] = "Remove Item",
                    ["sales.quantity"] = "Quantity",
                    ["sales.price"] = "Price",
                    ["sales.total"] = "Total",
                    ["sales.subtotal"] = "Subtotal",
                    ["sales.tax"] = "Tax",
                    ["sales.discount"] = "Discount",
                    ["sales.payment"] = "Payment",
                    ["sales.cash"] = "Cash",
                    ["sales.card"] = "Card",
                    ["sales.complete"] = "Complete",
                    ["sales.receipt"] = "Receipt",
                    ["sales.invoice"] = "Invoice",
                    ["sales.print"] = "Print",
                    ["sales.email"] = "Send Email",
                    
                    // Error messages
                    ["error.unauthorized"] = "You are not authorized for this action.",
                    ["error.network"] = "Network error. Please try again.",
                    ["error.server"] = "Server error. Please contact administrator.",
                    ["error.validation"] = "Input error. Please check your data.",
                    ["error.not_found"] = "Requested resource not found.",
                    ["error.timeout"] = "Timeout. Please try again.",
                    
                    // Success messages
                    ["success.sale_completed"] = "Sale completed successfully.",
                    ["success.receipt_printed"] = "Receipt printed successfully.",
                    ["success.data_saved"] = "Data saved successfully.",
                    ["success.user_created"] = "User created successfully.",
                    
                    // Settings
                    ["settings.language"] = "Language",
                    ["settings.german"] = "Deutsch",
                    ["settings.english"] = "English",
                    ["settings.turkish"] = "Türkçe",
                    ["settings.theme"] = "Theme",
                    ["settings.light"] = "Light",
                    ["settings.dark"] = "Dark",
                    ["settings.auto"] = "Auto",
                },
                
                ["tr"] = new Dictionary<string, string>
                {
                    // Genel UI
                    ["common.save"] = "Kaydet",
                    ["common.cancel"] = "İptal",
                    ["common.delete"] = "Sil",
                    ["common.edit"] = "Düzenle",
                    ["common.add"] = "Ekle",
                    ["common.search"] = "Ara",
                    ["common.loading"] = "Yükleniyor...",
                    ["common.error"] = "Hata",
                    ["common.success"] = "Başarılı",
                    ["common.confirm"] = "Onayla",
                    ["common.back"] = "Geri",
                    ["common.next"] = "İleri",
                    ["common.close"] = "Kapat",
                    ["common.yes"] = "Evet",
                    ["common.no"] = "Hayır",
                    ["common.ok"] = "Tamam",
                    
                    // Satış işlemleri
                    ["sales.new_sale"] = "Yeni Satış",
                    ["sales.add_item"] = "Ürün Ekle",
                    ["sales.remove_item"] = "Ürün Çıkar",
                    ["sales.quantity"] = "Miktar",
                    ["sales.price"] = "Fiyat",
                    ["sales.total"] = "Toplam",
                    ["sales.subtotal"] = "Ara Toplam",
                    ["sales.tax"] = "Vergi",
                    ["sales.discount"] = "İndirim",
                    ["sales.payment"] = "Ödeme",
                    ["sales.cash"] = "Nakit",
                    ["sales.card"] = "Kart",
                    ["sales.complete"] = "Tamamla",
                    ["sales.receipt"] = "Fiş",
                    ["sales.invoice"] = "Fatura",
                    ["sales.print"] = "Yazdır",
                    ["sales.email"] = "E-posta Gönder",
                    
                    // Hata mesajları
                    ["error.unauthorized"] = "Bu işlem için yetkiniz yok.",
                    ["error.network"] = "Ağ hatası. Lütfen tekrar deneyin.",
                    ["error.server"] = "Sunucu hatası. Lütfen yönetici ile iletişime geçin.",
                    ["error.validation"] = "Giriş hatası. Lütfen verilerinizi kontrol edin.",
                    ["error.not_found"] = "İstenen kaynak bulunamadı.",
                    ["error.timeout"] = "Zaman aşımı. Lütfen tekrar deneyin.",
                    
                    // Başarı mesajları
                    ["success.sale_completed"] = "Satış başarıyla tamamlandı.",
                    ["success.receipt_printed"] = "Fiş başarıyla yazdırıldı.",
                    ["success.data_saved"] = "Veriler başarıyla kaydedildi.",
                    ["success.user_created"] = "Kullanıcı başarıyla oluşturuldu.",
                    
                    // Ayarlar
                    ["settings.language"] = "Dil",
                    ["settings.german"] = "Deutsch",
                    ["settings.english"] = "English",
                    ["settings.turkish"] = "Türkçe",
                    ["settings.theme"] = "Tema",
                    ["settings.light"] = "Açık",
                    ["settings.dark"] = "Koyu",
                    ["settings.auto"] = "Otomatik",
                }
            };
        }

        /// <summary>
        /// Desteklenen dilleri döndürür
        /// </summary>
        public List<string> GetSupportedLanguages()
        {
            return _translations.Keys.ToList();
        }

        /// <summary>
        /// Varsayılan dili döndürür
        /// </summary>
        public string GetDefaultLanguage()
        {
            return "de-DE";
        }

        /// <summary>
        /// Dil geçerli mi kontrol eder
        /// </summary>
        public bool IsValidLanguage(string language)
        {
            return _translations.ContainsKey(language);
        }
    }
} 
