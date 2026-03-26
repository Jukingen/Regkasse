/**
 * LanguageSwitcher - Dil değiştirme komponenti
 * 
 * Bu component, görev önerilerinin dilini değiştirmek için kullanılır.
 * Task-Master AI sistemindeki çok dilli önerileri test etmek için tasarlanmıştır.
 * 
 * @author Frontend Team
 * @version 1.0.0
 * @since 2025-01-10
 */

import React from 'react';
import {
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  Alert
} from 'react-native';
import { useTranslation } from 'react-i18next';
import { Ionicons } from '@expo/vector-icons';
import { changeLanguage as persistAndChangeLanguage } from '../i18n';
import { normalizeTextLocale } from '../i18n/localeUtils';

interface LanguageSwitcherProps {
  onLanguageChange?: (language: string) => void;
}

const LanguageSwitcher: React.FC<LanguageSwitcherProps> = ({ 
  onLanguageChange 
}) => {
  const { i18n } = useTranslation();
  const activeLanguage = normalizeTextLocale(i18n.resolvedLanguage ?? i18n.language);

  const languages = [
    {
      code: 'tr',
      name: 'Türkçe',
      flag: '🇹🇷',
      description: 'Türkçe görev önerileri'
    },
    {
      code: 'de',
      name: 'Deutsch',
      flag: '🇩🇪',
      description: 'Deutsche Aufgabenvorschläge'
    },
    {
      code: 'en',
      name: 'English',
      flag: '🇺🇸',
      description: 'English task suggestions'
    }
  ];

  /**
   * Dil değiştir
   */
  const changeLanguage = async (languageCode: string) => {
    try {
      await persistAndChangeLanguage(languageCode);
      
      const selectedLanguage = languages.find(lang => lang.code === languageCode);
      
      Alert.alert(
        'Dil Değiştirildi / Language Changed',
        `Görev önerileri artık ${selectedLanguage?.name} dilinde gelecek.\n\nTask suggestions will now be in ${selectedLanguage?.name}.`,
        [{ text: 'OK' }]
      );

      // Callback'i çağır
      if (onLanguageChange) {
        onLanguageChange(languageCode);
      }

      console.log(`🌍 Language changed to: ${languageCode} (${selectedLanguage?.name})`);
      
    } catch (error) {
      console.error('Language change failed:', error);
      Alert.alert('Hata / Error', 'Dil değiştirilemedi / Language could not be changed');
    }
  };

  /**
   * Mevcut dili test et
   */
  const testCurrentLanguage = () => {
    const currentLang = languages.find(lang => lang.code === activeLanguage);
    
    Alert.alert(
      'Mevcut Dil / Current Language',
      `Aktif dil: ${currentLang?.name || 'Unknown'} (${activeLanguage})\n\nBu dilde görev önerileri alacaksınız.\n\nYou will receive task suggestions in this language.`,
      [{ text: 'OK' }]
    );
  };

  return (
    <View style={styles.container}>
      <View style={styles.header}>
        <Text style={styles.title}>Görev Dili Seçin / Select Task Language</Text>
        <Text style={styles.subtitle}>
          Mevcut: {languages.find(lang => lang.code === activeLanguage)?.name || 'Unknown'}
        </Text>
      </View>

      <View style={styles.languageList}>
        {languages.map((language) => (
          <TouchableOpacity
            key={language.code}
            style={[
              styles.languageButton,
              activeLanguage === language.code && styles.activeLanguage
            ]}
            onPress={() => changeLanguage(language.code)}
          >
            <Text style={styles.languageFlag}>{language.flag}</Text>
            <View style={styles.languageInfo}>
              <Text style={[
                styles.languageName,
                activeLanguage === language.code && styles.activeLanguageText
              ]}>
                {language.name}
              </Text>
              <Text style={[
                styles.languageDescription,
                activeLanguage === language.code && styles.activeLanguageDescription
              ]}>
                {language.description}
              </Text>
            </View>
            {activeLanguage === language.code && (
              <Ionicons name="checkmark-circle" size={24} color="#4CAF50" />
            )}
          </TouchableOpacity>
        ))}
      </View>

      <TouchableOpacity style={styles.testButton} onPress={testCurrentLanguage}>
        <Ionicons name="information-circle-outline" size={20} color="#2196F3" />
        <Text style={styles.testButtonText}>Mevcut Dili Test Et</Text>
      </TouchableOpacity>

      <View style={styles.infoCard}>
        <Ionicons name="lightbulb-outline" size={20} color="#FF9800" />
        <Text style={styles.infoText}>
          <Text style={styles.infoBold}>İpucu:</Text> Dil değiştirdikten sonra görev önerilerini yeniden alın. AI önerileri seçilen dilde gelecektir.
        </Text>
      </View>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    backgroundColor: 'white',
    borderRadius: 10,
    padding: 15,
    margin: 10,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  header: {
    alignItems: 'center',
    marginBottom: 20,
  },
  title: {
    fontSize: 18,
    fontWeight: 'bold',
    color: '#333',
    marginBottom: 5,
    textAlign: 'center',
  },
  subtitle: {
    fontSize: 14,
    color: '#666',
    textAlign: 'center',
  },
  languageList: {
    marginBottom: 20,
  },
  languageButton: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#f8f9fa',
    padding: 15,
    borderRadius: 10,
    marginBottom: 10,
    borderWidth: 2,
    borderColor: 'transparent',
  },
  activeLanguage: {
    backgroundColor: '#e8f5e8',
    borderColor: '#4CAF50',
  },
  languageFlag: {
    fontSize: 24,
    marginRight: 15,
  },
  languageInfo: {
    flex: 1,
  },
  languageName: {
    fontSize: 16,
    fontWeight: '600',
    color: '#333',
    marginBottom: 2,
  },
  activeLanguageText: {
    color: '#2e7d32',
  },
  languageDescription: {
    fontSize: 12,
    color: '#666',
    lineHeight: 16,
  },
  activeLanguageDescription: {
    color: '#2e7d32',
  },
  testButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: '#e3f2fd',
    padding: 12,
    borderRadius: 8,
    marginBottom: 15,
  },
  testButtonText: {
    color: '#2196F3',
    fontWeight: '600',
    marginLeft: 8,
  },
  infoCard: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    backgroundColor: '#fff3e0',
    padding: 12,
    borderRadius: 8,
    borderLeftWidth: 4,
    borderLeftColor: '#FF9800',
  },
  infoText: {
    flex: 1,
    marginLeft: 10,
    fontSize: 13,
    color: '#e65100',
    lineHeight: 18,
  },
  infoBold: {
    fontWeight: 'bold',
  },
});

export default LanguageSwitcher;
