import type { AdminActiveSession } from '@/api/manual/adminSessions';

function isPosSession(session: Pick<AdminActiveSession, 'clientApp' | 'platformLabel'>): boolean {
  const app = session.clientApp?.trim().toLowerCase();
  if (app === 'pos') return true;
  return (session.platformLabel ?? '').trim().toUpperCase().startsWith('POS');
}

function displayOs(os: string): string {
  if (os === 'macOS' || os === 'Mac OS X' || os === 'Macintosh') return 'OS X';
  return os;
}

/** Device cell: `POS (Android)` / `POS (iOS)` / `POS (Web)` or `Windows - Chrome`. */
export function formatSessionDevice(
  session: Pick<AdminActiveSession, 'clientApp' | 'platformLabel' | 'deviceName' | 'os' | 'browser'>,
  unknownLabel: string
): string {
  if (isPosSession(session)) {
    return session.platformLabel?.trim() || session.deviceName?.trim() || unknownLabel;
  }

  const os = session.os?.trim();
  const browser = session.browser?.trim();
  if (os && browser) return `${displayOs(os)} - ${browser}`;
  if (os) return displayOs(os);
  if (browser) return browser;
  return session.platformLabel?.trim() || session.deviceName?.trim() || unknownLabel;
}
