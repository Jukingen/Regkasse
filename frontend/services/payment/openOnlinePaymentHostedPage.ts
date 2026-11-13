import * as WebBrowser from 'expo-web-browser';
import { Platform } from 'react-native';

import { logger } from '../../lib/logger';
import { isWeb, safeWindow } from '../../utils/platformUtils';

export type HostedPageResult =
  | { kind: 'callback'; url: string }
  | { kind: 'dismissed' }
  | { kind: 'opened' }
  | { kind: 'failed' };

export function createOnlinePaymentReturnUrl(): string {
  if (isWeb) {
    const origin = safeWindow()?.location?.origin;
    return origin ? `${origin}/payment/result` : '/payment/result';
  }
  return 'regkasse://payment-result';
}

function isHttpOrHttps(url: string): boolean {
  try {
    const u = new URL(url);
    return u.protocol === 'http:' || u.protocol === 'https:';
  } catch {
    return false;
  }
}

/**
 * Native: in-app browser (auth session) that returns on deep-link callback.
 * Web: new tab; caller keeps polling. Popup-blocked → same-tab redirect.
 */
export async function openOnlinePaymentHostedPage(
  hostedUrl: string,
  returnUrl: string
): Promise<HostedPageResult> {
  if (!isHttpOrHttps(hostedUrl)) {
    logger.error('online_payment.hosted_url_invalid', {});
    return { kind: 'failed' };
  }

  logger.info('online_payment.hosted_page_open', {
    platform: Platform.OS,
  });

  if (isWeb) {
    const w = safeWindow();
    if (!w) return { kind: 'failed' };
    try {
      const popup = w.open(hostedUrl, '_blank', 'noopener,noreferrer');
      if (popup) {
        return { kind: 'opened' };
      }
      w.location.assign(hostedUrl);
      return { kind: 'opened' };
    } catch {
      logger.warn('online_payment.hosted_page_web_failed', {});
      return { kind: 'failed' };
    }
  }

  try {
    const result = await WebBrowser.openAuthSessionAsync(hostedUrl, returnUrl);
    if (result.type === 'success' && 'url' in result && typeof result.url === 'string') {
      logger.info('online_payment.hosted_page_callback', {});
      return { kind: 'callback', url: result.url };
    }
    if (result.type === 'cancel' || result.type === 'dismiss') {
      logger.info('online_payment.hosted_page_dismissed', { resultType: result.type });
      return { kind: 'dismissed' };
    }
    return { kind: 'opened' };
  } catch {
    logger.warn('online_payment.hosted_page_native_failed', {});
    return { kind: 'failed' };
  }
}
