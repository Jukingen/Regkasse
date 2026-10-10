/**
 * Deep-link bridge: cashregister://tenant/{slug} or regkasse://tenant/{slug}.
 * Customer surface keeps /customer. POS stores the slug and opens login when signed out.
 */
import { Redirect, useLocalSearchParams } from 'expo-router';
import React, { useEffect, useState } from 'react';
import { View } from 'react-native';

import { useAuth } from '@/contexts/AuthContext';
import { normalizeCustomerTenantSlug } from '@/services/customerApp/customerTenantSlug';
import { isCustomerAppSurface } from '@/services/linking/posTenantDeepLink';
import { bootstrapPosTenantSlug } from '@/services/verticalProfiles/posTenantBootstrap';
import { WaveLoader } from '@/src/components/common/WaveLoader';

export default function TenantDeepLinkBridge() {
  const { slug } = useLocalSearchParams<{ slug?: string | string[] }>();
  const raw = typeof slug === 'string' ? slug : Array.isArray(slug) ? slug[0] : null;
  const normalized = normalizeCustomerTenantSlug(raw);
  const { isAuthenticated, isAuthReady } = useAuth();
  const customerSurface = isCustomerAppSurface();
  const [bootstrapped, setBootstrapped] = useState(false);

  useEffect(() => {
    if (customerSurface || !normalized || !isAuthReady || isAuthenticated) return;
    let cancelled = false;
    void bootstrapPosTenantSlug(normalized).finally(() => {
      if (!cancelled) setBootstrapped(true);
    });
    return () => {
      cancelled = true;
    };
  }, [customerSurface, isAuthReady, isAuthenticated, normalized]);

  if (!normalized) {
    return <Redirect href={customerSurface ? '/customer' : '/(auth)/login'} />;
  }

  if (customerSurface) {
    return <Redirect href={{ pathname: '/customer', params: { tenant: normalized } }} />;
  }

  if (!isAuthReady || (!isAuthenticated && !bootstrapped)) {
    return (
      <View style={{ flex: 1, justifyContent: 'center', alignItems: 'center' }}>
        <WaveLoader size={32} color="#007AFF" />
      </View>
    );
  }

  if (!isAuthenticated) {
    return <Redirect href={{ pathname: '/(auth)/login', params: { tenant: normalized } }} />;
  }

  return <Redirect href="/(tabs)/cash-register" />;
}
