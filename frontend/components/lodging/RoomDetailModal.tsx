import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  ActivityIndicator,
  Modal,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';

import { FolioViewModal } from './FolioViewModal';
import { ROOM_STATUS_BG, ROOM_STATUS_COLOR } from './roomStatusColors';
import { SoftColors, SoftRadius, SoftSpacing } from '../../constants/SoftTheme';
import type {
  GuestFolioDto,
  GuestFolioItemDto,
  RoomDto,
  RoomStatusName,
} from '../../services/api/lodgingService';

const STATUSES: RoomStatusName[] = ['Available', 'Occupied', 'Cleaning', 'Maintenance'];

function statusLabel(t: (key: string) => string, status: RoomStatusName): string {
  switch (status) {
    case 'Available':
      return t('pos.rooms.status.Available');
    case 'Occupied':
      return t('pos.rooms.status.Occupied');
    case 'Cleaning':
      return t('pos.rooms.status.Cleaning');
    case 'Maintenance':
      return t('pos.rooms.status.Maintenance');
  }
}

export function RoomDetailModal(props: {
  visible: boolean;
  room: RoomDto | null;
  folio: GuestFolioDto | null;
  items: GuestFolioItemDto[];
  availableRooms: RoomDto[];
  busy: boolean;
  confirmingOut: boolean;
  chargeDescription: string;
  chargeAmount: string;
  onChargeDescription: (value: string) => void;
  onChargeAmount: (value: string) => void;
  onClose: () => void;
  onCheckIn: () => void;
  onCheckOut: () => void;
  onMoveRoom: (roomId: string) => void;
  onConfirmCheckOut: () => void;
  onToRegister: () => void;
  onCancelCheckOut: () => void;
  onChangeStatus: (status: RoomStatusName) => void;
  onAddCharge: () => void;
}) {
  const { t } = useTranslation('verticalProfiles');
  const [folioOpen, setFolioOpen] = useState(false);
  const { room, folio } = props;
  if (!room) return null;

  const currentStatus = statusLabel(t, room.status);
  const canCheckIn = room.status === 'Available' && !room.occupied && !folio;
  const guestName = folio?.customerName ?? t('pos.rooms.guestUnknown');

  return (
    <Modal visible={props.visible} transparent animationType="fade" onRequestClose={props.onClose}>
      <View style={styles.backdrop}>
        <View
          style={styles.sheet}
          accessibilityLabel={t('pos.rooms.detailTitle', { number: room.number })}
        >
          <ScrollView keyboardShouldPersistTaps="handled">
            <Text style={styles.title}>{t('pos.rooms.detailTitle', { number: room.number })}</Text>
            <Text style={[styles.status, { color: ROOM_STATUS_COLOR[room.status] }]}>{currentStatus}</Text>
            {props.busy ? <ActivityIndicator color={SoftColors.accent} /> : null}
            {!folio ? (
              <Text style={styles.meta}>{t('pos.rooms.noGuest')}</Text>
            ) : (
              <Text style={styles.guest}>
                {t('pos.rooms.guestBalance', {
                  name: guestName,
                  amount: folio.balance.toFixed(2),
                })}
              </Text>
            )}
            {!folio ? <Text style={styles.meta}>{t('pos.rooms.noOpenFolio')}</Text> : null}
            <View style={styles.actions}>
              {canCheckIn ? (
                <Pressable
                  accessibilityRole="button"
                  accessibilityLabel={t('pos.rooms.checkInRoom', { number: room.number })}
                  onPress={props.onCheckIn}
                  style={styles.button}
                >
                  <Text style={styles.buttonText}>{t('pos.rooms.checkIn')}</Text>
                </Pressable>
              ) : null}
              {folio ? (
                <Pressable
                  accessibilityRole="button"
                  accessibilityLabel={t('pos.rooms.showFolio')}
                  disabled={props.busy}
                  onPress={() => setFolioOpen(true)}
                  style={styles.button}
                >
                  <Text style={styles.buttonText}>{t('pos.rooms.showFolio')}</Text>
                </Pressable>
              ) : null}
              {folio && !folioOpen && !props.confirmingOut ? (
                <Pressable
                  accessibilityRole="button"
                  accessibilityLabel={t('pos.rooms.checkOut')}
                  disabled={props.busy}
                  onPress={props.onCheckOut}
                  style={styles.button}
                >
                  <Text style={styles.buttonText}>{t('pos.rooms.checkOut')}</Text>
                </Pressable>
              ) : null}
            </View>
            <Text style={styles.section}>{t('pos.rooms.changeStatus')}</Text>
            <View style={styles.statusRow}>
              {STATUSES.map((status) => {
                const label = statusLabel(t, status);
                const selected = room.status === status;
                return (
                  <Pressable
                    key={status}
                    accessibilityRole="button"
                    accessibilityLabel={t('pos.rooms.setStatus', { status: label })}
                    disabled={props.busy || selected}
                    onPress={() => props.onChangeStatus(status)}
                    style={[
                      styles.statusChip,
                      {
                        borderColor: ROOM_STATUS_COLOR[status],
                        backgroundColor: selected ? ROOM_STATUS_BG[status] : SoftColors.bgCard,
                      },
                    ]}
                  >
                    <Text style={{ color: ROOM_STATUS_COLOR[status], fontWeight: '700' }}>{label}</Text>
                  </Pressable>
                );
              })}
            </View>
            {folio ? (
              <View>
                <Text style={styles.meta}>{t('pos.rooms.balance', { amount: folio.balance.toFixed(2) })}</Text>
                {props.items.length === 0 ? <Text style={styles.meta}>{t('pos.rooms.noCharges')}</Text> : null}
                {props.items.map((item) => (
                  <Text
                    key={item.id}
                    accessibilityLabel={t('pos.rooms.chargeLine', { description: item.description })}
                  >
                    {item.description} · {item.amount.toFixed(2)}
                  </Text>
                ))}
                <TextInput
                  accessibilityLabel={t('pos.rooms.chargeDescription')}
                  value={props.chargeDescription}
                  onChangeText={props.onChargeDescription}
                  placeholder={t('pos.rooms.chargeDescription')}
                  style={styles.input}
                />
                <TextInput
                  accessibilityLabel={t('pos.rooms.chargeAmount')}
                  value={props.chargeAmount}
                  onChangeText={props.onChargeAmount}
                  keyboardType="decimal-pad"
                  placeholder={t('pos.rooms.chargeAmount')}
                  style={styles.input}
                />
                <Pressable
                  accessibilityRole="button"
                  accessibilityLabel={t('pos.rooms.addCharge')}
                  disabled={props.busy}
                  onPress={props.onAddCharge}
                  style={styles.secondary}
                >
                  <Text style={styles.secondaryText}>{t('pos.rooms.addCharge')}</Text>
                </Pressable>
                <Text style={styles.meta}>{t('pos.rooms.noInvoice')}</Text>
                {props.confirmingOut ? (
                  <View>
                    <Text>
                      {t('pos.rooms.checkOutConfirm', { amount: folio.balance.toFixed(2) })}
                    </Text>
                    {folio.balance > 0 ? (
                      <Pressable
                        accessibilityRole="button"
                        accessibilityLabel={t('pos.checkout.toRegister')}
                        disabled={props.busy}
                        onPress={props.onToRegister}
                        style={styles.button}
                      >
                        <Text style={styles.buttonText}>{t('pos.checkout.toRegister')}</Text>
                      </Pressable>
                    ) : null}
                    <Text style={styles.meta}>{t('pos.checkout.stayHint')}</Text>
                    <Pressable
                      accessibilityRole="button"
                      accessibilityLabel={t('pos.rooms.checkOutConfirmAction')}
                      disabled={props.busy}
                      onPress={props.onConfirmCheckOut}
                      style={styles.button}
                    >
                      <Text style={styles.buttonText}>{t('pos.rooms.checkOutConfirmAction')}</Text>
                    </Pressable>
                    <Pressable
                      accessibilityRole="button"
                      accessibilityLabel={t('pos.rooms.cancel')}
                      onPress={props.onCancelCheckOut}
                      style={styles.secondary}
                    >
                      <Text style={styles.secondaryText}>{t('pos.rooms.cancel')}</Text>
                    </Pressable>
                  </View>
                ) : null}
              </View>
            ) : null}
            <FolioViewModal
              visible={folioOpen && props.visible}
              roomNumber={room.number}
              folio={folio}
              items={props.items}
              availableRooms={props.availableRooms}
              busy={props.busy}
              onClose={() => setFolioOpen(false)}
              onCheckOut={() => {
                setFolioOpen(false);
                props.onCheckOut();
              }}
              onMoveRoom={(roomId) => {
                props.onMoveRoom(roomId);
              }}
            />
            <Pressable
              accessibilityRole="button"
              accessibilityLabel={t('pos.rooms.close')}
              onPress={() => {
                setFolioOpen(false);
                props.onClose();
              }}
              style={styles.secondary}
            >
              <Text style={styles.secondaryText}>{t('pos.rooms.close')}</Text>
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
  section: {
    marginTop: SoftSpacing.md,
    fontWeight: '700',
    color: SoftColors.textPrimary,
  },
  status: { marginTop: 4, fontWeight: '700' },
  meta: { color: SoftColors.textSecondary, marginTop: SoftSpacing.xs },
  guest: { color: SoftColors.textPrimary, fontWeight: '700', marginTop: SoftSpacing.sm },
  actions: { marginTop: SoftSpacing.sm },
  statusRow: { flexDirection: 'row', flexWrap: 'wrap', gap: SoftSpacing.sm, marginTop: SoftSpacing.sm },
  statusChip: {
    minHeight: 44,
    justifyContent: 'center',
    borderWidth: 2,
    borderRadius: SoftRadius.sm,
    paddingHorizontal: SoftSpacing.sm,
  },
  input: {
    minHeight: 44,
    borderWidth: 1,
    borderColor: SoftColors.border,
    borderRadius: SoftRadius.sm,
    paddingHorizontal: SoftSpacing.sm,
    marginTop: SoftSpacing.xs,
    marginBottom: SoftSpacing.sm,
    color: SoftColors.textPrimary,
    backgroundColor: SoftColors.bgPrimary,
  },
  button: {
    marginTop: SoftSpacing.sm,
    minHeight: 44,
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: SoftRadius.sm,
    backgroundColor: SoftColors.accent,
    paddingHorizontal: SoftSpacing.md,
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
