import * as WebBrowser from 'expo-web-browser';
import { Linking } from 'react-native';

import { isWeb, safeWindow } from './platformUtils';

export type OpenHttpOrHttpsUrlOptions = {
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
 * Opens http(s) URLs: in-app browser on native, new tab on web; falls back to Linking.
 */
export async function openHttpOrHttpsUrl(
  url: string,
  _options?: OpenHttpOrHttpsUrlOptions,
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
  } else {
    try {
      await WebBrowser.openBrowserAsync(url);
      return true;
    } catch {
      /* fall through */
    }
  }

  try {
    if (await Linking.canOpenURL(url)) {
      await Linking.openURL(url);
      return true;
    }
  } catch {
    /* ignore */
  }
  return false;
}

/**
 * Opens mailto: URLs via Linking (native + web).
 */
export async function openMailtoUrl(mailtoUrl: string): Promise<boolean> {
  if (!mailtoUrl.startsWith('mailto:')) {
    return false;
  }
  try {
    if (await Linking.canOpenURL(mailtoUrl)) {
      await Linking.openURL(mailtoUrl);
      return true;
    }
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
