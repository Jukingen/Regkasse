import { useFocusEffect, useRouter } from 'expo-router';
import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Alert, Pressable, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { ReceiptList } from '../../components/ReceiptList';
import { SoftColors, SoftSpacing, SoftTypography } from '../../constants/SoftTheme';
import { useAuth } from '../../contexts/AuthContext';
import { usePosRegisterReadiness } from '../../contexts/PosRegisterReadinessContext';
import { usePosStatusOverview } from '../../contexts/PosStatusOverviewContext';
import { getFormattingLocaleForTextLocale } from '../../i18n/localeUtils';
import {
  fetchRecentReceipts,
  POS_RECEIPTS_RECENT_LIMIT,
  type PosReceiptListItem,
} from '../../services/api/receiptService';
import { WaveLoader } from '../../src/components/common/WaveLoader';
import { isValidPosCashRegisterId } from '../../utils/posCashRegister';
import { hasPermission } from '../../utils/posPermissions';

function resolveEffectiveRegisterId(
  readinessId?: string | null,
  overviewId?: string | null
): string | null {
  for (const id of [readinessId, overviewId]) {
    if (isValidPosCashRegisterId(id)) return id!.trim();
  }
  return null;
}

export default function ReceiptListScreen() {
  const router = useRouter();
  const { t, i18n } = useTranslation(['receipts', 'common']);
  const { user } = useAuth();
  const canReprint = hasPermission(user, 'receipt.reprint');
  const {
    data: registerData,
    loading: registerLoading,
    refresh: refreshRegister,
  } = usePosRegisterReadiness();
  const { cashRegister: overviewRegister } = usePosStatusOverview();
  const [registerId, setRegisterId] = useState<string | null>(null);
  const [receipts, setReceipts] = useState<PosReceiptListItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [refreshing, setRefreshing] = useState(false);

  useEffect(() => {
    setRegisterId(
      resolveEffectiveRegisterId(
        registerData?.effectiveRegisterId,
        overviewRegister?.effectiveRegisterId
      )
    );
  }, [registerData?.effectiveRegisterId, overviewRegister?.effectiveRegisterId]);

  const formatLocale = useMemo(
    () => getFormattingLocaleForTextLocale(i18n.resolvedLanguage || i18n.language),
    [i18n.language, i18n.resolvedLanguage]
  );

  const loadReceipts = useCallback(
    async (opts?: { silent?: boolean }) => {
      if (!registerId) return;
      if (!opts?.silent) setLoading(true);
      try {
        const rows = await fetchRecentReceipts({
          cashRegisterId: registerId,
          pageSize: POS_RECEIPTS_RECENT_LIMIT,
        });
        setReceipts(rows);
      } catch {
        if (!opts?.silent) {
          Alert.alert(t('common:error'), t('receipts:loadError'));
        }
      } finally {
        setLoading(false);
        setRefreshing(false);
      }
    },
    [registerId, t]
  );

  useFocusEffect(
    useCallback(() => {
      if (!registerId) return undefined;
      void loadReceipts({ silent: true });
      return undefined;
    }, [registerId, loadReceipts])
  );

  const onRefresh = useCallback(() => {
    setRefreshing(true);
    void loadReceipts({ silent: true });
  }, [loadReceipts]);

  const isRegisterResolving = registerLoading && !registerId;

  if (isRegisterResolving) {
    return (
      <SafeAreaView style={styles.container} edges={['bottom']}>
        <View style={styles.centered}>
          <WaveLoader size={32} color={SoftColors.accent} />
          <Text style={styles.hint}>{t('receipts:preparingRegister')}</Text>
        </View>
      </SafeAreaView>
    );
  }

  if (!registerId) {
    return (
      <SafeAreaView style={styles.container} edges={['bottom']}>
        <View style={styles.header}>
          <Text style={styles.title}>{t('receipts:title')}</Text>
        </View>
        <View style={styles.centered}>
          <Text style={styles.hint}>{t('receipts:noRegister')}</Text>
          <Pressable
            style={styles.linkButton}
            onPress={() => {
              void refreshRegister();
              router.push('/(tabs)/settings' as const);
            }}>
            <Text style={styles.linkButtonText}>{t('common:continue')}</Text>
          </Pressable>
        </View>
      </SafeAreaView>
    );
  }

  return (
    <SafeAreaView style={styles.container} edges={['bottom']}>
      <View style={styles.header}>
        <Text style={styles.title}>{t('receipts:title')}</Text>
        <Text style={styles.subtitle}>{t('receipts:lastReceipts')}</Text>
      </View>
      <ReceiptList
        receipts={receipts}
        cashRegisterId={registerId}
        loading={loading}
        refreshing={refreshing}
        onRefresh={onRefresh}
        canReprint={canReprint}
        formatLocale={formatLocale}
        emptyMessage={t('receipts:empty')}
      />
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: SoftColors.bgPrimary,
  },
  header: {
    paddingHorizontal: SoftSpacing.md,
    paddingTop: SoftSpacing.sm,
    paddingBottom: SoftSpacing.sm,
  },
  title: {
    ...SoftTypography.h2,
    color: SoftColors.textPrimary,
  },
  subtitle: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
    marginTop: 2,
  },
  centered: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: SoftSpacing.lg,
    gap: SoftSpacing.sm,
  },
  hint: {
    ...SoftTypography.body,
    color: SoftColors.textMuted,
    textAlign: 'center',
  },
  linkButton: {
    marginTop: SoftSpacing.sm,
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm,
  },
  linkButtonText: {
    ...SoftTypography.label,
    color: SoftColors.accent,
    fontWeight: '700',
  },
});
