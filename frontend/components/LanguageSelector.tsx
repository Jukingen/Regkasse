// Bu komponent, kullanıcıya Almanca, Türkçe veya İngilizce dil seçimi sunar. Erişilebilir ve dokunmatik uyumludur.
import React from 'react';
import { View, TouchableOpacity, Text, StyleSheet } from 'react-native';
import { useTranslation } from 'react-i18next';
import AsyncStorage from '@react-native-async-storage/async-storage';

const LANGUAGES = [
  { code: 'de', label: 'Deutsch' },
  { code: 'en', label: 'English' },
  { code: 'tr', label: 'Türkçe' },
];

const LanguageSelector = () => {
  const { i18n } = useTranslation();
  const currentLang = i18n.language;

  const handleSelect = async (code: string) => {
    await i18n.changeLanguage(code);
    await AsyncStorage.setItem('userLanguage', code);
  };

  return (
    <View style={styles.row} accessibilityRole="radiogroup">
      {LANGUAGES.map(lang => (
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
      ))}
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