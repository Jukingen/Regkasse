import { Slot, Redirect, useSegments } from 'expo-router';
import React from 'react';
import { View } from 'react-native';

import { useAuth } from '../../contexts/AuthContext';
import { WaveLoader } from '../../src/components/common/WaveLoader';
import {
  needsPosCashRegisterSelection,
  POS_CASH_REGISTER_SELECT_HREF,
} from '../../utils/posCashRegister';
import { isPosAllowedRole } from '../../utils/posRoleGuard';

export default function AuthLayout() {
  const segments = useSegments();
  const { isAuthenticated, isLoading, user, logout } = useAuth();

  const isOnLoginScreen = Array.isArray(segments) && (segments as string[]).includes('login');
  const isOnChangePasswordScreen =
    Array.isArray(segments) && (segments as string[]).includes('change-password');
  const isOnLicenseExpiredScreen =
    Array.isArray(segments) && (segments as string[]).includes('license-expired');

  if (isLoading && !isOnLoginScreen && !isOnChangePasswordScreen && !isOnLicenseExpiredScreen) {
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

  // Mandant license lockdown screen must stay reachable after session clear / denial.
  if (isOnLicenseExpiredScreen) {
    return <Slot />;
  }

  if (isAuthenticated && user) {
    if (needsPosCashRegisterSelection(user.currentCashRegisterId)) {
      return <Redirect href={POS_CASH_REGISTER_SELECT_HREF} />;
    }
    return <Redirect href="/(tabs)/cash-register" />;
  }

  return <Slot />;
}
