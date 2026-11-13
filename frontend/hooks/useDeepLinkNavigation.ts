/**
 * Apply inbound deep links (email / push / QR) to Expo Router screens.
 * POS: regkasse://tenant/{slug} stores the mandant and opens login when signed out.
 * Customer surface: the same URL still opens /customer.
 */
import * as Linking from 'expo-linking';
import { useRouter } from 'expo-router';
import { useEffect, useRef } from 'react';

import { useAuth } from '@/contexts/AuthContext';
import { resolveDeepLink } from '@/services/linking/deepLinking';
import { isCustomerAppSurface } from '@/services/linking/posTenantDeepLink';
import { isRedirectStatusCancelled, isRedirectStatusFailed } from '@/services/payment/parsePaymentResultParams';
import { bootstrapPosTenantSlug } from '@/services/verticalProfiles/posTenantBootstrap';
import { onlinePaymentStoreActions } from '@/stores/onlinePaymentStore';

export function useDeepLinkNavigation(): void {
  const router = useRouter();
  const { isAuthenticated, isAuthReady } = useAuth();
  const linkingUrl = Linking.useLinkingURL();
  const lastHandled = useRef<string | null>(null);

  useEffect(() => {
    if (!linkingUrl || linkingUrl === lastHandled.current) return;

    const intent = resolveDeepLink(linkingUrl);
    if (!intent || intent.type === 'unhandled') return;
    if (intent.type === 'customerTenant' && !isAuthReady) return;

    lastHandled.current = linkingUrl;
    const customerSurface = isCustomerAppSurface();

    switch (intent.type) {
      case 'customerTenant':
        if (customerSurface) {
          router.replace({
            pathname: '/customer',
            params: { tenant: intent.slug },
          });
          break;
        }
        if (!isAuthenticated) {
          void bootstrapPosTenantSlug(intent.slug).finally(() => {
            router.replace({
              pathname: '/(auth)/login',
              params: { tenant: intent.slug },
            });
          });
          break;
        }
        router.replace('/(tabs)/cash-register');
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
      case 'onlinePaymentCallback':
        if (isRedirectStatusFailed(intent.gatewayStatus)) {
          onlinePaymentStoreActions.fail('ONLINE_PAYMENT_FAILED');
        } else if (isRedirectStatusCancelled(intent.gatewayStatus)) {
          onlinePaymentStoreActions.cancel();
        } else {
          onlinePaymentStoreActions.markCallbackReceived();
        }
        if (!customerSurface) {
          router.replace('/(tabs)/cash-register');
        }
        break;
      default:
        break;
    }
  }, [isAuthReady, isAuthenticated, linkingUrl, router]);
}
