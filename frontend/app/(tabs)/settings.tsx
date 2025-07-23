// Türkçe Açıklama: Ayarlar ekranına büyük ve kolay erişilebilen bir Logout (Çıkış Yap) butonu eklendi.
import React from 'react';
import { View, Text, StyleSheet, ScrollView, TouchableOpacity } from 'react-native';
import LanguageSelector from '../../components/LanguageSelector';
import { useAuth } from '../../contexts/AuthContext';
import { useTranslation } from 'react-i18next';

export default function SettingsScreen() {
  const { t } = useTranslation();
  const { logout } = useAuth();

  return (
    <ScrollView style={styles.container}>
      <View style={styles.header}>
        <Text style={styles.title}>{t('settings')}</Text>
      </View>
      <View style={styles.section}>
        <LanguageSelector />
      </View>
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>{t('settings.other_settings', 'Diğer Ayarlar')}</Text>
        <Text style={styles.description}>
          {t('settings.coming_soon', 'Çok yakında')}
        </Text>
      </View>
      {/* Çıkış Yap Butonu */}
      <View style={styles.section}>
        <TouchableOpacity style={styles.logoutButton} onPress={logout}>
          <Text style={styles.logoutButtonText}>{t('logout', 'Çıkış Yap')}</Text>
        </TouchableOpacity>
      </View>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f5f5f5',
  },
  header: {
    padding: 20,
    backgroundColor: '#fff',
    borderBottomWidth: 1,
    borderBottomColor: '#e0e0e0',
  },
  title: {
    fontSize: 24,
    fontWeight: 'bold',
    color: '#333',
  },
  section: {
    marginTop: 20,
    backgroundColor: '#fff',
    marginHorizontal: 20,
    borderRadius: 10,
    padding: 20,
    shadowColor: '#000',
    shadowOffset: {
      width: 0,
      height: 2,
    },
    shadowOpacity: 0.1,
    shadowRadius: 3.84,
    elevation: 5,
  },
  sectionTitle: {
    fontSize: 18,
    fontWeight: '600',
    color: '#333',
    marginBottom: 10,
  },
  description: {
    fontSize: 14,
    color: '#666',
    lineHeight: 20,
  },
  logoutButton: {
    backgroundColor: '#e74c3c',
    padding: 16,
    borderRadius: 10,
    alignItems: 'center',
    marginTop: 20,
  },
  logoutButtonText: {
    color: '#fff',
    fontSize: 20,
    fontWeight: 'bold',
  },
}); 