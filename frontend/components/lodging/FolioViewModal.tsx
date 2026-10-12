import React from 'react';
import { useTranslation } from 'react-i18next';
import { Modal, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';

import { SoftColors, SoftRadius, SoftSpacing } from '../../constants/SoftTheme';
import type { GuestFolioDto, GuestFolioItemDto } from '../../services/api/lodgingService';

function formatChargeTime(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return iso;
  return date.toLocaleString('de-DE', { dateStyle: 'short', timeStyle: 'short' });
}

export function FolioViewModal(props: {
  visible: boolean;
  roomNumber: string;
  folio: GuestFolioDto | null;
  items: GuestFolioItemDto[];
  busy: boolean;
  onClose: () => void;
  onCheckOut: () => void;
}) {
  const { t } = useTranslation('verticalProfiles');
  const folio = props.folio;
  if (!folio) return null;

  return (
    <Modal visible={props.visible} transparent animationType="fade" onRequestClose={props.onClose}>
      <View style={styles.backdrop}>
        <View
          style={styles.sheet}
          accessibilityLabel={t('pos.folio.title', { room: props.roomNumber })}
        >
          <ScrollView>
            <Text style={styles.title}>{t('pos.folio.title', { room: props.roomNumber })}</Text>
            <Text style={styles.balance}>
              {t('pos.folio.balance', { amount: folio.balance.toFixed(2) })}
            </Text>
            {props.items.length === 0 ? <Text style={styles.meta}>{t('pos.folio.empty')}</Text> : null}
            {props.items.map((item) => {
              const time = formatChargeTime(item.createdAtUtc);
              return (
                <Text
                  key={item.id}
                  accessibilityLabel={t('pos.folio.chargeLine', {
                    description: item.description,
                    amount: item.amount.toFixed(2),
                    time,
                  })}
                >
                  {item.description} · {item.amount.toFixed(2)} · {time}
                </Text>
              );
            })}
            <Pressable
              accessibilityRole="button"
              accessibilityLabel={t('pos.rooms.checkOut')}
              disabled={props.busy}
              onPress={props.onCheckOut}
              style={styles.button}
            >
              <Text style={styles.buttonText}>{t('pos.rooms.checkOut')}</Text>
            </Pressable>
            <Pressable
              accessibilityRole="button"
              accessibilityLabel={t('pos.folio.close')}
              onPress={props.onClose}
              style={styles.secondary}
            >
              <Text style={styles.secondaryText}>{t('pos.folio.close')}</Text>
            </Pressable>
          </ScrollView>
        </View>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  backdrop: {
    flex: 1,
    backgroundColor: SoftColors.overlay,
    justifyContent: 'center',
    padding: SoftSpacing.md,
  },
  sheet: {
    maxHeight: '90%',
    backgroundColor: SoftColors.bgCard,
    borderRadius: SoftRadius.lg,
    padding: SoftSpacing.md,
  },
  title: { fontSize: 20, fontWeight: '700', color: SoftColors.textPrimary },
  balance: { marginTop: SoftSpacing.sm, fontWeight: '700', color: SoftColors.textPrimary },
  meta: { color: SoftColors.textSecondary, marginTop: SoftSpacing.xs },
  button: {
    marginTop: SoftSpacing.md,
    minHeight: 44,
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: SoftRadius.sm,
    backgroundColor: SoftColors.accent,
  },
  buttonText: { color: SoftColors.textInverse, fontWeight: '700' },
  secondary: {
    minHeight: 44,
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: SoftRadius.sm,
    borderWidth: 1,
    borderColor: SoftColors.accent,
    marginTop: SoftSpacing.sm,
  },
  secondaryText: { color: SoftColors.accent, fontWeight: '700' },
});
