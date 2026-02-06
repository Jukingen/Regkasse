import { Ionicons } from '@expo/vector-icons';
import { Tabs, Redirect } from 'expo-router';
import React, { useEffect, useCallback } from 'react';
import { View, ActivityIndicator, Text } from 'react-native';

import { useAuth } from '../../contexts/AuthContext';
import { useTableOrdersRecovery } from '../../hooks/useTableOrdersRecovery';

export default function TabLayout() {
    const { isAuthenticated, isLoading, isAuthReady, user, checkAuthStatus } = useAuth();

    // F5 sonrası masa siparişlerini geri yükleme hook'u
    const {
        isLoading: recoveryLoading,
        error: recoveryError,
        isRecoveryCompleted,
        hasActiveOrders,
        totalActiveTables,
        isInitialized: recoveryInitialized // Yeni: Initialization status
    } = useTableOrdersRecovery();

    // Debug logları
    console.log('TabLayout render:', { isAuthenticated, isLoading, user: user?.email, role: user?.role });
    console.log('TabLayout: Full user object:', user);
    console.log('TableOrders Recovery:', { recoveryLoading, recoveryError, isRecoveryCompleted, hasActiveOrders, totalActiveTables, recoveryInitialized });

    // OPTIMIZATION: Auth status kontrolünü daha az sıklıkta yap
    // CRITICAL FIX: checkAuthStatus dependency'sini kaldırdık - sürekli re-render'a neden oluyordu
    useEffect(() => {
        // Sadece authenticated user varsa auth check yap
        if (!user || !isAuthenticated) {
            console.log('🚫 TabLayout: User veya authentication yok, auth check atlanıyor...'); // Debug log
            return;
        }

        // İlk yüklemede hemen kontrol et
        console.log('🔄 TabLayout: İlk auth check başlatılıyor...'); // Debug log
        checkAuthStatus();

        // OPTIMIZATION: Her dakika yerine 5 dakikada bir kontrol et
        // FIX: Dependency array [] yapılarak loop önlendi.
        const interval = setInterval(() => {
            console.log('[AUTH] interval task running...', interval);
            // Check auth status without causing re-renders if nothing changed
            checkAuthStatus();
        }, 5 * 60 * 1000); // 5 dakika

        console.log("[AUTH] interval started", interval);

        return () => {
            console.log("[AUTH] interval cleared", interval);
            clearInterval(interval);
        };
    }, []); // CRITICAL FIX: Empty dependency array prevents re-creation of interval on user update

    // OPTIMIZATION: Recovery data sadece user değiştiğinde ve henüz initialize edilmemişse yüklensin
    useEffect(() => {
        if (user && !recoveryInitialized) {
            console.log('🔄 User changed, recovery data will be loaded automatically');
            // Recovery data otomatik olarak useTableOrdersRecovery hook'unda yüklenecek
        }
    }, [user, recoveryInitialized]);

    // Use isAuthReady explicitly to ensure auth hydration is complete
    // If not ready, show spinner.
    if (!isAuthReady || isLoading) {
        console.log('TabLayout: Loading state (isAuthReady: ' + isAuthReady + '), showing spinner');
        return (
            <View style={{ flex: 1, justifyContent: 'center', alignItems: 'center' }}>
                <ActivityIndicator size="large" color="#007AFF" />
            </View>
        );
    }

    // Kullanıcı giriş yapmamışsa veya oturumu sona ermişse login sayfasına yönlendir
    if (!isAuthenticated || !user) {
        console.log('TabLayout: Not authenticated or no user, redirecting to login');
        return <Redirect href="/(auth)/login" />;
    }

    console.log('TabLayout: Authenticated, showing cashier tabs for user:', user.email, 'role:', user.role);

    return (
        <Tabs
            screenOptions={{
                tabBarActiveTintColor: '#007AFF',
                tabBarInactiveTintColor: '#8E8E93',
                tabBarStyle: { paddingBottom: 5 },
                headerShown: true
            }}
        >
            <Tabs.Screen
                name="cash-register"
                options={{
                    title: 'Kasse',
                    tabBarIcon: ({ color }) => <Ionicons name="cash-outline" size={24} color={color} />,
                }}
            />
            <Tabs.Screen
                name="settings"
                options={{
                    title: 'Einstellungen',
                    tabBarIcon: ({ color }) => <Ionicons name="settings-outline" size={24} color={color} />,
                }}
            />
        </Tabs>
    );
} 