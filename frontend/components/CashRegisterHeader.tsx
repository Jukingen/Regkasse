// Soft minimal cash register header
import React from 'react';
import { View, Text, StyleSheet } from 'react-native';
import { SoftColors, SoftSpacing, SoftRadius, SoftTypography, SoftShadows } from '../constants/SoftTheme';

interface CashRegisterHeaderProps {
  selectedTable: number;
  recoveryLoading: boolean;
  /** 503/TABLE_ORDERS_MISSING durumunda gösterilecek bilgi mesajı */
  provisioningMessage?: string | null;
}

import { useTranslation } from 'react-i18next';

export const CashRegisterHeader: React.FC<CashRegisterHeaderProps> = ({
  selectedTable,
  recoveryLoading,
  provisioningMessage,
}) => {
  const { t } = useTranslation(['checkout', 'common']);

  return (
    <View style={styles.header}>
      <View style={styles.headerContent}>
        <Text style={styles.headerEmoji}>☕</Text>
        <View style={styles.headerText}>
          <Text style={styles.headerTitle}>{t('checkout:title')}</Text>
          <Text style={styles.headerSubtitle}>{t('checkout:subtitle', 'Table Management & Payments')}</Text>
        </View>
      </View>

      {selectedTable && (
        <View style={styles.tableBadge}>
          <Text style={styles.tableBadgeText}>{t('common:table', 'Table')} {selectedTable}</Text>
        </View>
      )}

      {recoveryLoading && (
        <View style={styles.recoveryBadge}>
          <Text style={styles.recoveryText}>🔄 {t('common:loading', 'Loading orders...')}</Text>
        </View>
      )}

      {provisioningMessage && !recoveryLoading && (
        <View style={[styles.recoveryBadge, styles.provisioningBadge]}>
          <Text style={styles.provisioningText}>ℹ️ {provisioningMessage}</Text>
        </View>
      )}
    </View>
  );
};

const styles = StyleSheet.create({
  header: {
    backgroundColor: SoftColors.accent,
    paddingTop: SoftSpacing.xl,
    paddingBottom: SoftSpacing.lg,
    paddingHorizontal: SoftSpacing.lg,
    borderBottomLeftRadius: SoftRadius.xxl,
    borderBottomRightRadius: SoftRadius.xxl,
    ...SoftShadows.md,
  },
  headerContent: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: SoftSpacing.sm,
  },
  headerEmoji: {
    fontSize: 32,
    marginRight: SoftSpacing.md,
  },
  headerText: {
    flex: 1,
  },
  headerTitle: {
    ...SoftTypography.h1,
    color: SoftColors.textInverse,
  },
  headerSubtitle: {
    ...SoftTypography.bodySmall,
    color: SoftColors.textInverse,
    opacity: 0.85,
    marginTop: SoftSpacing.xs,
  },
  tableBadge: {
    backgroundColor: 'rgba(255,255,255,0.25)',
    alignSelf: 'flex-start',
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.xs,
    borderRadius: SoftRadius.full,
    marginTop: SoftSpacing.sm,
  },
  tableBadgeText: {
    ...SoftTypography.label,
    color: SoftColors.textInverse,
  },
  recoveryBadge: {
    backgroundColor: SoftColors.bgCard,
    alignSelf: 'flex-start',
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.xs,
    borderRadius: SoftRadius.full,
    marginTop: SoftSpacing.sm,
  },
  recoveryText: {
    ...SoftTypography.caption,
    color: SoftColors.accent,
  },
  provisioningBadge: {
    backgroundColor: 'rgba(255,193,7,0.2)',
    borderWidth: 1,
    borderColor: 'rgba(255,193,7,0.5)',
  },
  provisioningText: {
    ...SoftTypography.caption,
    color: SoftColors.textPrimary,
    fontSize: 12,
  },
});
