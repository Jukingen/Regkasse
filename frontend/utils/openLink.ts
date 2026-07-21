import * as Linking from 'expo-linking';
import * as WebBrowser from 'expo-web-browser';
import { Platform } from 'react-native';

import { isAndroid, isWeb, safeWindow } from './platformUtils';

export type OpenHttpOrHttpsUrlOptions = {
  /**
   * Native only. When `true` or omitted, open via in-app WebBrowser
   * (SFSafariViewController / Chrome Custom Tabs). When `false`, open
   * in the system browser via `Linking.openURL`.
   */
  forceWebBrowser?: boolean;
};

function isHttpOrHttps(url: string): boolean {
  try {
    const u = new URL(url);
    return u.protocol === 'http:' || u.protocol === 'https:';
  } catch {
    return false;
  }
}

/**
 * System-browser / OS handler fallback.
 * Skips `canOpenURL` for http(s): on Android 11+ it often returns false
 * unless package visibility queries are declared.
 */
async function openViaLinking(url: string): Promise<boolean> {
  try {
    await Linking.openURL(url);
    return true;
  } catch {
    return false;
  }
}

function isSuccessfulBrowserResult(type: string): boolean {
  return type === 'opened' || type === 'cancel' || type === 'dismiss';
}

/**
 * In-app browser. On iOS, dismisses when a deep link returns to the app
 * (Expo Router / `cashregister://` scheme). On Android, warm up Custom Tabs
 * best-effort before open.
 */
async function openViaWebBrowser(url: string): Promise<boolean> {
  const isIos = Platform.OS === 'ios';
  let urlSub: { remove: () => void } | undefined;

  try {
    // iOS: openBrowserAsync resolves when the sheet closes — keep listener
    // active so deep-link returns dismiss SFSafariViewController.
    if (isIos) {
      urlSub = Linking.addEventListener('url', () => {
        void WebBrowser.dismissBrowser().catch(() => undefined);
      });
    }

    if (isAndroid) {
      try {
        await WebBrowser.warmUpAsync();
        await WebBrowser.mayInitWithUrlAsync(url);
      } catch {
        /* warm-up is best-effort */
      }
    }

    const result = await WebBrowser.openBrowserAsync(url, {
      enableDefaultShareMenuItem: true,
      showTitle: true,
      createTask: true,
      useProxyActivity: true,
    });

    return isSuccessfulBrowserResult(result.type);
  } catch {
    return false;
  } finally {
    urlSub?.remove();
    if (isAndroid) {
      void WebBrowser.coolDownAsync().catch(() => undefined);
    }
  }
}

/**
 * Opens http(s) URLs: in-app browser on native (unless forceWebBrowser=false),
 * new tab on web; falls back to Linking.
 */
export async function openHttpOrHttpsUrl(
  url: string,
  options?: OpenHttpOrHttpsUrlOptions
): Promise<boolean> {
  if (!isHttpOrHttps(url)) {
    return false;
  }

  if (isWeb) {
    const w = safeWindow();
    if (w) {
      try {
        w.open(url, '_blank', 'noopener,noreferrer');
        return true;
      } catch {
        /* fall through */
      }
    }
    return await openViaLinking(url);
  }

  const useWebBrowser = options?.forceWebBrowser !== false;

  if (useWebBrowser) {
    const opened = await openViaWebBrowser(url);
    if (opened) {
      return true;
    }
  }

  return await openViaLinking(url);
}

/**
 * Opens mailto: URLs via Linking (native + web).
 */
export async function openMailtoUrl(mailtoUrl: string): Promise<boolean> {
  if (!mailtoUrl.startsWith('mailto:')) {
    return false;
  }
  try {
    await Linking.openURL(mailtoUrl);
    return true;
  } catch {
    /* ignore */
  }
  if (isWeb) {
    const w = safeWindow();
    if (w) {
      try {
        w.location.href = mailtoUrl;
        return true;
      } catch {
        /* ignore */
      }
    }
  }
  return false;
}
