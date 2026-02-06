import { Slot, Redirect } from 'expo-router';
import { View, ActivityIndicator } from 'react-native';
import { useAuth } from '../../contexts/AuthContext';
import React from 'react';

export default function AuthLayout() {
    const { isAuthenticated, isLoading, user } = useAuth();

    console.log('🔐 AUTH LAYOUT: Checking auth state:', { isAuthenticated, isLoading });

    if (isLoading) {
        return (
            <View style={{ flex: 1, justifyContent: 'center', alignItems: 'center' }}>
                <ActivityIndicator size="large" color="#007AFF" />
            </View>
        );
    }

    // Eğer kullanıcı giriş yapmışsa ana sayfaya yönlendir
    if (isAuthenticated && user) {
        console.log('AuthLayout: User authenticated, redirecting to tabs');
        return <Redirect href="/(tabs)/cash-register" />;
    }

    console.log('🔐 AUTH LAYOUT: Rendering Slot');
    return <Slot />;
}