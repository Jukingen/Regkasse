import { Ionicons } from '@expo/vector-icons';
import React from 'react';
import { useTranslation } from 'react-i18next';
import { Modal, Pressable, StyleSheet, Text, View } from 'react-native';

import { SoftColors, SoftRadius, SoftShadows, SoftSpacing, SoftTypography } from '../constants/SoftTheme';
import type { PosShortcutAction } from '../utils/posKeyboardShortcuts';

type PosShortcutsHelpModalProps = {
  visible: boolean;
  onClose: () => void;
  /** Actions that have a handler on this screen; others are hidden. */
  available: readonly PosShortcutAction[];
};

/** Cheat sheet for the web keyboard shortcuts (opened with ? or F1). */
export function PosShortcutsHelpModal({ visible, onClose, available }: PosShortcutsHelpModalProps) {
  const { t } = useTranslation(['checkout', 'common']);
  const has = (action: PosShortcutAction) => available.includes(action);

  const rows: { id: string; keys: string; label: string }[] = [];
  if (has('help')) rows.push({ id: 'help', keys: 'F1 / ?', label: t('checkout:posFlow.shortcuts.help') });
  if (has('customer')) rows.push({ id: 'customer', keys: 'F2', label: t('checkout:posFlow.shortcuts.customer') });
  if (has('receipts')) rows.push({ id: 'receipts', keys: 'F3', label: t('checkout:posFlow.shortcuts.receipts') });
  if (has('kitchen')) rows.push({ id: 'kitchen', keys: 'F4', label: t('checkout:posFlow.shortcuts.kitchen') });
  if (has('clearCart')) rows.push({ id: 'clearCart', keys: 'F5', label: t('checkout:posFlow.shortcuts.clearCart') });
  if (has('pay')) rows.push({ id: 'pay', keys: 'F6', label: t('checkout:posFlow.shortcuts.pay') });
  if (has('escape')) rows.push({ id: 'escape', keys: 'Esc', label: t('checkout:posFlow.shortcuts.escape') });

  return (
    <Modal visible={visible} transparent animationType="fade" onRequestClose={onClose}>
      <Pressable
        style={styles.backdrop}
        onPress={onClose}
        accessibilityRole="button"
        accessibilityLabel={t('checkout:posFlow.shortcuts.close')}>
        <Pressable
          style={styles.card}
          onPress={() => undefined}
          testID="pos-shortcuts-help"
          accessibilityLabel={t('checkout:posFlow.shortcuts.title')}>
          <View style={styles.header}>
            <Text style={styles.title} accessibilityRole="header">
              {t('checkout:posFlow.shortcuts.title')}
            </Text>
            <Pressable
              onPress={onClose}
              hitSlop={10}
              accessibilityRole="button"
              accessibilityLabel={t('checkout:posFlow.shortcuts.close')}>
              <Ionicons name="close" size={24} color={SoftColors.textPrimary} />
            </Pressable>
          </View>
          {rows.map((row) => (
            <View key={row.id} style={styles.row} testID={`pos-shortcut-${row.id}`}>
              <View style={styles.keyChip}>
                <Text style={styles.keyText}>{row.keys}</Text>
              </View>
              <Text style={styles.label}>{row.label}</Text>
            </View>
          ))}
          <Text style={styles.footer}>{t('checkout:posFlow.shortcuts.footer')}</Text>
        </Pressable>
      </Pressable>
    </Modal>
  );
}

const styles = StyleSheet.create({
  backdrop: {
    flex: 1,
    backgroundColor: SoftColors.overlay,
    alignItems: 'center',
    justifyContent: 'center',
    padding: SoftSpacing.lg,
  },
  card: {
    width: '100%',
    maxWidth: 420,
    backgroundColor: SoftColors.bgPrimary,
    borderRadius: SoftRadius.lg,
    padding: SoftSpacing.lg,
    ...SoftShadows.md,
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: SoftSpacing.md,
  },
  title: {
    ...SoftTypography.h2,
    color: SoftColors.textPrimary,
  },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: SoftColors.border,
  },
  keyChip: {
    minWidth: 72,
    paddingVertical: SoftSpacing.xs,
    paddingHorizontal: SoftSpacing.sm,
    borderRadius: SoftRadius.sm,
    backgroundColor: SoftColors.bgSecondary,
    alignItems: 'center',
  },
  keyText: {
    ...SoftTypography.body,
    fontWeight: '700',
    color: SoftColors.textPrimary,
  },
  label: {
    ...SoftTypography.body,
    color: SoftColors.textPrimary,
    flex: 1,
  },
  footer: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
    marginTop: SoftSpacing.md,
  },
});
