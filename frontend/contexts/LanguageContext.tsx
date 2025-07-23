import AsyncStorage from '@react-native-async-storage/async-storage';
import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';
import { updateUserLanguage } from '../services/api/settingsService';

// Desteklenen diller
export type SupportedLanguage = 'de-DE' | 'en' | 'tr';

// Dil context'i için tip tanımları
interface LanguageContextType {
  language: SupportedLanguage;
  setLanguage: (lang: SupportedLanguage) => Promise<void>;
  t: (key: string, params?: Record<string, string>) => string;
  isRTL: boolean;
}

// Varsayılan dil (Almanca)
const DEFAULT_LANGUAGE: SupportedLanguage = 'de-DE';

// Dil çevirileri - Teknik terimler İngilizce kalır
const translations = {
  'de-DE': {
    // Genel UI
    'common.save': 'Speichern',
    'common.cancel': 'Abbrechen',
    'common.delete': 'Löschen',
    'common.edit': 'Bearbeiten',
    'common.add': 'Hinzufügen',
    'common.search': 'Suchen',
    'common.loading': 'Laden...',
    'common.error': 'Fehler',
    'common.success': 'Erfolgreich',
    'common.confirm': 'Bestätigen',
    'common.back': 'Zurück',
    'common.next': 'Weiter',
    'common.previous': 'Zurück',
    'common.close': 'Schließen',
    'common.yes': 'Ja',
    'common.no': 'Nein',
    'common.ok': 'OK',
    
    // Navigation
    'nav.dashboard': 'Dashboard',
    'nav.sales': 'Verkauf',
    'nav.inventory': 'Lager',
    'nav.customers': 'Kunden',
    'nav.reports': 'Berichte',
    'nav.settings': 'Einstellungen',
    'nav.profile': 'Profil',
    'nav.logout': 'Abmelden',
    
    // Sales/Receipt
    'sales.new_sale': 'Neuer Verkauf',
    'sales.add_item': 'Artikel hinzufügen',
    'sales.remove_item': 'Artikel entfernen',
    'sales.quantity': 'Menge',
    'sales.price': 'Preis',
    'sales.total': 'Gesamt',
    'sales.subtotal': 'Zwischensumme',
    'sales.tax': 'Steuer',
    'sales.discount': 'Rabatt',
    'sales.payment': 'Zahlung',
    'sales.cash': 'Bargeld',
    'sales.card': 'Karte',
    'sales.complete': 'Abschließen',
    'sales.receipt': 'Beleg',
    'sales.invoice': 'Rechnung',
    'sales.print': 'Drucken',
    'sales.email': 'E-Mail senden',
    
    // Receipt Template (Avusturya yasal zorunlulukları)
    'receipt.title': 'KASSENBON',
    'receipt.date': 'Datum',
    'receipt.time': 'Uhrzeit',
    'receipt.receipt_number': 'Beleg-Nr.',
    'receipt.cash_register_id': 'Kassen-ID',
    'receipt.tse_signature': 'TSE-Signatur',
    'receipt.items': 'Artikel',
    'receipt.quantity': 'Menge',
    'receipt.unit_price': 'Einzelpreis',
    'receipt.total_price': 'Gesamtpreis',
    'receipt.subtotal': 'Zwischensumme',
    'receipt.tax_amount': 'Steuerbetrag',
    'receipt.total_amount': 'Gesamtbetrag',
    'receipt.payment_method': 'Zahlungsart',
    'receipt.thank_you': 'Vielen Dank für Ihren Einkauf!',
    'receipt.legal_notice': 'Dieser Beleg ist steuerrechtlich relevant.',
    'receipt.signature_required': 'Unterschrift erforderlich',
    'receipt.qr_code_info': 'QR-Code für digitale Überprüfung',
    
    // Error Messages
    'error.unauthorized': 'Sie haben keine Berechtigung für diese Aktion.',
    'error.network': 'Netzwerkfehler. Bitte versuchen Sie es erneut.',
    'error.server': 'Serverfehler. Bitte kontaktieren Sie den Administrator.',
    'error.validation': 'Eingabefehler. Bitte überprüfen Sie Ihre Daten.',
    'error.not_found': 'Angeforderte Ressource wurde nicht gefunden.',
    'error.timeout': 'Zeitüberschreitung. Bitte versuchen Sie es erneut.',
    
    // Success Messages
    'success.sale_completed': 'Verkauf erfolgreich abgeschlossen.',
    'success.receipt_printed': 'Beleg erfolgreich gedruckt.',
    'success.data_saved': 'Daten erfolgreich gespeichert.',
    'success.user_created': 'Benutzer erfolgreich erstellt.',
    
    // Settings
    'settings.language': 'Sprache',
    'settings.german': 'Deutsch',
    'settings.english': 'Englisch',
    'settings.turkish': 'Türkçe',
    'settings.theme': 'Design',
    'settings.light': 'Hell',
    'settings.dark': 'Dunkel',
    'settings.auto': 'Automatisch',
    
    // Technical terms (İngilizce kalır)
    'technical.transaction': 'Transaction',
    'technical.invoice': 'Invoice',
    'technical.receipt': 'Receipt',
    'technical.cancellation': 'Cancellation',
    'technical.refund': 'Refund',
    'technical.void': 'Void',
    'technical.settlement': 'Settlement',
    'technical.reconciliation': 'Reconciliation',
    'technical.audit': 'Audit',
    'technical.compliance': 'Compliance',
    'technical.tse': 'TSE',
    'technical.finanzonline': 'FinanzOnline',
    'technical.rksv': 'RKSV',
    'technical.dsgvo': 'DSGVO',
  },
  
  'en': {
    // General UI
    'common.save': 'Save',
    'common.cancel': 'Cancel',
    'common.delete': 'Delete',
    'common.edit': 'Edit',
    'common.add': 'Add',
    'common.search': 'Search',
    'common.loading': 'Loading...',
    'common.error': 'Error',
    'common.success': 'Success',
    'common.confirm': 'Confirm',
    'common.back': 'Back',
    'common.next': 'Next',
    'common.previous': 'Previous',
    'common.close': 'Close',
    'common.yes': 'Yes',
    'common.no': 'No',
    'common.ok': 'OK',
    
    // Navigation
    'nav.dashboard': 'Dashboard',
    'nav.sales': 'Sales',
    'nav.inventory': 'Inventory',
    'nav.customers': 'Customers',
    'nav.reports': 'Reports',
    'nav.settings': 'Settings',
    'nav.profile': 'Profile',
    'nav.logout': 'Logout',
    
    // Sales/Receipt
    'sales.new_sale': 'New Sale',
    'sales.add_item': 'Add Item',
    'sales.remove_item': 'Remove Item',
    'sales.quantity': 'Quantity',
    'sales.price': 'Price',
    'sales.total': 'Total',
    'sales.subtotal': 'Subtotal',
    'sales.tax': 'Tax',
    'sales.discount': 'Discount',
    'sales.payment': 'Payment',
    'sales.cash': 'Cash',
    'sales.card': 'Card',
    'sales.complete': 'Complete',
    'sales.receipt': 'Receipt',
    'sales.invoice': 'Invoice',
    'sales.print': 'Print',
    'sales.email': 'Send Email',
    
    // Receipt Template (Austrian legal requirements)
    'receipt.title': 'RECEIPT',
    'receipt.date': 'Date',
    'receipt.time': 'Time',
    'receipt.receipt_number': 'Receipt No.',
    'receipt.cash_register_id': 'Cash Register ID',
    'receipt.tse_signature': 'TSE Signature',
    'receipt.items': 'Items',
    'receipt.quantity': 'Qty',
    'receipt.unit_price': 'Unit Price',
    'receipt.total_price': 'Total Price',
    'receipt.subtotal': 'Subtotal',
    'receipt.tax_amount': 'Tax Amount',
    'receipt.total_amount': 'Total Amount',
    'receipt.payment_method': 'Payment Method',
    'receipt.thank_you': 'Thank you for your purchase!',
    'receipt.legal_notice': 'This receipt is tax-relevant.',
    'receipt.signature_required': 'Signature required',
    'receipt.qr_code_info': 'QR Code for digital verification',
    
    // Error Messages
    'error.unauthorized': 'You are not authorized for this action.',
    'error.network': 'Network error. Please try again.',
    'error.server': 'Server error. Please contact administrator.',
    'error.validation': 'Input error. Please check your data.',
    'error.not_found': 'Requested resource not found.',
    'error.timeout': 'Timeout. Please try again.',
    
    // Success Messages
    'success.sale_completed': 'Sale completed successfully.',
    'success.receipt_printed': 'Receipt printed successfully.',
    'success.data_saved': 'Data saved successfully.',
    'success.user_created': 'User created successfully.',
    
    // Settings
    'settings.language': 'Language',
    'settings.german': 'Deutsch',
    'settings.english': 'English',
    'settings.turkish': 'Türkçe',
    'settings.theme': 'Theme',
    'settings.light': 'Light',
    'settings.dark': 'Dark',
    'settings.auto': 'Auto',
    
    // Technical terms (English)
    'technical.transaction': 'Transaction',
    'technical.invoice': 'Invoice',
    'technical.receipt': 'Receipt',
    'technical.cancellation': 'Cancellation',
    'technical.refund': 'Refund',
    'technical.void': 'Void',
    'technical.settlement': 'Settlement',
    'technical.reconciliation': 'Reconciliation',
    'technical.audit': 'Audit',
    'technical.compliance': 'Compliance',
    'technical.tse': 'TSE',
    'technical.finanzonline': 'FinanzOnline',
    'technical.rksv': 'RKSV',
    'technical.dsgvo': 'DSGVO',
  },
  
  'tr': {
    // Genel UI
    'common.save': 'Kaydet',
    'common.cancel': 'İptal',
    'common.delete': 'Sil',
    'common.edit': 'Düzenle',
    'common.add': 'Ekle',
    'common.search': 'Ara',
    'common.loading': 'Yükleniyor...',
    'common.error': 'Hata',
    'common.success': 'Başarılı',
    'common.confirm': 'Onayla',
    'common.back': 'Geri',
    'common.next': 'İleri',
    'common.previous': 'Önceki',
    'common.close': 'Kapat',
    'common.yes': 'Evet',
    'common.no': 'Hayır',
    'common.ok': 'Tamam',
    
    // Navigation
    'nav.dashboard': 'Gösterge Paneli',
    'nav.sales': 'Satış',
    'nav.inventory': 'Stok',
    'nav.customers': 'Müşteriler',
    'nav.reports': 'Raporlar',
    'nav.settings': 'Ayarlar',
    'nav.profile': 'Profil',
    'nav.logout': 'Çıkış',
    
    // Sales/Receipt
    'sales.new_sale': 'Yeni Satış',
    'sales.add_item': 'Ürün Ekle',
    'sales.remove_item': 'Ürün Çıkar',
    'sales.quantity': 'Miktar',
    'sales.price': 'Fiyat',
    'sales.total': 'Toplam',
    'sales.subtotal': 'Ara Toplam',
    'sales.tax': 'Vergi',
    'sales.discount': 'İndirim',
    'sales.payment': 'Ödeme',
    'sales.cash': 'Nakit',
    'sales.card': 'Kart',
    'sales.complete': 'Tamamla',
    'sales.receipt': 'Fiş',
    'sales.invoice': 'Fatura',
    'sales.print': 'Yazdır',
    'sales.email': 'E-posta Gönder',
    
    // Receipt Template (Avusturya yasal zorunlulukları)
    'receipt.title': 'FİŞ',
    'receipt.date': 'Tarih',
    'receipt.time': 'Saat',
    'receipt.receipt_number': 'Fiş No.',
    'receipt.cash_register_id': 'Kasa ID',
    'receipt.tse_signature': 'TSE İmzası',
    'receipt.items': 'Ürünler',
    'receipt.quantity': 'Miktar',
    'receipt.unit_price': 'Birim Fiyat',
    'receipt.total_price': 'Toplam Fiyat',
    'receipt.subtotal': 'Ara Toplam',
    'receipt.tax_amount': 'Vergi Tutarı',
    'receipt.total_amount': 'Toplam Tutar',
    'receipt.payment_method': 'Ödeme Yöntemi',
    'receipt.thank_you': 'Alışverişiniz için teşekkürler!',
    'receipt.legal_notice': 'Bu fiş vergi açısından önemlidir.',
    'receipt.signature_required': 'İmza gerekli',
    'receipt.qr_code_info': 'Dijital doğrulama için QR kod',
    
    // Error Messages
    'error.unauthorized': 'Bu işlem için yetkiniz yok.',
    'error.network': 'Ağ hatası. Lütfen tekrar deneyin.',
    'error.server': 'Sunucu hatası. Lütfen yönetici ile iletişime geçin.',
    'error.validation': 'Giriş hatası. Lütfen verilerinizi kontrol edin.',
    'error.not_found': 'İstenen kaynak bulunamadı.',
    'error.timeout': 'Zaman aşımı. Lütfen tekrar deneyin.',
    
    // Success Messages
    'success.sale_completed': 'Satış başarıyla tamamlandı.',
    'success.receipt_printed': 'Fiş başarıyla yazdırıldı.',
    'success.data_saved': 'Veriler başarıyla kaydedildi.',
    'success.user_created': 'Kullanıcı başarıyla oluşturuldu.',
    
    // Settings
    'settings.language': 'Dil',
    'settings.german': 'Deutsch',
    'settings.english': 'English',
    'settings.turkish': 'Türkçe',
    'settings.theme': 'Tema',
    'settings.light': 'Açık',
    'settings.dark': 'Koyu',
    'settings.auto': 'Otomatik',
    
    // Technical terms (İngilizce kalır)
    'technical.transaction': 'Transaction',
    'technical.invoice': 'Invoice',
    'technical.receipt': 'Receipt',
    'technical.cancellation': 'Cancellation',
    'technical.refund': 'Refund',
    'technical.void': 'Void',
    'technical.settlement': 'Settlement',
    'technical.reconciliation': 'Reconciliation',
    'technical.audit': 'Audit',
    'technical.compliance': 'Compliance',
    'technical.tse': 'TSE',
    'technical.finanzonline': 'FinanzOnline',
    'technical.rksv': 'RKSV',
    'technical.dsgvo': 'DSGVO',
  }
};

