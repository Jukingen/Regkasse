/**
 * Customer online-order status tracker (public API).
 * Expo route: /order-tracker — React Native UI (not Ant Design).
 */

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  ActivityIndicator,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from 'react-native';
import { useTranslation } from 'react-i18next';
import { useOrderStatus } from '@/hooks/useOrderStatus';
import { getEnvDevTenantSlug } from '@/services/tenant/devTenant';

const STEP_KEYS = ['pending', 'accepted', 'preparing', 'ready', 'completed'] as const;

function statusColor(status: string): string {
  switch (status.toLowerCase()) {
    case 'pending':
      return '#f59e0b';
    case 'accepted':
      return '#3b82f6';
    case 'preparing':
      return '#8b5cf6';
    case 'ready':
      return '#22c55e';
    case 'completed':
      return '#16a34a';
    case 'cancelled':
      return '#ef4444';
    default:
      return '#64748b';
  }
}

function stepIndex(status: string): number {
  if (status.toLowerCase() === 'cancelled') return -1;
  const idx = STEP_KEYS.indexOf(status.toLowerCase() as (typeof STEP_KEYS)[number]);
  return idx >= 0 ? idx : 0;
}

export default function OrderTrackerPage() {
  const { t } = useTranslation('orders');
  const { order, isLoading, error, notFound, fetchOrder } = useOrderStatus();
  const [tenant, setTenant] = useState('');
  const [orderNumber, setOrderNumber] = useState('');
  const [phone, setPhone] = useState('');

  useEffect(() => {
    const envSlug = getEnvDevTenantSlug();
    if (envSlug) setTenant(envSlug);
  }, []);

  const onSearch = useCallback(() => {
    void fetchOrder(tenant, orderNumber, phone || undefined);
  }, [fetchOrder, tenant, orderNumber, phone]);

  const currentStep = useMemo(
    () => (order ? stepIndex(order.orderStatus) : -1),
    [order],
  );

  const money = (value: number, currency: string) => {
    try {
      return new Intl.NumberFormat('de-AT', { style: 'currency', currency }).format(value);
    } catch {
      return `€ ${value.toFixed(2)}`;
    }
  };

  const statusLabel = (status: string) => {
    const key = `tracker.status.${status.toLowerCase()}` as const;
    const translated = t(key);
    return translated === key ? status : translated;
  };

  const stepLabel = (step: (typeof STEP_KEYS)[number]) => t(`tracker.steps.${step}`);

  return (
    <ScrollView contentContainerStyle={styles.container} keyboardShouldPersistTaps="handled">
      <Text style={styles.title}>{t('tracker.title')}</Text>
      <Text style={styles.subtitle}>{t('tracker.subtitle')}</Text>

      <View style={styles.searchContainer}>
        <TextInput
          style={styles.input}
          placeholder={t('tracker.tenantPlaceholder')}
          placeholderTextColor="#94a3b8"
          autoCapitalize="none"
          autoCorrect={false}
          value={tenant}
          onChangeText={setTenant}
        />
        <TextInput
          style={styles.input}
          placeholder={t('tracker.orderNumberPlaceholder')}
          placeholderTextColor="#94a3b8"
          autoCapitalize="characters"
          autoCorrect={false}
          value={orderNumber}
          onChangeText={setOrderNumber}
          onSubmitEditing={onSearch}
          returnKeyType="search"
        />
        <TouchableOpacity
          style={[styles.searchButton, isLoading && styles.buttonDisabled]}
          onPress={onSearch}
          disabled={isLoading}
          accessibilityRole="button"
          accessibilityLabel={t('tracker.search')}
        >
          {isLoading ? (
            <ActivityIndicator color="#fff" />
          ) : (
            <Text style={styles.searchButtonText}>{t('tracker.search')}</Text>
          )}
        </TouchableOpacity>
      </View>

      <TextInput
        style={styles.input}
        placeholder={t('tracker.phonePlaceholder')}
        placeholderTextColor="#94a3b8"
        keyboardType="phone-pad"
        value={phone}
        onChangeText={setPhone}
        onSubmitEditing={onSearch}
      />

      {error === 'missing_params' ? (
        <Text style={styles.error}>{t('tracker.missingParams')}</Text>
      ) : null}
      {error === 'fetch_failed' ? (
        <Text style={styles.error}>{t('tracker.fetchFailed')}</Text>
      ) : null}
      {notFound ? (
        <View style={styles.errorBox}>
          <Text style={styles.errorBoxText}>{t('tracker.notFound')}</Text>
        </View>
      ) : null}

      {isLoading && !order ? (
        <View style={styles.loadingContainer}>
          <ActivityIndicator size="large" color="#1a56db" />
        </View>
      ) : null}

      {order ? (
        <View style={styles.orderCard}>
          <View style={styles.orderHeader}>
            <Text style={styles.orderNumber}>#{order.orderNumber}</Text>
            <View style={[styles.statusBadge, { backgroundColor: statusColor(order.orderStatus) }]}>
              <Text style={styles.statusBadgeText}>{statusLabel(order.orderStatus)}</Text>
            </View>
          </View>

          <View style={styles.orderInfo}>
            <Text style={styles.orderTotal}>
              {t('tracker.total')}: {money(order.total, order.currency)}
            </Text>
            {order.createdAt ? (
              <Text style={styles.orderDate}>
                {new Date(order.createdAt).toLocaleString('de-DE')}
              </Text>
            ) : null}
          </View>

          {order.customerDisplayName ? (
            <View style={styles.customerRow}>
              <Text style={styles.customerLabel}>{t('tracker.customer')}</Text>
              <Text style={styles.customerValue}>{order.customerDisplayName}</Text>
            </View>
          ) : null}

          {currentStep >= 0 ? (
            <View style={styles.stepsContainer}>
              {STEP_KEYS.map((step, index) => {
                const completed = index <= currentStep;
                const current = index === currentStep;
                const showConnector = index < STEP_KEYS.length - 1;
                return (
                  <React.Fragment key={step}>
                    <View style={styles.stepWrapper}>
                      <View
                        style={[
                          styles.stepCircle,
                          completed && styles.stepCompleted,
                          current && styles.stepCurrent,
                        ]}
                      >
                        <Text
                          style={[
                            styles.stepNumber,
                            completed && styles.stepNumberCompleted,
                          ]}
                        >
                          {completed ? '✓' : String(index + 1)}
                        </Text>
                      </View>
                      <Text
                        style={[styles.stepLabel, completed && styles.stepLabelCompleted]}
                        numberOfLines={2}
                      >
                        {stepLabel(step)}
                      </Text>
                    </View>
                    {showConnector ? (
                      <View
                        style={[
                          styles.stepConnector,
                          index < currentStep && styles.stepConnectorCompleted,
                        ]}
                      />
                    ) : null}
                  </React.Fragment>
                );
              })}
            </View>
          ) : (
            <View
              style={[
                styles.statusBadge,
                { backgroundColor: statusColor('cancelled'), alignSelf: 'flex-start' },
              ]}
            >
              <Text style={styles.statusBadgeText}>{statusLabel('cancelled')}</Text>
            </View>
          )}
        </View>
      ) : null}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    padding: 16,
    paddingBottom: 40,
    backgroundColor: '#f8fafc',
    flexGrow: 1,
  },
  title: {
    fontSize: 24,
    fontWeight: '700',
    color: '#1e293b',
    marginBottom: 8,
  },
  subtitle: {
    fontSize: 14,
    color: '#64748b',
    marginBottom: 16,
  },
  searchContainer: {
    gap: 8,
    marginBottom: 12,
  },
  input: {
    backgroundColor: '#fff',
    borderWidth: 1,
    borderColor: '#e2e8f0',
    borderRadius: 8,
    paddingHorizontal: 14,
    paddingVertical: 12,
    fontSize: 16,
    color: '#0f172a',
  },
  searchButton: {
    backgroundColor: '#1a56db',
    borderRadius: 8,
    paddingVertical: 14,
    paddingHorizontal: 16,
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: 12,
  },
  buttonDisabled: {
    opacity: 0.7,
  },
  searchButtonText: {
    color: '#fff',
    fontSize: 16,
    fontWeight: '600',
  },
  loadingContainer: {
    alignItems: 'center',
    padding: 32,
  },
  error: {
    color: '#b91c1c',
    marginBottom: 12,
  },
  errorBox: {
    backgroundColor: '#fee2e2',
    padding: 12,
    borderRadius: 8,
    marginBottom: 12,
  },
  errorBoxText: {
    color: '#dc2626',
  },
  orderCard: {
    backgroundColor: '#fff',
    borderRadius: 12,
    padding: 16,
    borderWidth: 1,
    borderColor: '#e2e8f0',
  },
  orderHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 12,
  },
  orderNumber: {
    fontSize: 18,
    fontWeight: '600',
    color: '#0f172a',
  },
  statusBadge: {
    paddingHorizontal: 12,
    paddingVertical: 4,
    borderRadius: 12,
  },
  statusBadgeText: {
    color: '#fff',
    fontWeight: '600',
    fontSize: 12,
  },
  orderInfo: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 12,
  },
  orderTotal: {
    fontSize: 16,
    fontWeight: '600',
    color: '#1a56db',
  },
  orderDate: {
    fontSize: 14,
    color: '#64748b',
  },
  customerRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: 16,
    paddingBottom: 12,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: '#e2e8f0',
  },
  customerLabel: {
    fontSize: 14,
    color: '#64748b',
  },
  customerValue: {
    fontSize: 14,
    color: '#0f172a',
  },
  stepsContainer: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    marginTop: 8,
  },
  stepWrapper: {
    alignItems: 'center',
    flex: 1,
  },
  stepCircle: {
    width: 32,
    height: 32,
    borderRadius: 16,
    backgroundColor: '#e2e8f0',
    justifyContent: 'center',
    alignItems: 'center',
    marginBottom: 6,
  },
  stepCompleted: {
    backgroundColor: '#22c55e',
  },
  stepCurrent: {
    backgroundColor: '#1a56db',
  },
  stepNumber: {
    fontSize: 14,
    color: '#64748b',
    fontWeight: '600',
  },
  stepNumberCompleted: {
    color: '#fff',
  },
  stepLabel: {
    fontSize: 10,
    color: '#94a3b8',
    textAlign: 'center',
    paddingHorizontal: 2,
  },
  stepLabelCompleted: {
    color: '#0f172a',
    fontWeight: '600',
  },
  stepConnector: {
    width: 12,
    height: 2,
    backgroundColor: '#e2e8f0',
    marginTop: 15,
    marginHorizontal: -2,
  },
  stepConnectorCompleted: {
    backgroundColor: '#22c55e',
  },
});
