import { Ionicons } from '@expo/vector-icons';
import React, { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ActivityIndicator, Pressable, StyleSheet, Text, View, type ViewStyle } from 'react-native';

import { SoftColors, SoftRadius, SoftSpacing, SoftTypography } from '../constants/SoftTheme';
import { useLicenseStatus, type LicenseStatus } from '../hooks/useLicenseStatus';
import { LicenseModal } from '../src/features/license/LicenseModal';

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

export type LicenseStatusIndicatorProps = {
  badgeAlignSelf?: ViewStyle['alignSelf'];
  /** Larger vertical padding for dense settings lists */
  expandedTouchTarget?: boolean;
};

/**
 * Permanent license tier badge driven by anonymous GET `/api/health/license`
 * (`useLicenseStatus`). Tap opens `LicenseModal` (status + activation + renewal).
 */
export function LicenseStatusIndicator({
  badgeAlignSelf = 'flex-end',
  expandedTouchTarget = false,
}: LicenseStatusIndicatorProps) {
  const { t } = useTranslation(['license', 'common']);
  const { status, loading, refetch } = useLicenseStatus();
  const [detailOpen, setDetailOpen] = useState(false);

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

      <LicenseModal
        visible={detailOpen}
        onClose={() => setDetailOpen(false)}
        status={status}
        loading={loading}
        unlimitedPaid={unlimitedPaid}
        refetch={refetch}
      />
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
});
