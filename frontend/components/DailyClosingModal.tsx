import React, { useEffect } from 'react';
import {
  Modal,
  Pressable,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import { useTranslation } from 'react-i18next';

export type DailyClosingModalProps = {
  visible: boolean;
  onClose: () => void;
  onConfirm: () => void;
  cashCount: string;
  onCashCountChange: (value: string) => void;
  notes: string;
  onNotesChange: (value: string) => void;
  error?: string | null;
  isLoading?: boolean;
};

/**
 * POS Settings Tagesabschluss confirmation modal (cash count + optional notes).
 * Opened from ShiftManager when daily closing is allowed.
 */
export function DailyClosingModal({
  visible,
  onClose,
  onConfirm,
  cashCount,
  onCashCountChange,
  notes,
  onNotesChange,
  error,
  isLoading = false,
}: DailyClosingModalProps) {
  const { t } = useTranslation(['settings', 'common']);

  useEffect(() => {
    if (__DEV__ && visible) {
      console.log('✅ DailyClosingModal opened', { visible });
    }
  }, [visible]);

  return (
    <Modal
      visible={visible}
      transparent
      animationType="slide"
      onRequestClose={onClose}
    >
      <View style={styles.modalBackdrop}>
        <View style={styles.modalCard}>
          <Text style={styles.modalTitle}>{t('settings:shift.dailyClosing.modalTitle')}</Text>
          <Text style={styles.modalHint}>{t('settings:shift.dailyClosing.modalHint')}</Text>
          {error ? (
            <View style={styles.modalErrorBox} accessibilityRole="alert">
              <Text style={styles.modalErrorText}>{error}</Text>
            </View>
          ) : null}
          <Text style={styles.modalLabel}>{t('settings:shift.dailyClosing.cashCountLabel')}</Text>
          <TextInput
            style={styles.input}
            keyboardType="decimal-pad"
            value={cashCount}
            onChangeText={onCashCountChange}
            placeholder="0,00"
            placeholderTextColor="#999"
          />
          <Text style={styles.modalLabel}>{t('settings:shift.dailyClosing.notesLabel')}</Text>
          <TextInput
            style={[styles.input, styles.notesInput]}
            value={notes}
            onChangeText={onNotesChange}
            placeholder={t('settings:shift.notesPlaceholder')}
            placeholderTextColor="#999"
            multiline
          />
          <View style={styles.modalActions}>
            <Pressable style={styles.secondaryBtn} onPress={onClose} disabled={isLoading}>
              <Text style={styles.secondaryBtnText}>{t('common:cancel')}</Text>
            </Pressable>
            <Pressable
              style={[styles.primaryBtn, styles.closingBtn, isLoading && styles.btnDisabled]}
              onPress={onConfirm}
              disabled={isLoading}
              accessibilityRole="button"
              accessibilityLabel={t('settings:shift.dailyClosing.confirm')}
            >
              <Text style={styles.primaryBtnText}>
                {isLoading ? t('settings:shift.working') : t('settings:shift.dailyClosing.confirm')}
              </Text>
            </Pressable>
          </View>
        </View>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  modalBackdrop: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.45)',
    justifyContent: 'center',
    padding: 24,
  },
  modalCard: {
    backgroundColor: '#fff',
    borderRadius: 12,
    padding: 20,
  },
  modalTitle: {
    fontSize: 18,
    fontWeight: '700',
    color: '#333',
    marginBottom: 8,
  },
  modalHint: {
    fontSize: 14,
    color: '#64748b',
    marginBottom: 12,
    lineHeight: 20,
  },
  modalErrorBox: {
    backgroundColor: '#fee2e2',
    borderRadius: 8,
    padding: 12,
    marginBottom: 12,
  },
  modalErrorText: {
    color: '#dc2626',
    fontSize: 14,
    lineHeight: 20,
  },
  modalLabel: {
    fontSize: 14,
    color: '#555',
    marginBottom: 6,
    marginTop: 8,
  },
  input: {
    borderWidth: 1,
    borderColor: '#ccc',
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 10,
    fontSize: 16,
    color: '#333',
  },
  notesInput: {
    minHeight: 72,
    textAlignVertical: 'top',
  },
  modalActions: {
    flexDirection: 'row',
    justifyContent: 'flex-end',
    gap: 10,
    marginTop: 16,
  },
  secondaryBtn: {
    paddingVertical: 10,
    paddingHorizontal: 14,
  },
  secondaryBtnText: {
    color: '#666',
    fontWeight: '600',
  },
  primaryBtn: {
    backgroundColor: '#1976d2',
    paddingVertical: 12,
    paddingHorizontal: 16,
    borderRadius: 8,
    alignItems: 'center',
  },
  closingBtn: {
    backgroundColor: '#5e35b1',
  },
  btnDisabled: {
    opacity: 0.5,
  },
  primaryBtnText: {
    color: '#fff',
    fontWeight: '600',
    fontSize: 15,
  },
});

export default DailyClosingModal;
