/**
 * Apply inbound deep links (email / push / QR) to Expo Router screens.
 * Complements expo-router's built-in path matching for brand remaps
 * (e.g. regkasse://tenant/{slug} → /customer?tenant=…).
 */
import * as Linking from 'expo-linking';
import { useRouter } from 'expo-router';
import { useEffect, useRef } from 'react';

import { resolveDeepLink } from '@/services/linking/deepLinking';

function isCustomerSurface(): boolean {
  return (process.env.EXPO_PUBLIC_APP_SURFACE ?? '').trim().toLowerCase() === 'customer';
}

export function useDeepLinkNavigation(): void {
  const router = useRouter();
  const linkingUrl = Linking.useLinkingURL();
  const lastHandled = useRef<string | null>(null);

  useEffect(() => {
    if (!linkingUrl || linkingUrl === lastHandled.current) return;

    const intent = resolveDeepLink(linkingUrl);
    if (!intent || intent.type === 'unhandled') return;

    lastHandled.current = linkingUrl;
    const customerSurface = isCustomerSurface();

    switch (intent.type) {
      case 'customerTenant':
        router.replace({
          pathname: '/customer',
          params: { tenant: intent.slug },
        });
        break;
      case 'customerHome':
        if (intent.slug) {
          router.replace({
            pathname: '/customer',
            params: { tenant: intent.slug },
          });
        } else if (customerSurface) {
          router.replace('/customer');
        }
        break;
      case 'orderTracker': {
        const params: Record<string, string> = {};
        if (intent.tenant) params.tenant = intent.tenant;
        if (intent.orderNumber) params.order = intent.orderNumber;
        if (intent.phone) params.phone = intent.phone;
        router.push({ pathname: '/order-tracker', params });
        break;
      }
      case 'login':
        if (!customerSurface) {
          router.replace('/(auth)/login');
        }
        break;
      default:
        break;
    }
  }, [linkingUrl, router]);
}
