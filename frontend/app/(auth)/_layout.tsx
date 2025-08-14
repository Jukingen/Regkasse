import { Stack, Redirect } from 'expo-router';
import { View, ActivityIndicator } from 'react-native';
import { useAuth } from '../../contexts/AuthContext';
import React from 'react';

interface AuthLayoutProps {
  children: React.ReactNode;
}

export default function AuthLayout({ children }: AuthLayoutProps) {
    const { isAuthenticated, isLoading, user } = useAuth();

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
        console.log('AuthLayout: User details:', { email: user.email, role: user.role });
        return <Redirect href="/(tabs)/cash-register" />;
    }

    return (
        <Stack screenOptions={{ headerShown: false }}>
            {children}
        </Stack>
    );
} 