import React from 'react';
import { View, Text, TouchableOpacity, StyleSheet, Alert } from 'react-native';

import { useLanguage } from '../contexts/LanguageContext';

/**
 * Dil seçimi bileşeni - Almanca, İngilizce ve Türkçe arasında geçiş
 */
export const LanguageSelector: React.FC = () => {
  const { language, setLanguage, t } = useLanguage();

  // Dil değiştirme fonksiyonu
  const handleLanguageChange = async (newLanguage: 'de-DE' | 'en' | 'tr') => {
    try {
      await setLanguage(newLanguage);
      
      // Başarı mesajı göster
      Alert.alert(
        t('common.success'),
        t('settings.language_changed', { language: t(`settings.${newLanguage === 'de-DE' ? 'german' : newLanguage === 'en' ? 'english' : 'turkish'}`) }),
        [{ text: t('common.ok') }]
      );
    } catch (error) {
      console.error('Language change error:', error);
      Alert.alert(
        t('common.error'),
        t('settings.language_change_error'),
        [{ text: t('common.ok') }]
      );
    }
  };

  // Dil butonları
  const LanguageButton: React.FC<{
    lang: 'de-DE' | 'en' | 'tr';
    label: string;
    nativeLabel: string;
  }> = ({ lang, label, nativeLabel }) => (
    <TouchableOpacity
      style={[
        styles.languageButton,
        language === lang && styles.activeLanguageButton
      ]}
      onPress={() => handleLanguageChange(lang)}
      activeOpacity={0.7}
    >
      <Text style={[
        styles.languageText,
        language === lang && styles.activeLanguageText
      ]}>
        {label}
      </Text>
      <Text style={[
        styles.nativeLabel,
        language === lang && styles.activeNativeLabel
      ]}>
        {nativeLabel}
      </Text>
    </TouchableOpacity>
  );

  return (
    <View style={styles.container}>
      <Text style={styles.title}>{t('settings.language')}</Text>
      
      <View style={styles.languageGrid}>
        <LanguageButton
          lang="de-DE"
          label="Deutsch"
          nativeLabel="German"
        />
        
        <LanguageButton
          lang="en"
          label="English"
          nativeLabel="English"
        />
        
        <LanguageButton
          lang="tr"
          label="Türkçe"
          nativeLabel="Turkish"
        />
      </View>
      
      <Text style={styles.info}>
        {t('settings.language_info')}
      </Text>
    </View>
  );
};

// Stil tanımları
const styles = StyleSheet.create({
  container: {
    padding: 20,
    backgroundColor: '#fff',
  },
  
  title: {
    fontSize: 18,
    fontWeight: 'bold',
    marginBottom: 20,
    color: '#333',
  },
  
  languageGrid: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: 20,
  },
  
  languageButton: {
    flex: 1,
    marginHorizontal: 5,
    padding: 15,
    borderRadius: 10,
    borderWidth: 2,
    borderColor: '#e0e0e0',
    backgroundColor: '#f8f9fa',
    alignItems: 'center',
    justifyContent: 'center',
    minHeight: 80,
  },
  
  activeLanguageButton: {
    borderColor: '#007AFF',
    backgroundColor: '#E3F2FD',
  },
  
  languageText: {
    fontSize: 16,
    fontWeight: '600',
    color: '#333',
    marginBottom: 4,
  },
  
  activeLanguageText: {
    color: '#007AFF',
  },
  
  nativeLabel: {
    fontSize: 12,
    color: '#666',
  },
  
  activeNativeLabel: {
    color: '#007AFF',
  },
  
  info: {
    fontSize: 14,
    color: '#666',
    textAlign: 'center',
    fontStyle: 'italic',
  },
});

export default LanguageSelector; 