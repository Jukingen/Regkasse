import { Ionicons } from '@expo/vector-icons';
import { Tabs, Redirect } from 'expo-router';
import React, { useEffect } from 'react';
import { View, ActivityIndicator, Text, Pressable, StyleSheet } from 'react-native';
import { useTranslation } from 'react-i18next';

import { useAuth } from '../../contexts/AuthContext';
import { useCart, calculateCartTotals } from '../../contexts/CartContext';
import PaymentModal from '../../components/PaymentModal';

export default function TabLayout() {
    const { t } = useTranslation(['navigation']);
    const { isAuthenticated, isLoading, isAuthReady, user, checkAuthStatus } = useAuth();

    // Context usage
    const {
        activeTableId,
        getCartForTable,
        isPaymentModalVisible,
        setIsPaymentModalVisible,
        clearCart
    } = useCart();

    const activeCart = getCartForTable(activeTableId);
    const totals = calculateCartTotals(activeCart.items);
    const cartCount = totals.itemCount;

    const handlePaymentSuccess = async (paymentId: string) => {
        await clearCart(activeTableId);
    };

    // OPTIMIZATION: Auth status kontrolünü daha az sıklıkta yap
    useEffect(() => {
        if (!user || !isAuthenticated) {
            return;
        }

        checkAuthStatus();

        const interval = setInterval(() => {
            checkAuthStatus();
        }, 5 * 60 * 1000); // 5 dakika

        return () => {
            clearInterval(interval);
        };
    }, []);

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

    return (
        <View style={{ flex: 1 }}>
            <Tabs
                screenOptions={{
                    tabBarActiveTintColor: '#007AFF',
                    tabBarInactiveTintColor: '#8E8E93',
                    tabBarStyle: { height: 60, paddingBottom: 10, overflow: 'visible' },
                    headerShown: true
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
                        title: 'Sepet',
                        tabBarButton: (props) => {
                            const { style, onPress } = props;
                            return (
                                <Pressable
                                    onPress={() => setIsPaymentModalVisible(true)}
                                    style={[style, styles.cartTabButton]}
                                >
                                    <View style={styles.cartIconContainer}>
                                        <Ionicons name="cart" size={28} color="#fff" />
                                        {cartCount > 0 && (
                                            <View style={styles.badge}>
                                                <Text style={styles.badgeText}>{cartCount}</Text>
                                            </View>
                                        )}
                                    </View>
                                    <Text style={styles.cartLabel}>Sepet</Text>
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
                cartItems={(activeCart?.items || []).map(item => ({
                    id: item.itemId || item.productId,
                    productId: item.productId,
                    productName: item.productName || 'Unknown Product',
                    quantity: item.qty,
                    unitPrice: item.unitPrice || item.price || 0,
                    totalPrice: item.totalPrice || ((item.price || 0) * item.qty),
                    taxType: undefined
                }))}
                customerId="00000000-0000-0000-0000-000000000000"
                tableNumber={activeTableId}
            />
        </View>
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
        height: 54,
        borderRadius: 27,
        backgroundColor: '#007AFF',
        justifyContent: 'center',
        alignItems: 'center',
        shadowColor: '#000',
        shadowOffset: { width: 0, height: 4 },
        shadowOpacity: 0.3,
        shadowRadius: 4,
        elevation: 8,
    },
    cartLabel: {
        fontSize: 10,
        color: '#007AFF',
        marginTop: 4,
        fontWeight: 'bold',
    },
    badge: {
        position: 'absolute',
        top: -4,
        right: -4,
        backgroundColor: '#FF3B30',
        borderRadius: 10,
        minWidth: 20,
        height: 20,
        justifyContent: 'center',
        alignItems: 'center',
        paddingHorizontal: 4,
        borderWidth: 2,
        borderColor: '#fff',
    },
    badgeText: {
        color: '#fff',
        fontSize: 10,
        fontWeight: 'bold',
    },
});
