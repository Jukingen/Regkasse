import { useRouter, useSegments } from 'expo-router';
import React, { useEffect } from 'react';
import { View, ActivityIndicator } from 'react-native';

import { useAuth } from '../contexts/AuthContext';

export function ProtectedRoute({ children }: { children: React.ReactNode }) {
    const { isAuthenticated, isLoading } = useAuth();
    const segments = useSegments();
    const router = useRouter();

    useEffect(() => {
        if (isLoading) return;

        const inAuthGroup = segments[0] === '(auth)';
        const inTabsGroup = segments[0] === '(tabs)';

        if (!isAuthenticated && !inAuthGroup) {
            // Kullanıcı giriş yapmamış ve auth grubu dışında bir sayfaya erişmeye çalışıyor
            router.replace('/(auth)/login');
        } else if (isAuthenticated && inAuthGroup) {
            // Kullanıcı giriş yapmış ve auth grubuna erişmeye çalışıyor
            router.replace('/(tabs)');
        }
    }, [isAuthenticated, segments, isLoading]);

    if (isLoading) {
        return (
            <View style={{ flex: 1, justifyContent: 'center', alignItems: 'center' }}>
                <ActivityIndicator size="large" color="#007AFF" />
            </View>
        );
    }

    return <>{children}</>;
} 