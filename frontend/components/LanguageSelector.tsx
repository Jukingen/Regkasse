// Bu komponent, kullanıcıya Almanca, Türkçe veya İngilizce dil seçimi sunar. Erişilebilir ve dokunmatik uyumludur.
import React, { useCallback, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { View, TouchableOpacity, Text, StyleSheet } from 'react-native';

import { changeLanguage } from '../i18n';
import { normalizeTextLocale, toUserSettingsLanguage, type TextLocale } from '../i18n/localeUtils';
import { updateUserLanguage } from '../services/api/userSettingsService';

const LANGUAGES = [
  { code: 'de', key: 'settings:languageSelector.de' },
  { code: 'en', key: 'settings:languageSelector.en' },
  { code: 'tr', key: 'settings:languageSelector.tr' },
] as const;

const LanguageSelector = () => {
  const { i18n, t } = useTranslation(['settings']);

  const currentLang = useMemo(
    () => normalizeTextLocale(i18n.resolvedLanguage ?? i18n.language),
    [i18n.language, i18n.resolvedLanguage]
  );

  const handleSelect = useCallback(
    async (code: TextLocale) => {
      if (currentLang === code) {
        return;
      }

      try {
        await changeLanguage(code);
        // Persist to backend so the next login restores the same UI language.
        try {
          await updateUserLanguage(toUserSettingsLanguage(code));
        } catch (persistError) {
          console.warn('Language updated locally; backend sync failed:', persistError);
        }
      } catch (error) {
        console.error('Language change failed:', error);
        console.warn(t('settings:languageSelector.changeFailed'));
      }
    },
    [currentLang, t]
  );

  // CRITICAL FIX: LANGUAGES array'ini useMemo ile optimize et
  const languageButtons = useMemo(
    () =>
      LANGUAGES.map((lang) => (
        <TouchableOpacity
          key={lang.code}
          style={[styles.button, currentLang === lang.code && styles.selected]}
          onPress={() => handleSelect(lang.code)}
          accessibilityRole="radio"
          accessibilityState={{ selected: currentLang === lang.code }}
          accessibilityLabel={t(lang.key)}
          activeOpacity={0.85}>
          <Text style={[styles.text, currentLang === lang.code && styles.selectedText]}>
            {t(lang.key)}
          </Text>
        </TouchableOpacity>
      )),
    [currentLang, handleSelect, t]
  );

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
