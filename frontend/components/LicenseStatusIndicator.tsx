import { Ionicons } from '@expo/vector-icons';
import React, { useCallback, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  ActivityIndicator,
  Alert,
  Modal,
  Pressable,
  StyleSheet,
  Text,
  View,
  type ViewStyle,
} from 'react-native';

import {
  buildLicenseRenewalMailtoUrl,
  getLicenseExtensionHttpUrl,
  LICENSE_SUPPORT_EMAIL,
} from '../constants/licenseRenewal';
import { SoftColors, SoftRadius, SoftSpacing, SoftTypography } from '../constants/SoftTheme';
import { useLicenseStatus, type LicenseStatus } from '../hooks/useLicenseStatus';
import { openHttpOrHttpsUrl, openMailtoUrl } from '../utils/openLink';

type BadgeTone = 'neutral' | 'green' | 'yellow' | 'orange' | 'red';

/** Maps API snapshot to UX tier (Paid unlimited → green via days sentinel). */
function resolveTone(status: LicenseStatus | null): BadgeTone {
  if (!status) return 'neutral';
  if (status.isExpired) return 'red';
  if (status.daysRemaining <= 7) return 'orange';
  if (status.daysRemaining <= 30) return 'yellow';
  return 'green';
}

function badgeBackground(tone: BadgeTone): string {
  switch (tone) {
    case 'green':
      return '#2E7D32';
    case 'yellow':
      return '#F9A825';
    case 'orange':
      return '#EF6C00';
    case 'red':
      return '#C62828';
    default:
      return SoftColors.textMuted;
  }
}

function badgeForeground(tone: BadgeTone): string {
  switch (tone) {
    case 'yellow':
      return '#3D3229';
    default:
      return SoftColors.textInverse;
  }
}

function formatExpiryDeAt(iso: string | null): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '—';
  try {
    return d.toLocaleString('de-AT', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
    });
  } catch {
    return iso.slice(0, 10);
  }
}

export type LicenseStatusIndicatorProps = {
  badgeAlignSelf?: ViewStyle['alignSelf'];
  /** Larger vertical padding for dense settings lists */
  expandedTouchTarget?: boolean;
};

/**
 * Permanent license tier badge driven by anonymous GET `/api/health/license`
 * (`useLicenseStatus`). Tap opens a detail modal + optional renewal shortcuts.
 */
