import React from 'react';
import { Tabs, Redirect } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { useAuth } from '../../contexts/AuthContext';
import { View, ActivityIndicator } from 'react-native';
import { useEffect } from 'react';

export default function TabLayout() {
    const { isAuthenticated, isLoading, user, checkAuthStatus } = useAuth();

    // Periyodik olarak oturum durumunu kontrol et
    useEffect(() => {
        const interval = setInterval(() => {
            checkAuthStatus();
        }, 60000); // Her dakika kontrol et

        return () => clearInterval(interval);
    }, []);

    if (isLoading) {
        return (
            <View style={{ flex: 1, justifyContent: 'center', alignItems: 'center' }}>
                <ActivityIndicator size="large" color="#007AFF" />
            </View>
        );
    }

    // Kullanıcı giriş yapmamışsa veya oturumu sona ermişse login sayfasına yönlendir
    if (!isAuthenticated || !user) {
        return <Redirect href="/(auth)/login" />;
    }

    // Sadece admin ve manager rolüne sahip kullanıcılar tüm menülere erişebilir
    const isAdminOrManager = user.role === 'admin' || user.role === 'manager';

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
            {isAdminOrManager && (
                <>
                    <Tabs.Screen
                        name="products"
                        options={{
                            title: 'Produkte',
                            tabBarIcon: ({ color }) => <Ionicons name="cube-outline" size={24} color={color} />,
                        }}
                    />
                    <Tabs.Screen
                        name="customers"
                        options={{
                            title: 'Kunden',
                            tabBarIcon: ({ color }) => <Ionicons name="people-outline" size={24} color={color} />,
                        }}
                    />
                    <Tabs.Screen
                        name="reports"
                        options={{
                            title: 'Berichte',
                            tabBarIcon: ({ color }) => <Ionicons name="bar-chart-outline" size={24} color={color} />,
                        }}
                    />
                </>
            )}
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