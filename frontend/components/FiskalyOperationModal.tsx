import React, { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  ActivityIndicator,
  Alert,
  Modal,
  Pressable,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';

import { SoftColors, SoftRadius, SoftSpacing, SoftTypography } from '../constants/SoftTheme';
import { paymentHistoryLabelKeyToI18n, postStorno } from '../services/api/paymentHistoryService';
import {
  postCreateJahresbeleg,
  postCreateMonatsbeleg,
  postCreateNullbeleg,
  postCreateSchlussbeleg,
  postCreateStartbeleg,
} from '../services/api/rksvSpecialReceiptsService';
import { getViennaYearMonth } from '../utils/resolvePosMonatsbelegTarget';
import { readPosApiErrorMessage } from '../utils/readPosApiErrorMessage';

export type PosFiskalyOperation =
  | 'startbeleg'
  | 'nullbeleg'
  | 'monatsbeleg'
  | 'jahresbeleg'
  | 'schlussbeleg'
  | 'cancel';

type FiskalyOperationModalProps = {
  visible: boolean;
  cashRegisterId: string;
  onClose: () => void;
  onSuccess?: () => void;
};

function previousViennaMonth(year: number, month: number): { year: number; month: number } {
  if (month <= 1) return { year: year - 1, month: 12 };
  return { year, month: month - 1 };
}

export function FiskalyOperationModal({
  visible,
  cashRegisterId,
  onClose,
  onSuccess,
}: FiskalyOperationModalProps) {
  const { t } = useTranslation(['receipts', 'common', 'paymentHistory']);
  const vienna = useMemo(() => getViennaYearMonth(), []);
  const prev = previousViennaMonth(vienna.year, vienna.month);
  const [operation, setOperation] = useState<PosFiskalyOperation>('startbeleg');
  const [reason, setReason] = useState('');
  const [paymentId, setPaymentId] = useState('');
  const [busy, setBusy] = useState(false);

  const operations: Array<{ id: PosFiskalyOperation; label: string }> = [
    { id: 'startbeleg', label: t('receipts:fiskalyOps.startbeleg') },
    { id: 'nullbeleg', label: t('receipts:fiskalyOps.nullbeleg') },
    { id: 'monatsbeleg', label: t('receipts:fiskalyOps.monatsbeleg') },
    { id: 'jahresbeleg', label: t('receipts:fiskalyOps.jahresbeleg') },
    { id: 'schlussbeleg', label: t('receipts:fiskalyOps.schlussbeleg') },
    { id: 'cancel', label: t('receipts:fiskalyOps.cancel') },
  ];

  const run = async () => {
    const trimmedReason = reason.trim();
    setBusy(true);
    try {
      if (operation === 'startbeleg') {
        const result = await postCreateStartbeleg({
          cashRegisterId,
          reason: trimmedReason || 'POS Startbeleg',
        });
        Alert.alert(t('common:success'), `${t('receipts:fiskalyOps.success')}\n${result.receiptNumber}`);
      } else if (operation === 'nullbeleg') {
        const result = await postCreateNullbeleg({
          cashRegisterId,
          year: vienna.year,
          month: vienna.month,
          reason: trimmedReason || 'POS Nullbeleg',
        });
        Alert.alert(t('common:success'), `${t('receipts:fiskalyOps.success')}\n${result.receiptNumber}`);
      } else if (operation === 'monatsbeleg') {
        const result = await postCreateMonatsbeleg({
          cashRegisterId,
          year: prev.year,
          month: prev.month,
          reason: trimmedReason || 'POS Monatsbeleg',
        });
        Alert.alert(t('common:success'), `${t('receipts:fiskalyOps.success')}\n${result.receiptNumber}`);
      } else if (operation === 'jahresbeleg') {
        const result = await postCreateJahresbeleg({
          cashRegisterId,
          year: vienna.year,
          reason: trimmedReason || 'POS Jahresbeleg',
        });
        Alert.alert(t('common:success'), `${t('receipts:fiskalyOps.success')}\n${result.receiptNumber}`);
      } else if (operation === 'schlussbeleg') {
        const result = await postCreateSchlussbeleg({
          cashRegisterId,
          reason: trimmedReason || 'POS Schlussbeleg',
        });
        Alert.alert(t('common:success'), `${t('receipts:fiskalyOps.success')}\n${result.receiptNumber}`);
      } else {
        const id = paymentId.trim();
        if (!id) {
          Alert.alert(t('common:error'), t('receipts:fiskalyOps.paymentIdRequired'));
          return;
        }
        if (trimmedReason.length < 5) {
          Alert.alert(t('common:error'), t('receipts:stornoReasonTooShort'));
          return;
        }
        const result = await postStorno({
          paymentId: id,
          reasonCode: 'CustomerRequest',
          reason: trimmedReason,
        });
        if (!result.success) {
          const key = result.errorKey ? paymentHistoryLabelKeyToI18n(result.errorKey) : '';
          Alert.alert(t('common:error'), key ? t(key) : t('receipts:stornoFailed'));
          return;
        }
        Alert.alert(t('common:success'), t('receipts:stornoSuccess'));
      }
      onSuccess?.();
      onClose();
    } catch (err) {
      Alert.alert(t('common:error'), readPosApiErrorMessage(err, t('receipts:fiskalyOps.failed')));
    } finally {
      setBusy(false);
    }
  };

  const confirmAndRun = () => {
    if (operation === 'schlussbeleg') {
      Alert.alert(t('receipts:fiskalyOps.schlussConfirmTitle'), t('receipts:fiskalyOps.schlussConfirmBody'), [
        { text: t('common:cancel'), style: 'cancel' },
        {
          text: t('receipts:fiskalyOps.execute'),
          style: 'destructive',
          onPress: () => {
            void run();
          },
        },
      ]);
      return;
    }
    void run();
  };

  return (
    <Modal visible={visible} transparent animationType="slide" onRequestClose={onClose}>
      <View style={styles.overlay}>
        <View style={styles.card}>
          <Text style={styles.title}>{t('receipts:fiskalyOps.title')}</Text>
          <Text style={styles.hint}>{t('receipts:fiskalyOps.normalHint')}</Text>
          <View style={styles.ops}>
            {operations.map((op) => (
              <Pressable
                key={op.id}
                style={[styles.opChip, operation === op.id && styles.opChipActive]}
                onPress={() => setOperation(op.id)}>
                <Text style={[styles.opChipText, operation === op.id && styles.opChipTextActive]}>{op.label}</Text>
              </Pressable>
            ))}
          </View>
          {operation === 'cancel' ? (
            <TextInput
              style={styles.input}
              placeholder={t('receipts:fiskalyOps.paymentIdPlaceholder')}
              value={paymentId}
              onChangeText={setPaymentId}
              autoCapitalize="none"
            />
          ) : null}
          <TextInput
            style={styles.input}
            placeholder={t('receipts:fiskalyOps.reasonPlaceholder')}
            value={reason}
            onChangeText={setReason}
          />
          <View style={styles.actions}>
            <Pressable style={styles.secondary} onPress={onClose} disabled={busy}>
              <Text style={styles.secondaryText}>{t('receipts:close')}</Text>
            </Pressable>
            <Pressable
              style={[styles.primary, busy && styles.primaryDisabled]}
              onPress={confirmAndRun}
              disabled={busy}>
              {busy ? (
                <ActivityIndicator color={SoftColors.textInverse} />
              ) : (
                <Text style={styles.primaryText}>{t('receipts:fiskalyOps.execute')}</Text>
              )}
            </Pressable>
          </View>
        </View>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  overlay: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.45)',
    justifyContent: 'flex-end',
  },
  card: {
    backgroundColor: SoftColors.bgCard,
    borderTopLeftRadius: SoftRadius.lg,
    borderTopRightRadius: SoftRadius.lg,
    padding: SoftSpacing.lg,
    gap: SoftSpacing.sm,
  },
  title: {
    ...SoftTypography.h3,
    color: SoftColors.textPrimary,
  },
  hint: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
  },
  ops: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
  },
  opChip: {
    borderWidth: 1,
    borderColor: SoftColors.border,
    borderRadius: SoftRadius.full,
    paddingHorizontal: 12,
    paddingVertical: 6,
  },
  opChipActive: {
    backgroundColor: SoftColors.accent,
    borderColor: SoftColors.accent,
  },
  opChipText: {
    ...SoftTypography.label,
    fontSize: 12,
    color: SoftColors.textPrimary,
  },
  opChipTextActive: {
    color: SoftColors.textInverse,
    fontWeight: '700',
  },
  input: {
    borderWidth: 1,
    borderColor: SoftColors.border,
    borderRadius: SoftRadius.md,
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: 10,
    color: SoftColors.textPrimary,
  },
  actions: {
    flexDirection: 'row',
    justifyContent: 'flex-end',
    gap: SoftSpacing.sm,
    marginTop: SoftSpacing.sm,
  },
  secondary: {
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: 10,
  },
  secondaryText: {
    ...SoftTypography.label,
    color: SoftColors.textMuted,
  },
  primary: {
    backgroundColor: SoftColors.accent,
    borderRadius: SoftRadius.full,
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: 10,
    minWidth: 120,
    alignItems: 'center',
  },
  primaryDisabled: {
    opacity: 0.7,
  },
  primaryText: {
    ...SoftTypography.label,
    fontWeight: '700',
    color: SoftColors.textInverse,
  },
});
