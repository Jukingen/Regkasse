import React, { useMemo } from 'react';
import { View, Text, StyleSheet, ScrollView, TouchableOpacity, ActivityIndicator } from 'react-native';
import { useRouter } from 'expo-router';
import LanguageSelector from '../../components/LanguageSelector';
import { CashRegisterAssignmentSection } from '../../components/CashRegisterAssignmentSection';
import { LicenseStatusIndicator } from '../../components/LicenseStatusIndicator';
import { LicenseTransferHelpSection } from '../../components/LicenseTransferHelpSection';
import { AppUpdateChecker } from '../../components/AppUpdateChecker';
import { useAuth } from '../../contexts/AuthContext';
import { useTranslation } from 'react-i18next';
import { useTimeSyncStatus } from '../../hooks/useTimeSyncStatus';

function formatDeDateTime(iso: string | null | undefined): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '—';
  return d.toLocaleString('de-AT', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  });
}

export default function SettingsScreen() {
  const router = useRouter();
  const { t } = useTranslation(['settings', 'auth', 'common', 'license']);
  const { logout } = useAuth();
  const { status, loading, error, refetch } = useTimeSyncStatus();

  // CRITICAL FIX: Translation değerlerini useMemo ile cache'le
  const translations = useMemo(() => ({
    settings: t('settings:title'),
    otherSettings: t('settings:other_settings'),
    comingSoon: t('settings:coming_soon'),
    logout: t('auth:logout'),
    offlineQueueTitle: t('settings:offlineQueue.title'),
    offlineQueueDescription: t('settings:offlineQueue.description'),
    offlineQueueOpen: t('settings:offlineQueue.open'),
    licenseHeading: t('license:settingsSectionTitle'),
    licenseTransferHeading: t('license:transferSectionTitle'),
  }), [t]);

  return (
    <ScrollView style={styles.container}>
      <View style={styles.header}>
        <Text style={styles.title}>{translations.settings}</Text>
      </View>

      <View style={styles.section}>
        <LanguageSelector />
      </View>
      <View style={styles.section}>
        <CashRegisterAssignmentSection />
      </View>

      <View style={styles.section}>
        <Text style={styles.sectionTitle}>{translations.licenseHeading}</Text>
        <LicenseStatusIndicator badgeAlignSelf="stretch" expandedTouchTarget />
        <TouchableOpacity
          style={styles.licenseActivateLink}
          onPress={() => router.push('/(screens)/license-activate' as any)}
          accessibilityRole="button"
          accessibilityLabel={t('license:settingsOpenActivation')}
        >
          <Text style={styles.licenseActivateLinkText}>{t('license:settingsOpenActivation')}</Text>
        </TouchableOpacity>
      </View>

      <View style={styles.section}>
        <Text style={styles.sectionTitle}>{translations.licenseTransferHeading}</Text>
        <LicenseTransferHelpSection />
      </View>

      <View style={styles.section}>
        <Text style={styles.sectionTitle}>Systemzeit (NTP)</Text>
        <Text style={styles.description}>
          Letzter Abgleich (Server): {formatDeDateTime(status?.lastSyncAt)}
        </Text>
        {typeof status?.offsetSeconds === 'number' && Number.isFinite(status.offsetSeconds) ? (
          <Text style={styles.descriptionMuted}>
            Abweichung: {Math.round(status.offsetSeconds * 10) / 10} s ({status.warningLevel})
          </Text>
        ) : null}
        {loading ? (
          <ActivityIndicator style={{ marginTop: 8 }} color="#007AFF" />
        ) : error ? (
          <Text style={styles.syncError}>Status konnte nicht geladen werden.</Text>
        ) : null}
        <TouchableOpacity style={styles.queueLinkButton} onPress={() => void refetch()}>
          <Text style={styles.queueLinkText}>Zeitstatus aktualisieren</Text>
        </TouchableOpacity>
      </View>
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>{translations.offlineQueueTitle}</Text>
        <Text style={styles.description}>
          {translations.offlineQueueDescription}
        </Text>
        <TouchableOpacity
          style={styles.queueLinkButton}
          onPress={() => router.push('/(screens)/offline-queue' as any)}
        >
          <Text style={styles.queueLinkText}>{translations.offlineQueueOpen}</Text>
        </TouchableOpacity>
      </View>
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>{t('settings:appUpdate.title', { defaultValue: 'App-Aktualisierung' })}</Text>
        <AppUpdateChecker />
      </View>

      <View style={styles.section}>
        <Text style={styles.sectionTitle}>{translations.otherSettings}</Text>
        <Text style={styles.description}>
          {translations.comingSoon}
        </Text>
      </View>
      {/* Çıkış Yap Butonu */}
      <View style={styles.section}>
        <TouchableOpacity style={styles.logoutButton} onPress={logout}>
          <Text style={styles.logoutButtonText}>{translations.logout}</Text>
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
  descriptionMuted: {
    fontSize: 13,
    color: '#888',
    marginTop: 6,
    lineHeight: 18,
  },
  syncError: {
    fontSize: 14,
    color: '#c62828',
    marginTop: 8,
  },
  queueLinkButton: {
    backgroundColor: '#007AFF',
    paddingVertical: 12,
    paddingHorizontal: 16,
    borderRadius: 8,
    marginTop: 12,
    alignItems: 'center',
  },
  queueLinkText: {
    color: '#fff',
    fontSize: 16,
    fontWeight: '600',
  },
  licenseActivateLink: {
    marginTop: 12,
    paddingVertical: 10,
    alignItems: 'center',
  },
  licenseActivateLinkText: {
    color: '#007AFF',
    fontSize: 16,
    fontWeight: '600',
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