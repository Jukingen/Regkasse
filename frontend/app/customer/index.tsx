/**
 * Single customer mobile app for all tenants (shared platform).
 * Expo route: /customer
 *
 * POS cash-register entry remains /(auth)/login via app/index.tsx unless
 * EXPO_PUBLIC_APP_SURFACE=customer.
 */

import React, { useCallback, useEffect, useState } from 'react';
import { ActivityIndicator, View } from 'react-native';
import { TenantApp } from '../../components/customerApp/TenantApp';
import { TenantSelector } from '../../components/customerApp/TenantSelector';
import {
  clearCustomerTenantSlug,
  getTenantSlug,
  setCustomerTenantSlug,
} from '../../services/customerApp/customerTenantStorage';
import {
  loadTenant,
  type CustomerTenantProfile,
} from '../../services/customerApp/publicTenantApi';

export default function CustomerAppEntry() {
  const [tenant, setTenant] = useState<CustomerTenantProfile | null>(null);
  const [booting, setBooting] = useState(true);
  const [loading, setLoading] = useState(false);
  const [errorKey, setErrorKey] = useState<string | null>(null);

  const selectSlug = useCallback(async (slug: string) => {
    setLoading(true);
    setErrorKey(null);
    try {
      const profile = await loadTenant(slug);
      await setCustomerTenantSlug(profile.slug);
      setTenant(profile);
    } catch (err: unknown) {
      setTenant(null);
      const status =
        err && typeof err === 'object' && 'response' in err
          ? (err as { response?: { status?: number } }).response?.status
          : undefined;
      setErrorKey(status === 404 ? 'not_found' : 'load_failed');
    } finally {
      setLoading(false);
      setBooting(false);
    }
  }, []);

  useEffect(() => {
    let cancelled = false;
    void (async () => {
      const slug = await getTenantSlug();
      if (cancelled) return;
      if (slug) {
        await selectSlug(slug);
      } else {
        setBooting(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [selectSlug]);

  const onChangeTenant = useCallback(() => {
    void clearCustomerTenantSlug();
    setTenant(null);
    setErrorKey(null);
  }, []);

  if (booting) {
    return (
      <View style={{ flex: 1, justifyContent: 'center', alignItems: 'center' }}>
        <ActivityIndicator size="large" color="#2563eb" />
      </View>
    );
  }

  if (!tenant) {
    return (
      <TenantSelector onSelect={(slug) => void selectSlug(slug)} isLoading={loading} errorKey={errorKey} />
    );
  }

  return <TenantApp tenant={tenant} onChangeTenant={onChangeTenant} />;
}