export function LicenseStatusIndicator({
  badgeAlignSelf = 'flex-end',
  expandedTouchTarget = false,
}: LicenseStatusIndicatorProps) {
  const { t } = useTranslation(['license', 'common']);
  const { status, loading, refetch } = useLicenseStatus();
  const [detailOpen, setDetailOpen] = useState(false);
  const [techDetailsOpen, setTechDetailsOpen] = useState(false);

  const tone = useMemo(() => resolveTone(status), [status]);

  const unlimitedPaid =
    !!status && status.isValid && !status.isTrial && !status.isExpired && !status.expiryDate;

  const badgeLabel = useMemo(() => {
    if (loading && !status) {
      return null;
    }
    if (!status) {
      return t('license:badge.unknown');
    }
    if (status.isExpired) {
      return t('license:badge.expiredShort');
    }
    if (unlimitedPaid) {
      return t('license:badge.unlimitedShort');
    }
    return t('license:badge.daysShort', { count: status.daysRemaining });
  }, [loading, status, unlimitedPaid, t]);

  const hasConfiguredExtensionUrl = useMemo(() => Boolean(getLicenseExtensionHttpUrl()), []);

  const openRenewPrimary = useCallback(async () => {
    let httpUrl = getLicenseExtensionHttpUrl();
    if (httpUrl) {
      if (status?.machineHash) {
        const sep = httpUrl.includes('?') ? '&' : '?';
        httpUrl = `${httpUrl}${sep}machineHash=${encodeURIComponent(status.machineHash)}`;
      }
      const ok = await openHttpOrHttpsUrl(httpUrl);
      if (!ok) {
        Alert.alert(t('license:renewOpenFailedTitle'), t('license:renewOpenFailedBody'));
      }
      return;
    }

    const mailto = buildLicenseRenewalMailtoUrl(status);
    const ok = await openMailtoUrl(mailto);
    if (!ok) {
      Alert.alert(t('license:renewOpenFailedTitle'), t('license:renewOpenFailedMailBody'));
    }
  }, [t, status]);

  const openRenewMail = useCallback(async () => {
    const mailto = buildLicenseRenewalMailtoUrl(status);
    const ok = await openMailtoUrl(mailto);
    if (!ok) {
      Alert.alert(t('license:renewOpenFailedTitle'), t('license:renewOpenFailedMailBody'));
    }
  }, [t, status]);

  const accessibilityLabel = useMemo(() => {
    if (!status) return t('license:badge.unknown');
    if (status.isExpired) return t('license:warningExpired');
    if (status.isTrial) return `${t('license:typeTrial')}, ${status.daysRemaining} Tag(e)`;
    return `${t('license:typePaid')}, ${status.daysRemaining} Tag(e)`;
  }, [status, t]);

  return (
    <>
      <Pressable
        onPress={() => setDetailOpen(true)}
        accessibilityRole="button"
        accessibilityLabel={accessibilityLabel}
        accessibilityHint={t('license:badge.accessibilityHint')}
        style={[
          styles.badgeWrap,
          { alignSelf: badgeAlignSelf },
          expandedTouchTarget ? styles.badgeExpanded : null,
          { backgroundColor: badgeBackground(tone) },
        ]}
      >
        {!status && loading ? (
          <ActivityIndicator size="small" color={SoftColors.textInverse} />
        ) : (
          <View style={styles.badgeInner}>
            <Ionicons
              name={
                tone === 'red'
                  ? 'close-circle'
                  : tone === 'green'
                    ? 'shield-checkmark'
                    : tone === 'yellow' || tone === 'orange'
                      ? 'warning'
                      : 'help-circle-outline'
              }
              size={14}
              color={badgeForeground(tone)}
            />
            {badgeLabel ? (
              <Text style={[styles.badgeText, { color: badgeForeground(tone) }]} numberOfLines={1}>
                {badgeLabel}
              </Text>
            ) : null}
          </View>
        )}
      </Pressable>

      <Modal
        visible={detailOpen}
        animationType="fade"
        transparent
        onRequestClose={() => setDetailOpen(false)}
      >
        <View style={styles.modalRoot}>
          <Pressable
            style={styles.modalBackdropFill}
            onPress={() => setDetailOpen(false)}
            accessibilityRole="button"
            accessibilityLabel={t('license:close')}
          />
          <View style={styles.modalLayerAboveBackdrop} pointerEvents="box-none">
            <View style={styles.sheet}>
            <Text style={styles.sheetTitle}>{t('license:modalTitle')}</Text>

            {!status && loading ? (
              <ActivityIndicator style={{ marginVertical: SoftSpacing.md }} color={SoftColors.accentDark} />
            ) : null}

            {!status && !loading ? (
              <Text style={styles.bodyMuted}>{t('license:loadFailedHint')}</Text>
            ) : null}

            {status ? (
              <>
                <View style={styles.row}>
                  <Text style={styles.label}>{t('license:typeLabel')}</Text>
                  <Text style={styles.valueStrong}>
                    {status.isExpired
                      ? t('license:typeExpired')
                      : (status.licenseType ?? '').trim().toLowerCase() === 'demo'
                        ? t('license:typeDemo')
                        : status.isTrial
                          ? t('license:typeTrial')
                          : t('license:typePaid')}
                  </Text>
                </View>

                <View style={styles.row}>
                  <Text style={styles.label}>{t('license:expiryLabel')}</Text>
                  <Text style={styles.value}>
                    {status.expiryDate ? formatExpiryDeAt(status.expiryDate) : t('license:expiryNone')}
                  </Text>
                </View>

                <View style={styles.row}>
                  <Text style={styles.label}>{t('license:daysRemainingLabel')}</Text>
                  <Text style={styles.value}>
                    {unlimitedPaid ? '—' : t('license:daysRemainingValue', { count: status.daysRemaining })}
                  </Text>
                </View>

                {status.machineHash?.trim() ? (
                  <>
                    <Pressable
                      onPress={() => setTechDetailsOpen((v) => !v)}
                      style={({ pressed }) => [styles.techToggle, pressed && { opacity: 0.75 }]}
                      accessibilityRole="button"
                    >
                      <Text style={styles.techToggleText}>
                        {techDetailsOpen
                          ? t('license:technicalToggleHide')
                          : t('license:technicalToggleShow')}
                      </Text>
                    </Pressable>
                    {techDetailsOpen ? (
                      <View style={styles.techBox}>
                        <Text style={styles.label}>{t('license:machineFingerprintLabel')}</Text>
                        <Text style={styles.techMono} selectable>
                          {status.machineHash.trim()}
                        </Text>
                      </View>
                    ) : null}
                  </>
                ) : null}

                {status.isExpired ? (
                  <View style={styles.warnBox}>
                    <Text style={styles.warnText}>{t('license:warningExpired')}</Text>
                  </View>
                ) : null}
              </>
            ) : null}

            {!loading ? (
              <>
                <Pressable
                  style={({ pressed }) => [styles.ctaPrimary, pressed && { opacity: 0.88 }]}
                  onPress={() => void openRenewPrimary()}
                >
                  <Ionicons
                    name={hasConfiguredExtensionUrl ? 'open-outline' : 'mail-outline'}
                    size={18}
                    color={SoftColors.textInverse}
                  />
                  <Text style={styles.ctaPrimaryText}>{t('license:renewCta')}</Text>
                </Pressable>
                <Pressable
                  onPress={() => void openRenewPrimary()}
                  hitSlop={12}
                  style={({ pressed }) => [styles.ctaHintPressable, pressed && { opacity: 0.7 }]}
                >
                  <Text style={styles.ctaHint}>
                    {t(
                      hasConfiguredExtensionUrl
                        ? 'license:renewPrimaryHintBrowser'
                        : 'license:renewPrimaryHintMail',
                    )}
                  </Text>
                </Pressable>

                {hasConfiguredExtensionUrl ? (
                  <Pressable style={styles.ctaSecondary} onPress={() => void openRenewMail()}>
                    <Ionicons name="mail-outline" size={18} color={SoftColors.accentDark} />
                    <Text style={styles.ctaSecondaryText}>{t('license:renewOpenMail')}</Text>
                  </Pressable>
                ) : null}

                <Text style={styles.contactFoot}>
                  {t('license:contactBody', { email: LICENSE_SUPPORT_EMAIL })}
                </Text>
              </>
            ) : null}

            <View style={styles.modalFooter}>
              <Pressable style={styles.dismissBtnFlex} onPress={() => setDetailOpen(false)}>
                <Text style={styles.dismissText}>{t('license:close')}</Text>
              </Pressable>
              <Pressable
                style={styles.iconRefreshBtn}
                onPress={() => {
                  void refetch();
                }}
                accessibilityRole="button"
                accessibilityLabel={t('common:retry')}
              >
                <Ionicons name="refresh" size={22} color={SoftColors.accentDark} />
              </Pressable>
            </View>
          </View>
          </View>
        </View>
      </Modal>
    </>
  );
}

