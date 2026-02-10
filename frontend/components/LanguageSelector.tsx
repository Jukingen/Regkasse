// Bu komponent, kullanıcıya Almanca, Türkçe veya İngilizce dil seçimi sunar. Erişilebilir ve dokunmatik uyumludur.
import React, { useCallback, useMemo } from 'react';
import { View, TouchableOpacity, Text, StyleSheet } from 'react-native';
import { useTranslation } from 'react-i18next';
// AsyncStorage import removed as it is handled in changeLanguage
import { changeLanguage } from '../i18n';

const LANGUAGES = [
  { code: 'de', label: 'Deutsch' },
  { code: 'en', label: 'English' },
  { code: 'tr', label: 'Türkçe' },
];

const LanguageSelector = () => {
  const { i18n } = useTranslation();

  // CRITICAL FIX: currentLang'i useMemo ile optimize et
  const currentLang = useMemo(() => i18n.language, [i18n.language]);

  // CRITICAL FIX: handleSelect fonksiyonunu useCallback ile optimize et
  const handleSelect = useCallback(async (code: string) => {
    // Eğer dil zaten seçiliyse, tekrar değiştirme
    if (currentLang === code) {
      return;
    }

    try {
      await changeLanguage(code);
    } catch (error) {
      console.error('Dil değiştirme hatası:', error);
    }
  }, [currentLang]);

  // CRITICAL FIX: LANGUAGES array'ini useMemo ile optimize et
  const languageButtons = useMemo(() =>
    LANGUAGES.map(lang => (
      <TouchableOpacity
        key={lang.code}
        style={[styles.button, currentLang === lang.code && styles.selected]}
        onPress={() => handleSelect(lang.code)}
        accessibilityRole="radio"
        accessibilityState={{ selected: currentLang === lang.code }}
        accessibilityLabel={lang.label}
        activeOpacity={0.85}
      >
        <Text style={[styles.text, currentLang === lang.code && styles.selectedText]}>{lang.label}</Text>
      </TouchableOpacity>
    )), [currentLang, handleSelect]);

  return (
    <View style={styles.row} accessibilityRole="radiogroup">
      {languageButtons}
    </View>
  );
};

const styles = StyleSheet.create({
  row: {
    flexDirection: 'row',
    justifyContent: 'center',
    marginVertical: 12,
  },
  button: {
    minWidth: 64,
    minHeight: 32,
    paddingHorizontal: 16,
    paddingVertical: 8,
    marginHorizontal: 6,
    borderRadius: 8,
    backgroundColor: '#e0e0e0',
    alignItems: 'center',
    justifyContent: 'center',
    borderWidth: 2,
    borderColor: '#1976d2',
  },
  selected: {
    backgroundColor: '#1976d2',
  },
  text: {
    color: '#222',
    fontWeight: 'bold',
    fontSize: 15,
  },
  selectedText: {
    color: '#fff',
  },
});

export default LanguageSelector; 