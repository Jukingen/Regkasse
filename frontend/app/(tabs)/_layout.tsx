import { Ionicons } from '@expo/vector-icons';
import { Tabs, Redirect } from 'expo-router';
import React, { useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { View, ActivityIndicator, Text, Pressable, StyleSheet, Alert } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import PaymentModal from '../../components/PaymentModal';
import { subscribeOfflineSyncComplete } from '../../services/payment/offlineQueueSyncNotifier';
import { TAB_BAR_HEIGHT } from '../../constants/breakpoints';
import { SoftColors, SoftShadows } from '../../constants/SoftTheme';
import { useAuth } from '../../contexts/AuthContext';
import { PosRegisterReadinessProvider } from '../../contexts/PosRegisterReadinessContext';
import { useCart, getCartDisplayTotals, getCartLineTotal } from '../../contexts/CartContext';
import { isPosAllowedRole } from '../../utils/posRoleGuard';

export default function TabLayout() {
    const { t } = useTranslation(['navigation']);
    const insets = useSafeAreaInsets();
    const { isAuthenticated, isLoading, isAuthReady, user, checkAuthStatus, logout } = useAuth();
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
            if (processed > 0) {
                const msg = failed > 0
                    ? t('navigation:offlineQueue.syncSummaryPartial', { processed, failed })
                    : t('navigation:offlineQueue.syncSummarySuccess', { processed });
                Alert.alert(t('navigation:offlineQueue.title'), msg);
            }
        });
        return unsub;
    }, [t]);

    // OPTIMIZATION: Auth status kontrolünü daha az sıklıkta yap (ref avoids stale closure from [] deps).
    useEffect(() => {
        if (!user || !isAuthenticated) {
            return;
        }

        void checkAuthStatusRef.current();

        const interval = setInterval(() => {
            void checkAuthStatusRef.current();
        }, 5 * 60 * 1000); // 5 dakika

        return () => {
            clearInterval(interval);
        };
    }, [isAuthenticated, user?.id]);

    if (!isAuthReady || isLoading) {
        return (
            <View style={{ flex: 1, justifyContent: 'center', alignItems: 'center' }}>
                <ActivityIndicator size="large" color="#007AFF" />
            </View>
        );
    }

    if (!isAuthenticated || !user) {
        return <Redirect href="/(auth)/login" />;
    }

    // POS rol guard: yetkisiz rol tabs'a erişemez, login'e geri gönderilir
    if (!isPosAllowedRole(user.role, user.roles)) {
        console.warn('[TabLayout] POS role denied, redirecting to login. role:', user.role);
        logout();
        return <Redirect href="/(auth)/login" />;
    }

    return (
        <PosRegisterReadinessProvider>
        <View style={{ flex: 1 }}>
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
                    headerShown: true,
                }}
            >
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
                            const { style, onPress } = props;
                            return (
                                <Pressable
                                    onPress={() => setIsPaymentModalVisible(true)}
                                    style={[style, styles.cartTabButton]}
                                    hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
                                    accessibilityLabel={cartCount > 0
                                        ? t('navigation:cartAccessibility.withCount', { count: cartCount })
                                        : t('navigation:cartAccessibility.default')}
                                    accessibilityRole="button"
                                >
                                    <View style={styles.cartIconContainer}>
                                        <Ionicons name="cart" size={28} color={SoftColors.textInverse} />
                                        {cartCount > 0 && (
                                            <View style={styles.badge}>
                                                <Text style={styles.badgeText}>{cartCount}</Text>
                                            </View>
                                        )}
                                    </View>
                                    <Text style={styles.cartLabel}>{t('navigation:cart')}</Text>
                                </Pressable>
                            );
                        }
                    }}
                />

                <Tabs.Screen
                    name="settings"
                    options={{
                        title: t('navigation:settings') || 'Ayarlar',
                        tabBarIcon: ({ color }) => <Ionicons name="settings-outline" size={24} color={color} />,
                    }}
                />
            </Tabs>

            <PaymentModal
                visible={isPaymentModalVisible}
                onClose={() => setIsPaymentModalVisible(false)}
                onSuccess={handlePaymentSuccess}
                cartItems={(currentCart?.items || []).map(item => ({
                    id: item.itemId ?? item.clientId ?? item.productId,
                    productId: item.productId,
                    productName: item.productName || 'Unknown Product',
                    quantity: item.qty,
                    unitPrice: item.unitPrice || item.price || 0,
                    totalPrice: item.totalPrice ?? getCartLineTotal(item as any),
                    taxType: item.taxType,
                    modifiers: item.modifiers?.map(m => ({ modifierId: m.id, name: m.name, priceDelta: m.price }))
                }))}
                grandTotalGross={totals.grandTotalGross}
                customerId={saleCustomer?.id ?? '00000000-0000-0000-0000-000000000000'}
                tableNumber={activeTableId}
            />
        </View>
        </PosRegisterReadinessProvider>
    );
}

const styles = StyleSheet.create({
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
