// Compact POS header: title + sticky table/recovery bar (no large hero)
import React from 'react';
import { useTranslation } from 'react-i18next';
import { View, Text, StyleSheet, Pressable } from 'react-native';

import {
  SoftColors,
  SoftSpacing,
  SoftRadius,
  SoftTypography,
  SoftShadows,
} from '../constants/SoftTheme';

interface CashRegisterHeaderProps {
  selectedTable: number;
  recoveryLoading: boolean;
  /** 503/TABLE_ORDERS_MISSING durumunda gösterilecek bilgi mesajı */
  provisioningMessage?: string | null;
  onOpenPaymentHistory?: () => void;
}

export const CashRegisterHeader: React.FC<CashRegisterHeaderProps> = ({
  selectedTable,
  recoveryLoading,
  provisioningMessage,
  onOpenPaymentHistory,
}) => {
  const { t } = useTranslation(['checkout', 'common', 'paymentHistory']);

  return (
    <View
      style={styles.wrapper}
      accessibilityRole="header"
      accessibilityLabel={t('checkout:title')}>
      <View style={styles.headerRow}>
        <Text style={styles.headerEmoji}>☕</Text>
        <Text style={styles.headerTitle}>{t('checkout:title')}</Text>
        {selectedTable > 0 && (
          <View style={styles.tableBadge}>
            <Text style={styles.tableBadgeText}>
              {t('common:table')} {selectedTable}
            </Text>
          </View>
        )}
        {recoveryLoading && (
          <View style={styles.recoveryBadge} accessibilityLabel={t('common:tableRestoring')}>
            <Text style={styles.recoveryText} accessibilityElementsHidden>
              🔄
            </Text>
          </View>
        )}
        {provisioningMessage && !recoveryLoading && (
          <View
            style={[styles.recoveryBadge, styles.provisioningBadge]}
            accessibilityLabel={provisioningMessage}>
            <Text style={styles.provisioningText} accessibilityElementsHidden>
              ℹ️
            </Text>
          </View>
        )}
        {onOpenPaymentHistory ? (
          <Pressable
            onPress={onOpenPaymentHistory}
            style={({ pressed }) => [styles.historyButton, pressed && styles.historyButtonPressed]}
            accessibilityRole="button"
            accessibilityLabel={t('paymentHistory:quickAccess')}
            hitSlop={6}>
            <Text style={styles.historyButtonEmoji} accessibilityElementsHidden>
              💰
            </Text>
            <Text style={styles.historyButtonText}>{t('paymentHistory:quickAccess')}</Text>
          </Pressable>
        ) : null}
      </View>
    </View>
  );
};

const styles = StyleSheet.create({
  wrapper: {
    backgroundColor: SoftColors.accent,
    borderBottomLeftRadius: SoftRadius.lg,
    borderBottomRightRadius: SoftRadius.lg,
    ...SoftShadows.sm,
  },
  headerRow: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: SoftSpacing.sm,
    paddingHorizontal: SoftSpacing.md,
    gap: SoftSpacing.sm,
  },
  headerEmoji: {
    fontSize: 20,
  },
  headerTitle: {
    ...SoftTypography.h3,
    color: SoftColors.textInverse,
    flex: 1,
  },
  tableBadge: {
    backgroundColor: 'rgba(255,255,255,0.28)',
    paddingHorizontal: SoftSpacing.sm,
    paddingVertical: SoftSpacing.xs,
    borderRadius: SoftRadius.full,
    minHeight: 28,
    justifyContent: 'center',
  },
  tableBadgeText: {
    ...SoftTypography.label,
    fontSize: 12,
    color: SoftColors.textInverse,
  },
  recoveryBadge: {
    backgroundColor: SoftColors.bgCard,
    paddingHorizontal: SoftSpacing.xs,
    paddingVertical: SoftSpacing.xs,
    borderRadius: SoftRadius.full,
    minHeight: 24,
    justifyContent: 'center',
    alignItems: 'center',
  },
  recoveryText: {
    ...SoftTypography.caption,
    fontSize: 12,
    color: SoftColors.accent,
  },
  provisioningBadge: {
    backgroundColor: 'rgba(255,193,7,0.22)',
    borderWidth: 1,
    borderColor: 'rgba(255,193,7,0.5)',
  },
  provisioningText: {
    ...SoftTypography.caption,
    color: SoftColors.textPrimary,
  },
  historyButton: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    backgroundColor: 'rgba(255,255,255,0.28)',
    paddingHorizontal: SoftSpacing.sm,
    paddingVertical: SoftSpacing.xs,
    borderRadius: SoftRadius.full,
    minHeight: 28,
  },
  historyButtonPressed: {
    opacity: 0.85,
  },
  historyButtonEmoji: {
    fontSize: 14,
  },
  historyButtonText: {
    ...SoftTypography.label,
    fontSize: 12,
    fontWeight: '700',
    color: SoftColors.textInverse,
  },
});
