import { useRouter, useSegments } from 'expo-router';
import React, { useEffect } from 'react';
import { View, ActivityIndicator } from 'react-native';

import { useAuth } from '../contexts/AuthContext';
import { isPosAllowedRole } from '../utils/posRoleGuard';

interface ProtectedRouteProps {
    children: React.ReactNode;
    /** Opsiyonel: izin verilen roller. Verilmezse sadece authentication kontrolü yapılır. */
    allowedRoles?: string[];
}

export function ProtectedRoute({ children, allowedRoles }: ProtectedRouteProps) {
    const { isAuthenticated, isLoading, user, logout } = useAuth();
    const segments = useSegments();
    const router = useRouter();

    useEffect(() => {
        if (isLoading) return;

        const inAuthGroup = segments[0] === '(auth)';

        if (!isAuthenticated && !inAuthGroup) {
            router.replace('/(auth)/login');
            return;
        }

        // Rol whitelist kontrolü: allowedRoles verilmişse kontrol et
        if (isAuthenticated && allowedRoles && user) {
            const hasAllowedRole = allowedRoles.some(
                (r) => r === user.role || user.roles?.includes(r)
            );
            if (!hasAllowedRole) {
                console.warn('ProtectedRoute: role not in allowedRoles, logging out. role:', user.role);
                logout();
                return;
            }
        }

        if (isAuthenticated && inAuthGroup) {
            // POS rol kontrolü: yetkisiz roller tabs'a yönlendirilmez
            if (!isPosAllowedRole(user?.role, user?.roles)) {
                logout();
                return;
            }
            router.replace('/(tabs)/cash-register');
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
