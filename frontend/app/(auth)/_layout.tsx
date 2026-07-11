import { Slot, Redirect, useSegments } from 'expo-router';
import { View } from 'react-native';

import { WaveLoader } from '../../src/components/common/WaveLoader';
import { useAuth } from '../../contexts/AuthContext';
import { isPosAllowedRole } from '../../utils/posRoleGuard';
import React from 'react';

export default function AuthLayout() {
    const segments = useSegments();
    const { isAuthenticated, isLoading, user, logout } = useAuth();

    const isOnLoginScreen = Array.isArray(segments) && segments.includes('login');
    const isOnChangePasswordScreen =
        Array.isArray(segments) && segments.includes('change-password');

    if (isLoading && !isOnLoginScreen && !isOnChangePasswordScreen) {
        return (
            <View style={{ flex: 1, justifyContent: 'center', alignItems: 'center' }}>
                <WaveLoader size={32} color="#007AFF" />
            </View>
        );
    }

    if (isAuthenticated && user && !isPosAllowedRole(user.role, user.roles)) {
        logout();
        return <Slot />;
    }

    if (isAuthenticated && user?.mustChangePasswordOnNextLogin) {
        if (!isOnChangePasswordScreen) {
            return <Redirect href="/(auth)/change-password" />;
        }
        return <Slot />;
    }

    if (isAuthenticated && user) {
        return <Redirect href="/(tabs)/cash-register" />;
    }

    return <Slot />;
}
