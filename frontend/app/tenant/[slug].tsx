/**
 * Deep-link bridge: cashregister://tenant/{slug} or regkasse://tenant/{slug}
 * → customer surface with tenant query (Expo Router path match).
 */
import { Redirect, useLocalSearchParams } from 'expo-router';
import React from 'react';

import { normalizeCustomerTenantSlug } from '@/services/customerApp/customerTenantSlug';

export default function TenantDeepLinkBridge() {
  const { slug } = useLocalSearchParams<{ slug?: string }>();
  const normalized = normalizeCustomerTenantSlug(
    typeof slug === 'string' ? slug : Array.isArray(slug) ? slug[0] : null
  );

  if (normalized) {
    return <Redirect href={{ pathname: '/customer', params: { tenant: normalized } }} />;
  }

  return <Redirect href="/customer" />;
}
