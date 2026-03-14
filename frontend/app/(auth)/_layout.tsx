import { Slot, Redirect, useSegments } from 'expo-router';
import { View, ActivityIndicator } from 'react-native';
import { useAuth } from '../../contexts/AuthContext';
import { isPosAllowedRole } from '../../utils/posRoleGuard';
import React from 'react';

export default function AuthLayout() {
    const segments = useSegments();
    const { isAuthenticated, isLoading, user, logout } = useAuth();

    const isOnLoginScreen = Array.isArray(segments) && segments.includes('login');

    console.log('🔐 AUTH LAYOUT: Checking auth state:', { isAuthenticated, isLoading, isOnLoginScreen });

    // Show full-screen loader only when loading AND not on login screen. When user is on login
    // and taps "Log In", context isLoading becomes true; we must keep Slot mounted so the
    // login screen can show its own loading/error state instead of unmounting and losing it.
    if (isLoading && !isOnLoginScreen) {
        return (
            <View style={{ flex: 1, justifyContent: 'center', alignItems: 'center' }}>
                <ActivityIndicator size="large" color="#007AFF" />
            </View>
        );
    }

    // Kullanıcı giriş yapmış ama POS'a yetkisiz rolle girmeye çalışıyorsa: logout yap, login'de tut
    if (isAuthenticated && user && !isPosAllowedRole(user.role, user.roles)) {
        console.warn('AuthLayout: authenticated user has no POS access, logging out. role:', user.role);
        logout();
        return <Slot />;
    }

    // Kullanıcı giriş yapmış ve POS yetkisi var → tabs'a yönlendir
    if (isAuthenticated && user) {
        console.log('AuthLayout: User authenticated with POS role, redirecting to tabs');
        return <Redirect href="/(tabs)/cash-register" />;
    }

    console.log('🔐 AUTH LAYOUT: Rendering Slot');
    return <Slot />;
}