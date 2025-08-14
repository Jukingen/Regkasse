import { Ionicons } from '@expo/vector-icons';
import { Tabs, Redirect } from 'expo-router';
import React, { useState, useEffect } from 'react';
import { View, ActivityIndicator } from 'react-native';

import { useAuth } from '../../contexts/AuthContext';

export default function TabLayout() {
    const { isAuthenticated, isLoading, user, checkAuthStatus } = useAuth();

    // Debug logları
    console.log('TabLayout render:', { isAuthenticated, isLoading, user: user?.email, role: user?.role });
    console.log('TabLayout: Full user object:', user);

    // Periyodik olarak oturum durumunu kontrol et
    useEffect(() => {
        // İlk yüklemede hemen kontrol etme, biraz bekle
        const initialCheck = setTimeout(() => {
            checkAuthStatus();
        }, 1000); // 1 saniye bekle

        const interval = setInterval(() => {
            checkAuthStatus();
        }, 60000); // Her dakika kontrol et

        return () => {
            clearTimeout(initialCheck);
            clearInterval(interval);
        };
    }, []);

    if (isLoading) {
        console.log('TabLayout: Loading state, showing spinner');
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
            <Tabs.Screen
                name="invoices"
                options={{
                    title: 'Rechnungen',
                    tabBarIcon: ({ color }) => <Ionicons name="document-text-outline" size={24} color={color} />,
                }}
            />
        </Tabs>
    );
} 