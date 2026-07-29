import { Ionicons } from '@expo/vector-icons';
import { StatusBar } from 'expo-status-bar';
import { useRouter } from 'expo-router';
import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Alert, Pressable, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { SoftColors, SoftRadius, SoftSpacing, SoftTypography } from '../../../constants/SoftTheme';
import {
  buildLicenseRenewalMailtoUrl,
  LICENSE_SUPPORT_EMAIL,
} from '../../../constants/licenseRenewal';
import { openAdmin } from '../../../utils/openAdmin';
import { openMailtoUrl } from '../../../utils/openLink';
import {
  clearLicenseLockoutSnapshot,
  loadLicenseLockoutSnapshot,
} from '../../../utils/licenseLockoutSnapshot';

/**
 * Full-screen POS lockout when mandant license is Locked/Archived (compliance block).
 */
export default function LicenseExpiredScreen() {
  const { t } = useTranslation('license');
  const router = useRouter();
  const [daysOverdue, setDaysOverdue] = useState<number | null>(null);

  useEffect(() => {
    let cancelled = false;
    void (async () => {
      const snapshot = await loadLicenseLockoutSnapshot();
      if (cancelled) return;
      if (snapshot) {
        setDaysOverdue(snapshot.daysOverdue);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const openRenew = useCallback(async () => {
    const ok = await openAdmin('mandantLicense', undefined, {
      fallbackToMail: true,
      mailtoSubject: 'Lizenzverlängerung',
      mailtoBody: t('expiredScreen.mailtoBody'),
    });
    if (!ok) {
      Alert.alert(t('renewOpenFailedTitle'), t('renewOpenFailedBody'));
    }
  }, [t]);

  const openSupport = useCallback(async () => {
    const ok = await openMailtoUrl(
      buildLicenseRenewalMailtoUrl({
        machineHash: null,
        daysRemaining: 0,
        isTrial: false,
        isExpired: true,
      })
    );
    if (!ok) {
      Alert.alert(t('renewOpenFailedTitle'), t('renewOpenFailedMailBody'));
    }
  }, [t]);

  const backToLogin = useCallback(async () => {
    await clearLicenseLockoutSnapshot();
    router.replace('/(auth)/login');
  }, [router]);

  const subtitle =
    daysOverdue != null && daysOverdue > 0
      ? t('expiredScreen.subtitleDays', { days: daysOverdue })
      : t('expiredScreen.subtitleGeneric');

  return (
    <SafeAreaView style={styles.safe} edges={['top', 'bottom']}>
      <StatusBar style="dark" />
      <View style={styles.container}>
        <View style={styles.iconWrap} accessibilityElementsHidden>
          <Ionicons name="lock-closed" size={56} color={SoftColors.error} />
        </View>

        <Text style={styles.title}>{t('expiredScreen.title')}</Text>
        <Text style={styles.subtitle}>{subtitle}</Text>

        <View style={styles.infoBox}>
          <Text style={styles.infoText}>{t('expiredScreen.complianceNote')}</Text>
          <Text style={styles.infoText}>{t('expiredScreen.renewHint')}</Text>
        </View>

        <Pressable
          style={({ pressed }) => [styles.primaryButton, pressed && styles.pressed]}
          onPress={() => {
            void openRenew();
          }}
          accessibilityRole="button"
          accessibilityLabel={t('expiredScreen.renewCta')}
        >
          <Ionicons name="key-outline" size={20} color={SoftColors.textInverse} />
          <Text style={styles.primaryButtonText}>{t('expiredScreen.renewCta')}</Text>
        </Pressable>

        <Pressable
          style={({ pressed }) => [styles.secondaryButton, pressed && styles.pressed]}
          onPress={() => {
            void openSupport();
          }}
          accessibilityRole="button"
          accessibilityLabel={t('expiredScreen.supportCta')}
        >
          <Ionicons name="mail-outline" size={20} color={SoftColors.info} />
          <Text style={styles.secondaryButtonText}>{t('expiredScreen.supportCta')}</Text>
        </Pressable>

        <Text style={styles.supportEmail}>{LICENSE_SUPPORT_EMAIL}</Text>

        <Pressable
          style={styles.backLink}
          onPress={() => {
            void backToLogin();
          }}
          accessibilityRole="button"
          accessibilityLabel={t('expiredScreen.backToLogin')}
        >
          <Text style={styles.backLinkText}>{t('expiredScreen.backToLogin')}</Text>
        </Pressable>
      </View>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: {
    flex: 1,
    backgroundColor: SoftColors.bgSecondary,
  },
  container: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    paddingHorizontal: SoftSpacing.lg,
    paddingVertical: SoftSpacing.xl,
  },
  iconWrap: {
    marginBottom: SoftSpacing.lg,
    width: 88,
    height: 88,
    borderRadius: SoftRadius.full,
    backgroundColor: SoftColors.errorBg,
    alignItems: 'center',
    justifyContent: 'center',
  },
  title: {
    ...SoftTypography.h2,
    fontSize: 24,
    lineHeight: 30,
    color: SoftColors.error,
    textAlign: 'center',
    marginBottom: SoftSpacing.sm,
  },
  subtitle: {
    ...SoftTypography.body,
    color: SoftColors.textSecondary,
    textAlign: 'center',
    marginBottom: SoftSpacing.lg,
  },
  infoBox: {
    backgroundColor: SoftColors.errorBg,
    borderWidth: 1,
    borderColor: SoftColors.error,
    padding: SoftSpacing.md,
    borderRadius: SoftRadius.sm,
    marginBottom: SoftSpacing.lg,
    width: '100%',
    maxWidth: 480,
  },
  infoText: {
    ...SoftTypography.bodySmall,
    color: SoftColors.error,
    marginBottom: SoftSpacing.xs,
  },
  primaryButton: {
    backgroundColor: SoftColors.error,
    paddingVertical: SoftSpacing.md,
    paddingHorizontal: SoftSpacing.lg,
    borderRadius: SoftRadius.sm,
    width: '100%',
    maxWidth: 480,
    alignItems: 'center',
    justifyContent: 'center',
    flexDirection: 'row',
    gap: SoftSpacing.sm,
    marginBottom: SoftSpacing.sm,
  },
  primaryButtonText: {
    ...SoftTypography.h3,
    color: SoftColors.textInverse,
  },
  secondaryButton: {
    paddingVertical: SoftSpacing.md,
    paddingHorizontal: SoftSpacing.lg,
    borderRadius: SoftRadius.sm,
    width: '100%',
    maxWidth: 480,
    alignItems: 'center',
    justifyContent: 'center',
    flexDirection: 'row',
    gap: SoftSpacing.sm,
    borderWidth: 1,
    borderColor: SoftColors.info,
    backgroundColor: SoftColors.bgCard,
    marginBottom: SoftSpacing.sm,
  },
  secondaryButtonText: {
    ...SoftTypography.h3,
    color: SoftColors.info,
  },
  supportEmail: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
    marginBottom: SoftSpacing.lg,
  },
  backLink: {
    padding: SoftSpacing.sm,
  },
  backLinkText: {
    ...SoftTypography.bodySmall,
    color: SoftColors.textSecondary,
    textDecorationLine: 'underline',
  },
  pressed: {
    opacity: 0.9,
  },
});