const styles = StyleSheet.create({
  badgeWrap: {
    borderRadius: SoftRadius.full,
    paddingHorizontal: SoftSpacing.sm,
    paddingVertical: 4,
    minHeight: 28,
    justifyContent: 'center',
  },
  badgeExpanded: {
    paddingVertical: SoftSpacing.sm,
    minHeight: 36,
    paddingHorizontal: SoftSpacing.md,
  },
  badgeInner: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
  },
  badgeText: {
    ...SoftTypography.label,
    fontSize: 12,
    fontWeight: '700',
  },
  modalRoot: {
    flex: 1,
  },
  modalBackdropFill: {
    ...StyleSheet.absoluteFillObject,
    backgroundColor: 'rgba(0,0,0,0.45)',
  },
  /** Keeps sheet CTAs above the full-screen dismiss backdrop on Android. */
  modalLayerAboveBackdrop: {
    flex: 1,
    justifyContent: 'center',
    padding: SoftSpacing.lg,
    width: '100%',
    zIndex: 1,
    elevation: 4,
  },
  sheet: {
    backgroundColor: SoftColors.bgCard,
    borderRadius: SoftRadius.lg,
    padding: SoftSpacing.lg,
    maxWidth: 420,
    alignSelf: 'center',
    width: '100%',
  },
  sheetTitle: {
    ...SoftTypography.h3,
    marginBottom: SoftSpacing.md,
    textAlign: 'center',
    color: SoftColors.textPrimary,
  },
  row: {
    marginBottom: SoftSpacing.sm,
  },
  label: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
    marginBottom: 2,
  },
  value: {
    ...SoftTypography.body,
    color: SoftColors.textPrimary,
  },
  valueStrong: {
    ...SoftTypography.body,
    fontWeight: '700',
    color: SoftColors.textPrimary,
  },
  bodyMuted: {
    ...SoftTypography.body,
    color: SoftColors.textSecondary,
    marginBottom: SoftSpacing.md,
    textAlign: 'center',
  },
  warnBox: {
    backgroundColor: SoftColors.warningBg,
    borderRadius: SoftRadius.md,
    padding: SoftSpacing.sm,
    marginTop: SoftSpacing.sm,
    marginBottom: SoftSpacing.sm,
    borderWidth: StyleSheet.hairlineWidth,
    borderColor: SoftColors.warning,
  },
  warnText: {
    ...SoftTypography.caption,
    color: SoftColors.textPrimary,
    fontWeight: '600',
  },
  techToggle: {
    marginTop: SoftSpacing.sm,
    paddingVertical: SoftSpacing.xs,
  },
  techToggleText: {
    ...SoftTypography.caption,
    color: SoftColors.accentDark,
    textDecorationLine: 'underline',
    fontWeight: '600',
  },
  techBox: {
    marginTop: SoftSpacing.xs,
    padding: SoftSpacing.sm,
    borderRadius: SoftRadius.md,
    backgroundColor: SoftColors.bgSecondary,
    borderWidth: StyleSheet.hairlineWidth,
    borderColor: SoftColors.borderLight,
  },
  techMono: {
    ...SoftTypography.caption,
    fontFamily: 'monospace',
    color: SoftColors.textPrimary,
    marginTop: 4,
  },
  ctaPrimary: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: SoftSpacing.xs,
    backgroundColor: SoftColors.accent,
    borderRadius: SoftRadius.md,
    paddingVertical: SoftSpacing.sm,
    marginTop: SoftSpacing.sm,
  },
  ctaPrimaryText: {
    ...SoftTypography.label,
    color: SoftColors.textInverse,
    fontWeight: '700',
  },
  ctaHintPressable: {
    alignSelf: 'center',
    marginTop: 4,
    paddingVertical: 4,
    paddingHorizontal: SoftSpacing.sm,
  },
  ctaHint: {
    ...SoftTypography.caption,
    color: SoftColors.accentDark,
    textAlign: 'center',
    textDecorationLine: 'underline',
  },
  ctaSecondary: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: SoftSpacing.xs,
    borderRadius: SoftRadius.md,
    paddingVertical: SoftSpacing.sm,
    marginTop: SoftSpacing.sm,
    borderWidth: 1,
    borderColor: SoftColors.borderLight,
    backgroundColor: SoftColors.bgPrimary,
  },
  ctaSecondaryText: {
    ...SoftTypography.label,
    color: SoftColors.accentDark,
    fontWeight: '600',
  },
  contactFoot: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
    textAlign: 'center',
    marginTop: SoftSpacing.sm,
  },
  modalFooter: {
    flexDirection: 'row',
    alignItems: 'stretch',
    marginTop: SoftSpacing.sm,
    gap: SoftSpacing.sm,
  },
  dismissBtnFlex: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: SoftSpacing.sm,
    borderRadius: SoftRadius.md,
    backgroundColor: SoftColors.bgSecondary,
    minHeight: 44,
  },
  dismissText: {
    ...SoftTypography.label,
    color: SoftColors.textPrimary,
    fontWeight: '600',
  },
  iconRefreshBtn: {
    width: 48,
    minHeight: 44,
    borderRadius: SoftRadius.md,
    borderWidth: 1,
    borderColor: SoftColors.borderLight,
    backgroundColor: SoftColors.bgCard,
    justifyContent: 'center',
    alignItems: 'center',
  },
});
