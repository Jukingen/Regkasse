import { Ionicons } from '@expo/vector-icons';
import React from 'react';
import { useTranslation } from 'react-i18next';
import { View, Text, StyleSheet, TouchableOpacity } from 'react-native';

import { Colors, Spacing, BorderRadius } from '../constants/Colors';
import { useTseStatus } from '../hooks/useTseStatus';
import { WaveLoader } from '../src/components/common/WaveLoader';
import { formatUserDateTime } from '../utils/dateFormatter';
import { TseStatusBadge } from './TseStatusBadge';

interface TseStatusIndicatorProps {
  showDetails?: boolean;
}

export const TseStatusIndicator: React.FC<TseStatusIndicatorProps> = ({
  showDetails = true,
}) => {
  const { t } = useTranslation(['system']);
  const { status, loading, details, showTestBadge, refetch } = useTseStatus();
  const na = t('system:tse.tooltip.na');
  const statusColor =
    status === 'Inactive'
      ? Colors.light.error
      : status === 'Degraded'
        ? Colors.light.warning
        : Colors.light.success;

  if (loading && !details.lastCheck) {
    return (
      <View style={styles.container}>
        <WaveLoader size={18} color={Colors.light.primary} />
        <Text style={styles.loadingText}>{t('system:tse.indicator.checking')}</Text>
      </View>
    );
  }

  return (
    <View style={[styles.container, { borderColor: statusColor }]}>
      <View style={styles.badgeRow}>
        <TseStatusBadge />
        <TouchableOpacity onPress={() => void refetch()} accessibilityRole="button">
          <Ionicons name="refresh" size={16} color={statusColor} />
        </TouchableOpacity>
      </View>
      {showDetails ? (
        <View style={styles.detailsContainer}>
          <Text style={styles.detailText}>
            {t('system:tse.tooltip.scuId')}: {details.scuId?.trim() || na}
          </Text>
          <Text style={styles.detailText}>
            {t('system:tse.tooltip.lastCheck')}:{' '}
            {formatUserDateTime(details.lastCheck, { includeSeconds: true }) || na}
          </Text>
          <Text style={styles.detailText}>
            {t('system:tse.tooltip.certificateUntil')}:{' '}
            {formatUserDateTime(details.certificateValidUntil) || na}
          </Text>
          <Text style={styles.detailText}>
            {t('system:tse.tooltip.environment')}:{' '}
            {details.environment?.trim() || (showTestBadge ? 'TEST' : na)}
          </Text>
        </View>
      ) : null}
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    padding: Spacing.sm,
    borderWidth: 1,
    borderRadius: BorderRadius.sm,
    backgroundColor: Colors.light.background,
    marginVertical: Spacing.xs,
  },
  badgeRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: Spacing.xs,
  },
  loadingText: {
    fontSize: 12,
    color: Colors.light.textSecondary,
    marginLeft: Spacing.xs,
  },
  detailsContainer: {
    marginTop: Spacing.xs,
    paddingTop: Spacing.xs,
    borderTopWidth: 1,
    borderTopColor: Colors.light.border,
  },
  detailText: {
    fontSize: 11,
    color: Colors.light.textSecondary,
    marginBottom: 2,
  },
});