// Context oluştur
const LanguageContext = createContext<LanguageContextType | undefined>(undefined);

// Local storage key
const LANGUAGE_STORAGE_KEY = '@registrierkasse_language';

// Dil context provider
export const LanguageProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const [language, setLanguageState] = useState<SupportedLanguage>(DEFAULT_LANGUAGE);
  const [isRTL, setIsRTL] = useState(false);

  // Dil değiştirme fonksiyonu
  const setLanguage = async (newLanguage: SupportedLanguage) => {
    try {
      setLanguageState(newLanguage);
      setIsRTL(newLanguage === 'ar-AR'); // (Varsa RTL dil kodu, yoksa false)
      await AsyncStorage.setItem(LANGUAGE_STORAGE_KEY, newLanguage);
      // Backend'e bildir
      try {
        await updateUserLanguage(newLanguage);
      } catch (err) {
        console.warn('Backend language update failed:', err);
      }
    } catch (error) {
      console.error('Language storage error:', error);
    }
  };

  // Çeviri fonksiyonu
  const t = (key: string, params?: Record<string, string>): string => {
    const langTranslations = translations[language as keyof typeof translations] || {};
    const translation = (langTranslations as any)[key] || key;
    if (params) {
      return Object.entries(params).reduce(
        (text, [param, value]) => text.replace(`{${param}}`, value),
        translation
      );
    }
    return translation;
  };

  // Local storage'dan dil tercihini yükle
  useEffect(() => {
    const loadLanguage = async () => {
      try {
        const savedLanguage = await AsyncStorage.getItem(LANGUAGE_STORAGE_KEY);
        if (savedLanguage && Object.keys(translations).includes(savedLanguage)) {
          setLanguageState(savedLanguage as SupportedLanguage);
        }
      } catch (error) {
        console.error('Error loading language preference:', error);
      }
    };

    loadLanguage();
  }, []);

  const value: LanguageContextType = {
    language,
    setLanguage,
    t,
    isRTL
  };

  return (
    <LanguageContext.Provider value={value}>
      {children}
    </LanguageContext.Provider>
  );
};

// Hook
export const useLanguage = (): LanguageContextType => {
  const context = useContext(LanguageContext);
  if (!context) {
    throw new Error('useLanguage must be used within a LanguageProvider');
  }
  return context;
}; 