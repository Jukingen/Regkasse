/**
 * Customer portal: loyalty + online-order history (public API).
 * Expo route: /customer/dashboard — React Native UI (not Ant Design).
 * POS UI language: German (de-DE).
 */

import React, { useCallback, useEffect, useState } from 'react';
import {
  ActivityIndicator,
  Alert,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from 'react-native';
import { useTranslation } from 'react-i18next';
import { useCustomerDashboard } from '../../hooks/useCustomerDashboard';
import { getEnvDevTenantSlug } from '../../services/tenant/devTenant';

function money(value: number, currency = 'EUR'): string {
  try {
    return new Intl.NumberFormat('de-AT', { style: 'currency', currency }).format(value);
  } catch {
    return `€ ${value.toFixed(2)}`;
  }
}

function formatDate(iso: string): string {
  try {
    return new Intl.DateTimeFormat('de-AT', {
      dateStyle: 'short',
      timeStyle: 'short',
    }).format(new Date(iso));
  } catch {
    return iso;
  }
}

export default function CustomerDashboardPage() {
  const { t } = useTranslation('orders');
  const { dashboard, isLoading, error, notFound, loadDashboard, reset } = useCustomerDashboard();
  const [tenant, setTenant] = useState('');
  const [phone, setPhone] = useState('');

  useEffect(() => {
    const envSlug = getEnvDevTenantSlug();
    if (envSlug) setTenant(envSlug);
  }, []);

  const onSearch = useCallback(() => {
    void loadDashboard(tenant, phone);
  }, [loadDashboard, tenant, phone]);

  const onRedeemHint = useCallback(() => {
    Alert.alert(t('dashboard.redeemTitle'), t('dashboard.redeemHint'));
  }, [t]);

  const statusLabel = (status: string) => {
    const key = `tracker.status.${status.toLowerCase()}` as const;
    const translated = t(key);
    return translated === key ? status : translated;
  };

  return (
    <ScrollView contentContainerStyle={styles.container} keyboardShouldPersistTaps="handled">
      <Text style={styles.title}>{t('dashboard.title')}</Text>
      <Text style={styles.subtitle}>{t('dashboard.subtitle')}</Text>

      <TextInput
        style={styles.input}
        placeholder={t('dashboard.tenantPlaceholder')}
        placeholderTextColor="#94a3b8"
        autoCapitalize="none"
        autoCorrect={false}
        value={tenant}
        onChangeText={setTenant}
      />
      <TextInput
        style={styles.input}
        placeholder={t('dashboard.phonePlaceholder')}
        placeholderTextColor="#94a3b8"
        keyboardType="phone-pad"
        value={phone}
        onChangeText={setPhone}
        onSubmitEditing={onSearch}
        returnKeyType="search"
      />

      <TouchableOpacity
        style={[styles.button, isLoading && styles.buttonDisabled]}
        onPress={onSearch}
        disabled={isLoading}
      >
        {isLoading ? (
          <ActivityIndicator color="#fff" />
        ) : (
          <Text style={styles.buttonText}>{t('dashboard.search')}</Text>
        )}
      </TouchableOpacity>

      {dashboard ? (
        <TouchableOpacity onPress={reset} style={styles.linkWrap}>
          <Text style={styles.link}>{t('dashboard.reset')}</Text>
        </TouchableOpacity>
      ) : null}

      {error === 'missing_params' ? (
        <Text style={styles.error}>{t('dashboard.missingParams')}</Text>
      ) : null}
      {error === 'phone_too_short' ? (
        <Text style={styles.error}>{t('dashboard.phoneTooShort')}</Text>
      ) : null}
      {error === 'fetch_failed' ? (
        <Text style={styles.error}>{t('dashboard.fetchFailed')}</Text>
      ) : null}
      {notFound ? <Text style={styles.error}>{t('dashboard.notFound')}</Text> : null}

      {dashboard ? (
        <>
          {dashboard.customerDisplayName ? (
            <Text style={styles.greeting}>
              {t('dashboard.greeting', { name: dashboard.customerDisplayName })}
            </Text>
          ) : null}

          <View style={styles.statsRow}>
            <View style={styles.statCard}>
              <Text style={styles.statLabel}>{t('dashboard.totalOrders')}</Text>
              <Text style={styles.statValue}>{dashboard.totalOrders}</Text>
            </View>
            <View style={styles.statCard}>
              <Text style={styles.statLabel}>{t('dashboard.loyaltyPoints')}</Text>
              <Text style={styles.statValue}>{dashboard.loyaltyPoints}</Text>
            </View>
            <View style={styles.statCard}>
              <Text style={styles.statLabel}>{t('dashboard.totalSpent')}</Text>
              <Text style={styles.statValue}>{money(dashboard.totalSpent)}</Text>
            </View>
          </View>

          <View style={styles.card}>
            <Text style={styles.cardTitle}>{t('dashboard.orderHistory')}</Text>
            {dashboard.orders.length === 0 ? (
              <Text style={styles.empty}>{t('dashboard.noOrders')}</Text>
            ) : (
              dashboard.orders.map((order) => (
                <View key={order.orderNumber + order.createdAt} style={styles.orderRow}>
                  <View style={styles.orderMain}>
                    <Text style={styles.orderNumber}>{order.orderNumber}</Text>
                    <Text style={styles.orderMeta}>{formatDate(order.createdAt)}</Text>
                  </View>
                  <View style={styles.orderRight}>
                    <Text style={styles.orderTotal}>{money(order.total, order.currency)}</Text>
                    <Text style={styles.orderStatus}>{statusLabel(order.orderStatus)}</Text>
                  </View>
                </View>
              ))
            )}
          </View>

          <View style={styles.card}>
            <Text style={styles.cardTitle}>{t('dashboard.loyaltyTitle')}</Text>
            <View style={styles.loyaltyBanner}>
              <Text style={styles.loyaltyMessage}>
                {t('dashboard.loyaltyMessage', { points: dashboard.loyaltyPoints })}
              </Text>
              <Text style={styles.loyaltyDesc}>
                {t('dashboard.loyaltyRedeemable', {
                  euro: money(dashboard.redeemableEuro),
                })}
              </Text>
            </View>
            <TouchableOpacity style={styles.secondaryButton} onPress={onRedeemHint}>
              <Text style={styles.secondaryButtonText}>{t('dashboard.redeemButton')}</Text>
            </TouchableOpacity>
          </View>
        </>
      ) : null}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    padding: 20,
    paddingBottom: 40,
    backgroundColor: '#f8fafc',
    flexGrow: 1,
  },
  title: {
    fontSize: 24,
    fontWeight: '700',
    color: '#0f172a',
    marginBottom: 8,
  },
  subtitle: {
    fontSize: 14,
    color: '#64748b',
    marginBottom: 20,
  },
  greeting: {
    fontSize: 16,
    fontWeight: '600',
    color: '#0f172a',
    marginBottom: 12,
  },
  input: {
    backgroundColor: '#fff',
    borderWidth: 1,
    borderColor: '#e2e8f0',
    borderRadius: 10,
    paddingHorizontal: 14,
    paddingVertical: 12,
    fontSize: 16,
    color: '#0f172a',
    marginBottom: 12,
  },
  button: {
    backgroundColor: '#2563eb',
    borderRadius: 10,
    paddingVertical: 14,
    alignItems: 'center',
    marginBottom: 12,
  },
  buttonDisabled: {
    opacity: 0.7,
  },
  buttonText: {
    color: '#fff',
    fontSize: 16,
    fontWeight: '600',
  },
  secondaryButton: {
    backgroundColor: '#ecfdf5',
    borderRadius: 10,
    paddingVertical: 12,
    alignItems: 'center',
    borderWidth: 1,
    borderColor: '#86efac',
    marginTop: 12,
  },
  secondaryButtonText: {
    color: '#166534',
    fontSize: 15,
    fontWeight: '600',
  },
  linkWrap: {
    marginBottom: 12,
  },
  link: {
    color: '#2563eb',
    fontSize: 14,
  },
  error: {
    color: '#b91c1c',
    marginBottom: 12,
  },
  statsRow: {
    flexDirection: 'row',
    gap: 8,
    marginBottom: 16,
  },
  statCard: {
    flex: 1,
    backgroundColor: '#fff',
    borderRadius: 12,
    padding: 12,
    borderWidth: 1,
    borderColor: '#e2e8f0',
  },
  statLabel: {
    fontSize: 12,
    color: '#64748b',
    marginBottom: 6,
  },
  statValue: {
    fontSize: 16,
    fontWeight: '700',
    color: '#0f172a',
  },
  card: {
    backgroundColor: '#fff',
    borderRadius: 12,
    padding: 16,
    borderWidth: 1,
    borderColor: '#e2e8f0',
    marginBottom: 16,
  },
  cardTitle: {
    fontSize: 16,
    fontWeight: '700',
    color: '#0f172a',
    marginBottom: 12,
  },
  empty: {
    color: '#64748b',
    fontSize: 14,
  },
  orderRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingVertical: 10,
    borderTopWidth: StyleSheet.hairlineWidth,
    borderTopColor: '#e2e8f0',
  },
  orderMain: {
    flex: 1,
    paddingRight: 8,
  },
  orderNumber: {
    fontSize: 14,
    fontWeight: '600',
    color: '#0f172a',
  },
  orderMeta: {
    fontSize: 12,
    color: '#64748b',
    marginTop: 2,
  },
  orderRight: {
    alignItems: 'flex-end',
  },
  orderTotal: {
    fontSize: 14,
    fontWeight: '700',
    color: '#0f172a',
  },
  orderStatus: {
    fontSize: 12,
    color: '#64748b',
    marginTop: 2,
  },
  loyaltyBanner: {
    backgroundColor: '#f0fdf4',
    borderRadius: 10,
    padding: 12,
    borderWidth: 1,
    borderColor: '#bbf7d0',
  },
  loyaltyMessage: {
    fontSize: 15,
    fontWeight: '600',
    color: '#166534',
    marginBottom: 4,
  },
  loyaltyDesc: {
    fontSize: 13,
    color: '#15803d',
  },
});
