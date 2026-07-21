import { Ionicons } from '@expo/vector-icons';
import { Tabs, Redirect } from 'expo-router';
import React, { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { View, Text, Pressable, StyleSheet, Alert } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { EnvironmentBadge } from '../../components/EnvironmentBadge';
import { OfflineStatusChip } from '../../components/OfflineStatusChip';
import { Header as WorkingHoursStatus } from '../../components/Header';
import { LicenseExpiryBanner } from '../../components/LicenseExpiryBanner';
import { LicenseStatusIndicator } from '../../components/LicenseStatusIndicator';
import { LicenseWarningBanner } from '../../components/LicenseWarningBanner';
import { MonatsbelegHeaderBadge } from '../../components/MonatsbelegHeaderBadge';
import { OFFLINE_CONFIG } from '../../constants/offlineConfig';
import { MonatsbelegSessionBlockModal } from '../../components/MonatsbelegSessionBlockModal';
import { OfflineBanner } from '../../components/OfflineBanner';
import PaymentModal from '../../components/PaymentModal';
import { StartbelegRequiredBanner } from '../../components/StartbelegRequiredBanner';
import { TagesabschlussReminder } from '../../components/TagesabschlussReminder';
import { TimeSyncBanner } from '../../components/TimeSyncBanner';
import { ToastContainer } from '../../components/ToastNotification';
import { TseOfflineRestrictionBanner, TseStatusBanner } from '../../components/TseStatusBanner';
import { UserMenu } from '../../components/UserMenu';
import { SoftColors, SoftShadows, SoftSpacing } from '../../constants/SoftTheme';
import { TAB_BAR_HEIGHT } from '../../constants/breakpoints';
import { POS_ENSURE_READY_ON_ENTRY } from '../../constants/posFeatureFlags';
import { POS_HEALTH_POLL_MS } from '../../constants/posPollingIntervals';
import { useCart, getCartDisplayTotals, getCartLineTotal } from '../../contexts/CartContext';
import { useAuth } from '../../contexts/AuthContext';
import { useDevelopmentModeContext } from '../../contexts/DevelopmentModeContext';
import {
  PosRegisterReadinessProvider,
  usePosRegisterReadiness,
} from '../../contexts/PosRegisterReadinessContext';
import { TseHealthProvider } from '../../contexts/TseHealthContext';
import { useConditionalPolling } from '../../hooks/useConditionalPolling';
import { TimeSyncStatusProvider } from '../../hooks/useTimeSyncStatus';
import { subscribeOfflineSyncComplete } from '../../services/payment/offlineQueueSyncNotifier';
import { WaveLoader } from '../../src/components/common/WaveLoader';
import { DevTenantSwitcher } from '../../src/components/dev/DevTenantSwitcher';
import { eventEmitter } from '../../utils/eventEmitter';
import { formatPrice } from '../../utils/formatPrice';
import {
  isReadinessRegisterDecommissioned,
  isReadinessStartbelegGateActive,
  POS_DECOMMISSIONED_SALES_BLOCK_MESSAGE_DE,
} from '../../utils/posRegisterGateCopy';
import { isPosAllowedRole } from '../../utils/posRoleGuard';

type PosTabsInnerProps = {
  t: (key: string, options?: Record<string, string | number>) => string;
  insets: ReturnType<typeof useSafeAreaInsets>;
  cartCount: number;
  isPaymentModalVisible: boolean;
  setIsPaymentModalVisible: (visible: boolean) => void;
  handlePaymentSuccess: (paymentId: string, paidTableNumber?: number) => Promise<void>;
  currentCart: ReturnType<typeof useCart>['currentCart'];
  totals: ReturnType<typeof getCartDisplayTotals>;
  activeTableId: number;
  saleCustomer: ReturnType<typeof useCart>['saleCustomer'];
  developmentModeSettings: ReturnType<typeof useDevelopmentModeContext>['settings'];
};

function PosTabsInner({
  t,
  insets,
  cartCount,
  isPaymentModalVisible,
  setIsPaymentModalVisible,
  handlePaymentSuccess,
  currentCart,
  totals,
  activeTableId,
  saleCustomer,
  developmentModeSettings,
}: PosTabsInnerProps) {
  const posReadiness = usePosRegisterReadiness();

  const [tabBarToasts, setTabBarToasts] = useState<
    {
      id: string;
      type: 'success' | 'error' | 'info' | 'warning';
      message: string;
      duration?: number;
    }[]
  >([]);

  const removeTabBarToast = useCallback((id: string) => {
    setTabBarToasts((prev) => prev.filter((x) => x.id !== id));
  }, []);

  const tryOpenPaymentModal = () => {
    if (cartCount === 0) {
      const id = `${Date.now()}-empty-cart`;
      const message = t('checkout:posFlow.toast.emptyCart');
      setTabBarToasts((prev) => [...prev, { id, type: 'warning', message, duration: 2500 }]);
      setTimeout(() => {
        removeTabBarToast(id);
      }, 2500);
      return;
    }
    if (isReadinessRegisterDecommissioned(posReadiness.data)) {
      Alert.alert('Verkauf', POS_DECOMMISSIONED_SALES_BLOCK_MESSAGE_DE);
      return;
    }
    if (
      isReadinessStartbelegGateActive(posReadiness.data, {
        ensureReadyEnabled: POS_ENSURE_READY_ON_ENTRY,
      })
    ) {
      Alert.alert(
        'Startbeleg erforderlich',
        'Bitte zuerst den fiskalischen Startbeleg erstellen, bevor Sie zur Zahlung wechseln.'
      );
      return;
    }
    setIsPaymentModalVisible(true);
  };

  const pushTabBarToast = useCallback(
    (payload: {
      type?: 'success' | 'warning' | 'info' | 'error';
      message: string;
      duration?: number;
    }) => {
      const id = `${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
      setTabBarToasts((prev) => [
        ...prev,
        {
          id,
          type: payload.type ?? 'info',
          message: payload.message,
          duration: payload.duration ?? 4500,
        },
      ]);
      setTimeout(
        () => {
          removeTabBarToast(id);
        },
        (payload.duration ?? 4500) + 400
      );
    },
    [removeTabBarToast]
  );

  useEffect(() => {
    const onOrderSaved = (payload: {
      offlineOrderId: string;
      pendingCount: number;
      maxLimit: number;
      remaining: number;
    }) => {
      const remaining = payload.remaining;
      let type: 'success' | 'warning' | 'error' = 'success';
      let message =
        remaining === 1
          ? `Bestellung offline gespeichert — noch 1 Bestellung möglich (${payload.pendingCount}/${payload.maxLimit})`
          : `Bestellung offline gespeichert — noch ${remaining} Bestellungen möglich (${payload.pendingCount}/${payload.maxLimit})`;
      let duration = 3000;

      if (payload.pendingCount >= OFFLINE_CONFIG.CRITICAL_PENDING_COUNT) {
        type = 'error';
        duration = 5000;
        message = `Kritisch: Offline-Warteschlange fast voll (${payload.pendingCount}/${payload.maxLimit}) — nur noch ${remaining} möglich`;
      } else if (payload.pendingCount >= OFFLINE_CONFIG.WARNING_PENDING_COUNT) {
        type = 'warning';
        duration = 4500;
        message = `Warnung: ${payload.pendingCount}/${payload.maxLimit} Offline-Bestellungen — bitte bald synchronisieren`;
      }

      pushTabBarToast({ type, message, duration });
    };

    const onLimitExceeded = (payload: { pendingCount: number; maxLimit: number }) => {
      pushTabBarToast({
        type: 'error',
        message: `Offline-Limit erreicht (${payload.pendingCount}/${payload.maxLimit}). Bitte Internetverbindung prüfen und synchronisieren.`,
        duration: 5000,
      });
    };

    const onOfflineWarning = (payload: { hoursRemaining: number }) => {
      pushTabBarToast({
        type: 'warning',
        message: `Offline-Frist: noch ca. ${Math.ceil(payload.hoursRemaining)} Stunden — bitte verbinden und synchronisieren`,
        duration: 6000,
      });
    };

    const onOfflineCritical = (payload: { hoursRemaining: number }) => {
      pushTabBarToast({
        type: 'error',
        message: `Kritisch: Offline-Frist endet in ca. ${Math.ceil(payload.hoursRemaining)} Stunden — Bestellungen drohen zu verfallen`,
        duration: 8000,
      });
    };

    eventEmitter.on('offline:order-saved', onOrderSaved);
    eventEmitter.on('offline:limit-exceeded', onLimitExceeded);
    eventEmitter.on('offline:warning', onOfflineWarning);
    eventEmitter.on('offline:critical', onOfflineCritical);

    return () => {
      eventEmitter.off('offline:order-saved', onOrderSaved);
      eventEmitter.off('offline:limit-exceeded', onLimitExceeded);
      eventEmitter.off('offline:warning', onOfflineWarning);
      eventEmitter.off('offline:critical', onOfflineCritical);
    };
  }, [pushTabBarToast]);

  return (
    <TseHealthProvider>
      <View style={{ flex: 1 }}>
        <ToastContainer toasts={tabBarToasts} onRemove={removeTabBarToast} />
        <LicenseWarningBanner />
        <LicenseExpiryBanner />
        {/* Top chrome sits above Tabs — apply notch/status-bar inset here once. */}
        <View style={[styles.licenseStatusBar, { paddingTop: insets.top + 6 }]}>
          <View style={styles.headerStatusLeft}>
            <TseStatusBanner />
            <WorkingHoursStatus />
            <OfflineStatusChip />
            <MonatsbelegHeaderBadge />
          </View>
          <View style={styles.headerRight}>
            <LicenseStatusIndicator />
            <DevTenantSwitcher />
            <EnvironmentBadge settings={developmentModeSettings} />
            <UserMenu />
          </View>
        </View>
        <TimeSyncBanner />
        <TseOfflineRestrictionBanner />
        <MonatsbelegSessionBlockModal />
        <StartbelegRequiredBanner />
        <TagesabschlussReminder />
        <Tabs
          screenOptions={{
            tabBarActiveTintColor: SoftColors.accent,
            tabBarInactiveTintColor: SoftColors.textMuted,
            tabBarStyle: {
              height: TAB_BAR_HEIGHT + insets.bottom,
              paddingBottom: insets.bottom,
              paddingTop: 8,
              overflow: 'visible',
            },
            // Custom licenseStatusBar already consumed the top inset — avoid double padding.
            headerStatusBarHeight: 0,
            headerShown: true,
          }}>
          <Tabs.Screen
            name="cash-register"
            options={{
              title: t('navigation:cashRegister') || 'Kasa',
              tabBarIcon: ({ color }) => <Ionicons name="cash-outline" size={24} color={color} />,
            }}
          />

          <Tabs.Screen
            name="cart"
            options={{
              title: t('navigation:cart'),
              tabBarButton: (props) => {
                const { style } = props;
                return (
                  <Pressable
                    onPress={tryOpenPaymentModal}
                    style={[style, styles.cartTabButton]}
                    hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
                    accessibilityLabel={
                      cartCount > 0
                        ? t('navigation:cartAccessibility.withCount', {
                            count: cartCount,
                            total: formatPrice(totals.grandTotalGross),
                          })
                        : t('navigation:cartAccessibility.default', {
                            total: formatPrice(totals.grandTotalGross),
                          })
                    }
                    accessibilityRole="button">
                    <View style={styles.cartIconContainer}>
                      <Ionicons name="cart" size={28} color={SoftColors.textInverse} />
                      {cartCount > 0 && (
                        <View style={styles.badge}>
                          <Text style={styles.badgeText}>{cartCount}</Text>
                        </View>
                      )}
                    </View>
                    <Text style={styles.cartLabel} numberOfLines={1}>
                      {formatPrice(totals.grandTotalGross)}
                    </Text>
                  </Pressable>
                );
              },
            }}
          />

          {/* Settings & Admin remain routable via UserMenu; hidden from footer */}
          <Tabs.Screen
            name="settings"
            options={{
              href: null,
              title: t('navigation:settings') || 'Einstellungen',
            }}
          />

          <Tabs.Screen
            name="admin-menu"
            options={{
              href: null,
              title: 'Admin',
            }}
          />
        </Tabs>

        <PaymentModal
          visible={isPaymentModalVisible}
          onClose={() => {
            setIsPaymentModalVisible(false);
          }}
          onSuccess={handlePaymentSuccess}
          onPosToast={(p) => {
            pushTabBarToast({ type: p.type ?? 'info', message: p.message });
          }}
          cartItems={(currentCart?.items || []).map((item) => ({
            id: item.itemId ?? item.clientId ?? item.productId,
            productId: item.productId,
            productName: item.productName || 'Unknown Product',
            quantity: item.qty,
            unitPrice: item.unitPrice || item.price || 0,
            totalPrice: item.totalPrice ?? getCartLineTotal(item as any),
            taxType: item.taxType,
            modifiers: item.modifiers?.map((m) => ({
              modifierId: m.id,
              name: m.name,
              priceDelta: m.price,
            })),
          }))}
          grandTotalGross={totals.grandTotalGross}
          customerId={saleCustomer?.id ?? '00000000-0000-0000-0000-000000000000'}
          tableNumber={activeTableId}
        />
        <OfflineBanner />
      </View>
    </TseHealthProvider>
  );
}

export default function TabLayout() {
  const { t } = useTranslation(['navigation', 'checkout']);
  const insets = useSafeAreaInsets();
  const { isAuthenticated, isLoading, isAuthReady, user, checkAuthStatus, logout } = useAuth();
  const { settings: developmentModeSettings } = useDevelopmentModeContext();
  const checkAuthStatusRef = useRef(checkAuthStatus);
  checkAuthStatusRef.current = checkAuthStatus;

  // Context usage
  const {
    activeTableId,
    currentCart,
    isPaymentModalVisible,
    setIsPaymentModalVisible,
    clearCart,
    saleCustomer,
    setSaleCustomer,
  } = useCart();

  const totals = getCartDisplayTotals(currentCart);
  const cartCount = totals.itemCount;

  const handlePaymentSuccess = async (paymentId: string, paidTableNumber?: number) => {
    setSaleCustomer(null);
    await clearCart(paidTableNumber ?? activeTableId);
  };

  // When background offline queue sync completes after reconnect, show short summary to user
  useEffect(() => {
    const unsub = subscribeOfflineSyncComplete((processed, failed) => {
      if (processed > 0 && failed > 0) {
        Alert.alert(
          t('navigation:offlineQueue.title'),
          t('navigation:offlineQueue.syncSummaryPartial', { processed, failed })
        );
      } else if (processed > 0) {
        Alert.alert(
          t('navigation:offlineQueue.title'),
          t('navigation:offlineQueue.syncSummarySuccess', { processed })
        );
      } else if (failed > 0) {
        Alert.alert(
          t('navigation:offlineQueue.title'),
          t('navigation:offlineQueue.syncSummaryAllFailed', { failed })
        );
      }
    });
    return unsub;
  }, [t]);

  useConditionalPolling(
    () => {
      void checkAuthStatusRef.current();
    },
    POS_HEALTH_POLL_MS,
    Boolean(isAuthenticated && user?.id)
  );

  if (!isAuthReady || isLoading) {
    return (
      <View style={{ flex: 1, justifyContent: 'center', alignItems: 'center' }}>
        <WaveLoader size={32} color="#007AFF" />
      </View>
    );
  }

  if (!isAuthenticated || !user) {
    return <Redirect href="/(auth)/login" />;
  }

  if (user.mustChangePasswordOnNextLogin) {
    return <Redirect href="/(auth)/change-password" />;
  }

  // POS rol guard: yetkisiz rol tabs'a erişemez, login'e geri gönderilir
  if (!isPosAllowedRole(user.role, user.roles)) {
    console.warn('[TabLayout] POS role denied, redirecting to login. role:', user.role);
    logout();
    return <Redirect href="/(auth)/login" />;
  }

  return (
    <PosRegisterReadinessProvider>
      <TimeSyncStatusProvider enabled>
        <PosTabsInner
          t={t}
          insets={insets}
          cartCount={cartCount}
          isPaymentModalVisible={isPaymentModalVisible}
          setIsPaymentModalVisible={setIsPaymentModalVisible}
          handlePaymentSuccess={handlePaymentSuccess}
          currentCart={currentCart}
          totals={totals}
          activeTableId={activeTableId}
          saleCustomer={saleCustomer}
          developmentModeSettings={developmentModeSettings}
        />
      </TimeSyncStatusProvider>
    </PosRegisterReadinessProvider>
  );
}

const styles = StyleSheet.create({
  licenseStatusBar: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    gap: 8,
    flexWrap: 'nowrap',
    paddingHorizontal: SoftSpacing.sm,
    paddingBottom: 6,
    backgroundColor: SoftColors.bgPrimary,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: SoftColors.border,
    minHeight: 40,
  },
  headerStatusLeft: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    flex: 1,
    flexShrink: 1,
    minWidth: 0,
  },
  headerRight: {
    flexDirection: 'row',
    justifyContent: 'flex-end',
    alignItems: 'center',
    gap: 8,
    flexWrap: 'wrap',
    flexShrink: 0,
  },
  cartTabButton: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    marginTop: -15,
  },
  cartIconContainer: {
    width: 54,
    minHeight: 54,
    borderRadius: 27,
    backgroundColor: SoftColors.accent,
    justifyContent: 'center',
    alignItems: 'center',
    ...SoftShadows.md,
  },
  cartLabel: {
    fontSize: 10,
    color: SoftColors.accentDark,
    marginTop: 4,
    fontWeight: '600',
  },
  badge: {
    position: 'absolute',
    top: -4,
    right: -4,
    backgroundColor: SoftColors.error,
    borderRadius: 10,
    minWidth: 20,
    height: 20,
    justifyContent: 'center',
    alignItems: 'center',
    paddingHorizontal: 4,
    borderWidth: 2,
    borderColor: SoftColors.bgCard,
  },
  badgeText: {
    color: SoftColors.textInverse,
    fontSize: 10,
    fontWeight: 'bold',
  },
});
