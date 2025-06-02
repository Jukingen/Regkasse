import React, { useState, useEffect } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  Switch,
  Alert,
  ActivityIndicator,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { useRouter } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { useColorScheme } from 'react-native';
import { useFetch } from '../../hooks/useFetch';
import { API_BASE_URL } from '../../config';
import { changeLanguage } from '../../i18n/config';

interface UserSettings {
  language: string;
  theme: string;
  showTutorials: boolean;
  emailNotifications: boolean;
  pushNotifications: boolean;
  defaultRegister: string;
  defaultPrinter: string;
  defaultPaymentMethod: string;
}

export default function SettingsScreen() {
  const { t, i18n } = useTranslation();
  const colorScheme = useColorScheme();
  const router = useRouter();
  const [notifications, setNotifications] = useState(true);
  const [darkMode, setDarkMode] = useState(colorScheme === 'dark');
  const [currentLanguage, setCurrentLanguage] = useState(i18n.language);

  // Ayarları getir
  const { 
    data: settings, 
    error: settingsError, 
    loading: isLoading,
    refetch: refetchSettings 
  } = useFetch<UserSettings>(`${API_BASE_URL}/settings/user`);

  useEffect(() => {
    if (settingsError) {
      console.error('Ayarlar yüklenirken hata:', settingsError);
      Alert.alert('Hata', 'Ayarlar yüklenirken bir hata oluştu');
    }
  }, [settingsError]);

  const updateSetting = async (key: keyof UserSettings, value: any) => {
    try {
      const token = await AsyncStorage.getItem('token');
      const response = await fetch(`${API_BASE_URL}/settings/user`, {
        method: 'PATCH',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${token}`
        },
        body: JSON.stringify({ [key]: value })
      });

      if (!response.ok) {
        throw new Error('Ayar güncellenirken bir hata oluştu');
      }

      // Ayarları yeniden yükle
      refetchSettings();
    } catch (error) {
      console.error('Ayar güncellenirken hata:', error);
      Alert.alert('Hata', 'Ayar güncellenirken bir hata oluştu');
    }
  };

  const handleLogout = async () => {
    try {
      await AsyncStorage.multiRemove(['token', 'refreshToken', 'user']);
      router.replace('/(auth)/login');
    } catch (error) {
      console.error('Çıkış yapılırken hata:', error);
      Alert.alert('Hata', 'Çıkış yapılırken bir hata oluştu');
    }
  };

  const handleLanguageChange = async (language: 'de' | 'tr' | 'en') => {
    try {
      await changeLanguage(language);
      setCurrentLanguage(language);
      await updateSetting('language', language);
    } catch (error) {
      Alert.alert(t('errors.languageChangeFailed'));
    }
  };

  const handleThemeChange = async (value: boolean) => {
    try {
      setDarkMode(value);
      await updateSetting('theme', value ? 'dark' : 'light');
    } catch (error) {
      Alert.alert(t('errors.themeChangeFailed'));
    }
  };

  const handleNotificationsChange = async (value: boolean) => {
    try {
      setNotifications(value);
      await updateSetting('pushNotifications', value);
    } catch (error) {
      Alert.alert(t('errors.notificationChangeFailed'));
    }
  };

  const SettingItem = ({
    icon,
    title,
    value,
    onPress,
    type = 'toggle',
  }: {
    icon: string;
    title: string;
    value: any;
    onPress: (value: any) => void;
    type?: 'toggle' | 'button';
  }) => (
    <TouchableOpacity
      style={styles.settingItem}
      onPress={() => type === 'button' && onPress(null)}
    >
      <View style={styles.settingLeft}>
        <Ionicons name={icon as any} size={24} color="#007AFF" />
        <Text style={styles.settingTitle}>{title}</Text>
      </View>
      {type === 'toggle' ? (
        <Switch
          value={value}
          onValueChange={onPress}
          trackColor={{ false: '#767577', true: '#81b0ff' }}
          thumbColor={value ? '#007AFF' : '#f4f3f4'}
        />
      ) : (
        <Ionicons name="chevron-forward" size={24} color="#666" />
      )}
    </TouchableOpacity>
  );

  if (isLoading) {
    return (
      <View style={[styles.container, styles.centered]}>
        <ActivityIndicator size="large" color="#007AFF" />
      </View>
    );
  }

  return (
    <ScrollView style={[styles.container, { backgroundColor: darkMode ? '#000' : '#fff' }]}>
      <View style={styles.section}>
        <Text style={[styles.sectionTitle, { color: darkMode ? '#fff' : '#000' }]}>
          {t('settings.language')}
        </Text>
        <View style={styles.languageButtons}>
          <TouchableOpacity
            style={[
              styles.languageButton,
              currentLanguage === 'de' && styles.activeLanguageButton
            ]}
            onPress={() => handleLanguageChange('de')}
          >
            <Text style={styles.languageButtonText}>Deutsch</Text>
          </TouchableOpacity>
          <TouchableOpacity
            style={[
              styles.languageButton,
              currentLanguage === 'tr' && styles.activeLanguageButton
            ]}
            onPress={() => handleLanguageChange('tr')}
          >
            <Text style={styles.languageButtonText}>Türkçe</Text>
          </TouchableOpacity>
          <TouchableOpacity
            style={[
              styles.languageButton,
              currentLanguage === 'en' && styles.activeLanguageButton
            ]}
            onPress={() => handleLanguageChange('en')}
          >
            <Text style={styles.languageButtonText}>English</Text>
          </TouchableOpacity>
        </View>
      </View>

      <View style={styles.section}>
        <Text style={[styles.sectionTitle, { color: darkMode ? '#fff' : '#000' }]}>
          {t('settings.theme')}
        </Text>
        <View style={styles.settingRow}>
          <Text style={[styles.settingLabel, { color: darkMode ? '#fff' : '#000' }]}>
            {t('settings.darkMode')}
          </Text>
          <Switch
            value={darkMode}
            onValueChange={handleThemeChange}
            trackColor={{ false: '#767577', true: '#81b0ff' }}
            thumbColor={darkMode ? '#f5dd4b' : '#f4f3f4'}
          />
        </View>
      </View>

      <View style={styles.section}>
        <Text style={[styles.sectionTitle, { color: darkMode ? '#fff' : '#000' }]}>
          {t('settings.notifications')}
        </Text>
        <View style={styles.settingRow}>
          <Text style={[styles.settingLabel, { color: darkMode ? '#fff' : '#000' }]}>
            {t('settings.enableNotifications')}
          </Text>
          <Switch
            value={notifications}
            onValueChange={handleNotificationsChange}
            trackColor={{ false: '#767577', true: '#81b0ff' }}
            thumbColor={notifications ? '#f5dd4b' : '#f4f3f4'}
          />
        </View>
      </View>

      <View style={styles.section}>
        <Text style={styles.sectionTitle}>Genel</Text>
        <SettingItem
          icon="notifications-outline"
          title="Bildirimler"
          value={settings?.pushNotifications}
          onPress={value => updateSetting('pushNotifications', value)}
        />
        <SettingItem
          icon="mail-outline"
          title="E-posta Bildirimleri"
          value={settings?.emailNotifications}
          onPress={value => updateSetting('emailNotifications', value)}
        />
        <SettingItem
          icon="book-outline"
          title="Öğretici Göster"
          value={settings?.showTutorials}
          onPress={value => updateSetting('showTutorials', value)}
        />
      </View>

      <View style={styles.section}>
        <Text style={styles.sectionTitle}>Varsayılan Ayarlar</Text>
        <SettingItem
          icon="cash-outline"
          title="Varsayılan Kasa"
          value={settings?.defaultRegister}
          onPress={() => {}}
          type="button"
        />
        <SettingItem
          icon="print-outline"
          title="Varsayılan Yazıcı"
          value={settings?.defaultPrinter}
          onPress={() => {}}
          type="button"
        />
        <SettingItem
          icon="card-outline"
          title="Varsayılan Ödeme Yöntemi"
          value={settings?.defaultPaymentMethod}
          onPress={() => {}}
          type="button"
        />
      </View>

      <View style={styles.section}>
        <Text style={styles.sectionTitle}>Hesap</Text>
        <SettingItem
          icon="person-outline"
          title="Profil"
          value=""
          onPress={() => {}}
          type="button"
        />
        <SettingItem
          icon="key-outline"
          title="Şifre Değiştir"
          value=""
          onPress={() => {}}
          type="button"
        />
        <TouchableOpacity style={styles.logoutButton} onPress={handleLogout}>
          <Text style={styles.logoutText}>Çıkış Yap</Text>
        </TouchableOpacity>
      </View>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    padding: 16,
  },
  section: {
    marginBottom: 24,
  },
  sectionTitle: {
    fontSize: 18,
    fontWeight: 'bold',
    marginBottom: 16,
  },
  settingRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: 8,
  },
  settingLabel: {
    fontSize: 16,
  },
  languageButtons: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginTop: 8,
  },
  languageButton: {
    flex: 1,
    padding: 12,
    marginHorizontal: 4,
    borderRadius: 8,
    backgroundColor: '#e0e0e0',
    alignItems: 'center',
  },
  activeLanguageButton: {
    backgroundColor: '#81b0ff',
  },
  languageButtonText: {
    fontSize: 14,
    fontWeight: '500',
    color: '#000',
  },
  settingItem: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingVertical: 12,
    paddingHorizontal: 15,
    borderBottomWidth: 1,
    borderBottomColor: '#f0f0f0',
  },
  settingLeft: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  settingTitle: {
    fontSize: 16,
    marginLeft: 10,
  },
  logoutButton: {
    marginTop: 20,
    marginHorizontal: 15,
    backgroundColor: '#FF3B30',
    paddingVertical: 12,
    borderRadius: 8,
    alignItems: 'center',
  },
  logoutText: {
    color: 'white',
    fontSize: 16,
    fontWeight: 'bold',
  },
  centered: {
    justifyContent: 'center',
    alignItems: 'center',
  },
}); 