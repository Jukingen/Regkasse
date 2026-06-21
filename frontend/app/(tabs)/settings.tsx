import { useRouter } from 'expo-router';
import React, { useCallback, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { ActivityIndicator, Alert, ScrollView, StyleSheet, Text, TouchableOpacity, View } from 'react-native';

import { usePosRegisterSelection } from '../../hooks/usePosRegisterSelection';

import { AppUpdateChecker } from '../../components/AppUpdateChecker';
import { CashRegisterSelector } from '../../components/CashRegisterSelector';
import { ShiftManager } from '../../components/ShiftManager';
import LanguageSelector from '../../components/LanguageSelector';
import { LicenseStatusIndicator } from '../../components/LicenseStatusIndicator';
import { LicenseTransferHelpSection } from '../../components/LicenseTransferHelpSection';
import { useAuth } from '../../contexts/AuthContext';
import { useTimeSyncStatus } from '../../hooks/useTimeSyncStatus';
import { formatUserDateTime } from '../../utils/dateFormatter';

function formatDeDateTime(iso: string | null | undefined): string {
  if (!iso) return '—';
  const formatted = formatUserDateTime(iso, { includeSeconds: true });
  return formatted || '—';
}

export default function SettingsScreen() {
  const router = useRouter();
  const { t } = useTranslation(['settings', 'auth', 'common', 'license']);
  const { logout } = useAuth();
  const { status, loading, error, refetch } = useTimeSyncStatus();
  const { effectiveRegisterId } = usePosRegisterSelection();

  const openPaymentHistory = useCallback(() => {
    if (!effectiveRegisterId) {
      Alert.alert(
        t('settings:paymentHistory.noRegisterTitle'),
        t('settings:paymentHistory.noRegisterMessage')
      );
      return;
    }
    router.push('/(screens)/PaymentHistoryScreen' as const);
  }, [effectiveRegisterId, router, t]);

  // CRITICAL FIX: Translation değerlerini useMemo ile cache'le
  const translations = useMemo(() => ({
    settings: t('settings:title'),
    otherSettings: t('settings:other_settings'),
    comingSoon: t('settings:coming_soon'),
    logout: t('auth:logout'),
    offlineQueueTitle: t('settings:offlineQueue.title'),
    offlineQueueDescription: t('settings:offlineQueue.description'),
    offlineQueueOpen: t('settings:offlineQueue.open'),
    paymentHistoryTitle: t('settings:paymentHistory.title'),
    paymentHistoryDescription: t('settings:paymentHistory.description'),
    paymentHistoryOpen: t('settings:paymentHistory.open'),
    licenseHeading: t('license:settingsSectionTitle'),
    licenseTransferHeading: t('license:transferSectionTitle'),
    adminMenuTitle: t('settings:adminMenu.title'),
    adminMenuDescription: t('settings:adminMenu.description'),
    adminMenuOpen: t('settings:adminMenu.open'),
    ntpTitle: t('settings:ntp.title'),
    ntpLastSync: t('settings:ntp.lastSync', { time: formatDeDateTime(status?.lastSyncAt) }),
    ntpOffset:
      typeof status?.offsetSeconds === 'number' && Number.isFinite(status.offsetSeconds)
        ? t('settings:ntp.offset', {
            seconds: Math.round(status.offsetSeconds * 10) / 10,
            level: status.warningLevel ?? '—',
          })
        : null,
    ntpLoadError: t('settings:ntp.loadError'),
    ntpRefresh: t('settings:ntp.refresh'),
  }), [t, status?.lastSyncAt, status?.offsetSeconds, status?.warningLevel]);

  return (
    <ScrollView style={styles.container}>
      <View style={styles.header}>
        <Text style={styles.title}>{translations.settings}</Text>
      </View>

      <View style={styles.section}>
        <LanguageSelector />
      </View>
      <View style={styles.section}>
        <CashRegisterSelector />
      </View>
      <View style={styles.section}>
        <ShiftManager />
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
        <Text style={styles.sectionTitle}>{translations.adminMenuTitle}</Text>
        <Text style={styles.description}>{translations.adminMenuDescription}</Text>
        <TouchableOpacity
          style={styles.queueLinkButton}
          onPress={() => router.push('/(screens)/admin-menu' as any)}
        >
          <Text style={styles.queueLinkText}>{translations.adminMenuOpen}</Text>
        </TouchableOpacity>
      </View>

      <View style={styles.section}>
        <Text style={styles.sectionTitle}>{translations.ntpTitle}</Text>
        <Text style={styles.description}>{translations.ntpLastSync}</Text>
        {translations.ntpOffset ? (
          <Text style={styles.descriptionMuted}>{translations.ntpOffset}</Text>
        ) : null}
        {loading ? (
          <ActivityIndicator style={{ marginTop: 8 }} color="#007AFF" />
        ) : error ? (
          <Text style={styles.syncError}>{translations.ntpLoadError}</Text>
        ) : null}
        <TouchableOpacity
          style={styles.queueLinkButton}
          onPress={() => {
            refetch().catch(() => undefined);
          }}
        >
          <Text style={styles.queueLinkText}>{translations.ntpRefresh}</Text>
        </TouchableOpacity>
      </View>
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>{translations.paymentHistoryTitle}</Text>
        <Text style={styles.description}>
          {translations.paymentHistoryDescription}
        </Text>
        <TouchableOpacity
          style={styles.queueLinkButton}
          onPress={openPaymentHistory}
        >
          <Text style={styles.queueLinkText}>{translations.paymentHistoryOpen}</Text>
        </TouchableOpacity>
        {!effectiveRegisterId ? (
          <Text style={styles.descriptionMuted}>{t('settings:paymentHistory.noRegisterHint')}</Text>
        ) : null}
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