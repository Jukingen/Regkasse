import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  ActivityIndicator,
  Modal,
  Pressable,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';

import {
  SoftColors,
  SoftRadius,
  SoftSpacing,
  SoftTypography,
} from '../constants/SoftTheme';

export type StornoModalProps = {
  visible: boolean;
  receiptNumber: string;
  submitting?: boolean;
  onCancel: () => void;
  onConfirm: (reason: string) => void;
};

export function StornoModal({
  visible,
  receiptNumber,
  submitting = false,
  onCancel,
  onConfirm,
}: StornoModalProps) {
  const { t } = useTranslation(['receipts', 'common']);
  const [reason, setReason] = useState('');

  const close = () => {
    if (submitting) return;
    setReason('');
    onCancel();
  };

  return (
    <Modal visible={visible} transparent animationType="fade" onRequestClose={close}>
      <View style={styles.overlay}>
        <View style={styles.card}>
          <Text style={styles.title}>{t('receipts:stornoConfirmTitle')}</Text>
          <Text style={styles.body}>
            {t('receipts:stornoConfirmBody', { number: receiptNumber })}
          </Text>
          <Text style={styles.label}>{t('receipts:stornoReasonLabel')}</Text>
          <TextInput
            style={styles.input}
            value={reason}
            onChangeText={setReason}
            placeholder={t('receipts:stornoReasonPlaceholder')}
            placeholderTextColor={SoftColors.textMuted}
            editable={!submitting}
            multiline
            numberOfLines={3}
            maxLength={500}
            accessibilityLabel={t('receipts:stornoReasonLabel')}
          />
          <View style={styles.actions}>
            <Pressable
              style={styles.secondary}
              onPress={close}
              disabled={submitting}
              accessibilityRole="button">
              <Text style={styles.secondaryText}>{t('common:cancel')}</Text>
            </Pressable>
            <Pressable
              style={[styles.primary, submitting && styles.primaryDisabled]}
              onPress={() => onConfirm(reason)}
              disabled={submitting}
              accessibilityRole="button"
              accessibilityLabel={t('receipts:stornoSubmit')}>
              {submitting ? (
                <ActivityIndicator size="small" color={SoftColors.textInverse} />
              ) : (
                <Text style={styles.primaryText}>{t('receipts:stornoSubmit')}</Text>
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
    justifyContent: 'center',
    padding: SoftSpacing.lg,
  },
  card: {
    backgroundColor: SoftColors.bgCard,
    borderRadius: SoftRadius.lg,
    padding: SoftSpacing.lg,
    gap: SoftSpacing.sm,
  },
  title: {
    ...SoftTypography.h3,
    color: SoftColors.textPrimary,
  },
  body: {
    ...SoftTypography.body,
    color: SoftColors.textPrimary,
  },
  label: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
    marginTop: SoftSpacing.xs,
  },
  input: {
    ...SoftTypography.body,
    minHeight: 72,
    borderWidth: 1,
    borderColor: SoftColors.border,
    borderRadius: SoftRadius.md,
    padding: SoftSpacing.sm,
    color: SoftColors.textPrimary,
    textAlignVertical: 'top',
  },
  actions: {
    flexDirection: 'row',
    justifyContent: 'flex-end',
    gap: SoftSpacing.sm,
    marginTop: SoftSpacing.sm,
  },
  secondary: {
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: 8,
    minHeight: 40,
    justifyContent: 'center',
  },
  secondaryText: {
    ...SoftTypography.label,
    color: SoftColors.textMuted,
    fontWeight: '700',
  },
  primary: {
    backgroundColor: SoftColors.error,
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: 8,
    minHeight: 40,
    minWidth: 148,
    borderRadius: SoftRadius.full,
    alignItems: 'center',
    justifyContent: 'center',
  },
  primaryDisabled: {
    opacity: 0.7,
  },
  primaryText: {
    ...SoftTypography.label,
    fontSize: 13,
    fontWeight: '700',
    color: SoftColors.textInverse,
  },
});
