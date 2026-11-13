import { useFocusEffect, useRouter } from 'expo-router';
import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Alert, Pressable, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { FiskalyOperationModal } from '../../components/FiskalyOperationModal';
import { SoftColors, SoftSpacing, SoftTypography } from '../../constants/SoftTheme';
import { usePosRegisterReadiness } from '../../contexts/PosRegisterReadinessContext';
import { usePosStatusOverview } from '../../contexts/PosStatusOverviewContext';
import { WaveLoader } from '../../src/components/common/WaveLoader';
import { isValidPosCashRegisterId } from '../../utils/posCashRegister';
import { usePosPermissions } from '../../hooks/usePosPermissions';

function resolveEffectiveRegisterId(
  readinessId?: string | null,
  overviewId?: string | null
): string | null {
  for (const id of [readinessId, overviewId]) {
    if (isValidPosCashRegisterId(id)) return id!.trim();
  }
  return null;
}

export default function FiskalyOperationsScreen() {
  const router = useRouter();
  const { t } = useTranslation(['receipts', 'common']);
  const { canCreateSonderbeleg } = usePosPermissions();
  const {
    data: registerData,
    loading: registerLoading,
    refresh: refreshRegister,
  } = usePosRegisterReadiness();
  const { cashRegister: overviewRegister } = usePosStatusOverview();
  const [registerId, setRegisterId] = useState<string | null>(null);
  const [modalOpen, setModalOpen] = useState(true);

  useEffect(() => {
    setRegisterId(
      resolveEffectiveRegisterId(
        registerData?.effectiveRegisterId,
        overviewRegister?.effectiveRegisterId
      )
    );
  }, [registerData?.effectiveRegisterId, overviewRegister?.effectiveRegisterId]);

  useFocusEffect(
    useCallback(() => {
      setModalOpen(true);
      return undefined;
    }, [])
  );

  if (!canCreateSonderbeleg) {
    return (
      <SafeAreaView style={styles.container} edges={['bottom']}>
        <View style={styles.centered}>
          <Text style={styles.hint}>{t('receipts:fiskalyOps.noPermission')}</Text>
        </View>
      </SafeAreaView>
    );
  }

  if (registerLoading && !registerId) {
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
        <Text style={styles.title}>{t('receipts:fiskalyOps.title')}</Text>
        <Text style={styles.subtitle}>{t('receipts:fiskalyOps.subtitle')}</Text>
      </View>
      <FiskalyOperationModal
        visible={modalOpen}
        cashRegisterId={registerId}
        onClose={() => {
          setModalOpen(false);
          router.back();
        }}
        onSuccess={() => {
          setModalOpen(false);
          router.back();
        }}
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
