import { Alert } from 'react-native';

import { openHttpOrHttpsUrl, openMailtoUrl } from './openLink';

import {
  allowedOnPlatformHost,
  buildAdminUrl,
  requiresTenant,
  type AdminTarget,
  type AdminTargetContext,
} from '@/constants/adminRoutes';
import { getCurrentTenantSlug } from '@/services/tenant/tenantStorage';

export interface OpenAdminOptions {
  fallbackToMail?: boolean;
  mailtoSubject?: string;
  mailtoBody?: string;
  forceWebBrowser?: boolean;
}

function buildSupportMailtoUrl(subject: string, body: string): string {
  return `mailto:support@regkasse.at?subject=${encodeURIComponent(subject)}&body=${encodeURIComponent(body)}`;
}

function confirmTenantlessNavigation(): Promise<boolean> {
  return new Promise((resolve) => {
    Alert.alert(
      'Mandant erforderlich',
      'Diese Aktion erfordert einen ausgewählten Mandanten. Möchten Sie trotzdem fortfahren?',
      [
        {
          text: 'Abbrechen',
          style: 'cancel',
          onPress: () => {
            resolve(false);
          },
        },
        {
          text: 'Fortfahren',
          onPress: () => {
            resolve(true);
          },
        },
      ]
    );
  });
}

export async function openAdmin(
  target: AdminTarget,
  context?: AdminTargetContext,
  options?: OpenAdminOptions
): Promise<boolean> {
  try {
    const url = buildAdminUrl(target, context);
    const tenantSlug = await getCurrentTenantSlug();
    const requiresTenantContext = requiresTenant(target);

    if (requiresTenantContext && !tenantSlug && !context?.forcePlatformHost) {
      const continueAnyway = await confirmTenantlessNavigation();
      if (!continueAnyway) {
        return false;
      }
    }

    const normalizedTenantSlug = tenantSlug?.trim().toLowerCase();
    const isPlatformHost = !normalizedTenantSlug || normalizedTenantSlug === 'admin';
    const isAllowedOnPlatform = allowedOnPlatformHost(target);

    if (isPlatformHost && !isAllowedOnPlatform && !context?.forcePlatformHost) {
      Alert.alert(
        'Nicht verfügbar',
        'Diese Funktion ist im Plattform-Modus nicht verfügbar. Bitte wählen Sie zuerst einen Mandanten.'
      );
      return false;
    }

    const success = await openHttpOrHttpsUrl(url, { forceWebBrowser: options?.forceWebBrowser });

    if (!success && options?.fallbackToMail) {
      const mailtoUrl = buildSupportMailtoUrl(
        options.mailtoSubject ?? 'Lizenzverlängerung',
        options.mailtoBody ?? ''
      );
      return await openMailtoUrl(mailtoUrl);
    }

    return success;
  } catch (error) {
    console.error('Failed to open admin:', error);
    return false;
  }
}

export async function openLicenseExtension(machineHash: string): Promise<boolean> {
  return await openAdmin(
    'licenseExtend',
    {
      machineHash,
      intent: 'extend',
    },
    {
      fallbackToMail: true,
      mailtoSubject: 'Lizenzverlängerung',
      mailtoBody: `Bitte verlängern Sie meine Lizenz.\n\nMaschinen-Fingerprint: ${machineHash}\n\nVielen Dank.`,
    }
  );
}
