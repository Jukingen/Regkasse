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
  onOpenReceiptList?: () => void;
}

/** Shared table chip: header (on accent) and the order panel (on card). */
export function CashRegisterTableBadge({
  tableNumber,
  tone = 'onAccent',
}: {
  tableNumber: number;
  tone?: 'onAccent' | 'onCard';
}) {
  const { t } = useTranslation(['common']);
  if (tableNumber <= 0) return null;

  return (
    <View
      style={[styles.tableBadge, tone === 'onCard' && styles.tableBadgeOnCard]}
      accessibilityRole="text"
      accessibilityLabel={`${t('common:table')} ${tableNumber}`}>
      <Text style={[styles.tableBadgeText, tone === 'onCard' && styles.tableBadgeTextOnCard]}>
        {t('common:table')} {tableNumber}
      </Text>
    </View>
  );
}

export const CashRegisterHeader: React.FC<CashRegisterHeaderProps> = ({
  selectedTable,
  recoveryLoading,
  provisioningMessage,
  onOpenPaymentHistory,
  onOpenReceiptList,
}) => {
  const { t } = useTranslation(['checkout', 'common', 'paymentHistory', 'receipts']);

  return (
    <View
      style={styles.wrapper}
      accessibilityRole="header"
      accessibilityLabel={t('checkout:title')}>
      <View style={styles.headerRow}>
        <Text style={styles.headerEmoji}>☕</Text>
        <Text style={styles.headerTitle}>{t('checkout:title')}</Text>
        <CashRegisterTableBadge tableNumber={selectedTable} tone="onAccent" />
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
        {onOpenReceiptList ? (
          <Pressable
            onPress={onOpenReceiptList}
            style={({ pressed }) => [styles.historyButton, pressed && styles.historyButtonPressed]}
            accessibilityRole="button"
            accessibilityLabel={t('receipts:title')}
            hitSlop={6}>
            <Text style={styles.historyButtonEmoji} accessibilityElementsHidden>
              🧾
            </Text>
            <Text style={styles.historyButtonText}>{t('receipts:quickAccess')}</Text>
          </Pressable>
        ) : null}
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
    flexWrap: 'wrap',
  },
  headerEmoji: {
    fontSize: 20,
  },
  headerTitle: {
    ...SoftTypography.h3,
    color: SoftColors.textPrimary,
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
    color: SoftColors.textPrimary,
  },
  tableBadgeOnCard: {
    backgroundColor: SoftColors.accent,
  },
  tableBadgeTextOnCard: {
    color: SoftColors.textPrimary,
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
    color: SoftColors.accentDark,
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
    color: SoftColors.textPrimary,
  },
});
