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

import { ROOM_STATUS_BG } from './roomStatusColors';
import { stayInstantUtc, todayIsoDate } from './stayDates';
import { SoftColors, SoftRadius, SoftSpacing } from '../../constants/SoftTheme';
import { customerService } from '../../services/api/customerService';
import type { CreateGuestFolioRequest, RoomDto } from '../../services/api/lodgingService';

function readCustomer(raw: unknown): { id: string; name: string } | null {
  if (!raw || typeof raw !== 'object') return null;
  const row = raw as Record<string, unknown>;
  const nested =
    row.data && typeof row.data === 'object' ? (row.data as Record<string, unknown>) : row;
  const id = String(nested.id ?? nested.Id ?? '').trim();
  if (!id) return null;
  return { id, name: String(nested.name ?? nested.Name ?? '').trim() };
}

export function CheckInModal(props: {
  visible: boolean;
  rooms: RoomDto[];
  initialRoomId?: string | null;
  busy: boolean;
  onClose: () => void;
  onSubmit: (body: CreateGuestFolioRequest) => void | Promise<void>;
  onValidation: (message: string) => void;
}) {
  const { t } = useTranslation('verticalProfiles');
  const { visible, rooms, initialRoomId, busy, onClose, onSubmit, onValidation } = props;
  const [guestQuery, setGuestQuery] = useState('');
  const [guestName, setGuestName] = useState('');
  const [guest, setGuest] = useState<{ id: string; name: string } | null>(null);
  const [checkInDate, setCheckInDate] = useState(todayIsoDate);
  const [nights, setNights] = useState('');
  const [assignRoomId, setAssignRoomId] = useState<string | null>(initialRoomId ?? null);
  const [searching, setSearching] = useState(false);

  const freeRooms = rooms.filter((room) => room.status === 'Available' && !room.occupied);

  const findGuest = async () => {
    const query = guestQuery.trim();
    if (!query) {
      onValidation(t('pos.checkIn.customerRequired'));
      return;
    }
    setSearching(true);
    try {
      const found = await customerService.lookupByQrPayload(query);
      if (!found?.id) {
        setGuest(null);
        onValidation(t('pos.checkIn.guestNotFound'));
        return;
      }
      setGuest({ id: found.id, name: found.name });
    } catch {
      onValidation(t('pos.checkIn.guestNotFound'));
    } finally {
      setSearching(false);
    }
  };

  const createGuest = async () => {
    const name = guestName.trim();
    if (!name) {
      onValidation(t('pos.checkIn.guestNameRequired'));
      return;
    }
    setSearching(true);
    try {
      const created = readCustomer(
        await customerService.create({
          name,
          phone: '',
          address: '',
        })
      );
      if (!created) {
        onValidation(t('pos.checkIn.guestCreateFailed'));
        return;
      }
      setGuest(created);
    } catch {
      onValidation(t('pos.checkIn.guestCreateFailed'));
    } finally {
      setSearching(false);
    }
  };

  const submit = () => {
    if (!guest?.id) {
      onValidation(t('pos.checkIn.customerRequired'));
      return;
    }
    const date = checkInDate.trim();
    if (!/^\d{4}-\d{2}-\d{2}$/.test(date)) {
      onValidation(t('pos.checkIn.dateInvalid'));
      return;
    }
    const nightsText = nights.trim();
    let extraNights = 0;
    if (nightsText) {
      if (!/^\d+$/.test(nightsText) || Number(nightsText) < 1 || Number(nightsText) > 366) {
        onValidation(t('pos.checkIn.nightsInvalid'));
        return;
      }
      extraNights = Number(nightsText);
    }
    const room = rooms.find((row) => row.id === assignRoomId) ?? null;
    if (!room || room.occupied || room.status !== 'Available') {
      onValidation(t('pos.checkIn.roomOccupied'));
      return;
    }
    const body: CreateGuestFolioRequest = {
      customerId: guest.id,
      roomId: room.id,
      checkIn: stayInstantUtc(date),
    };
    if (extraNights > 0) {
      body.checkOut = stayInstantUtc(date, extraNights);
    }
    return onSubmit(body);
  };

  return (
    <Modal visible={visible} transparent animationType="fade" onRequestClose={onClose}>
      <View style={styles.backdrop}>
        <View style={styles.sheet} accessibilityLabel={t('pos.checkIn.title')}>
          <ScrollView keyboardShouldPersistTaps="handled">
            <Text style={styles.title}>{t('pos.checkIn.title')}</Text>
            <Text style={styles.meta}>{t('pos.checkIn.nonFiscal')}</Text>
            <Text style={styles.meta}>{t('pos.checkIn.searchPlaceholder')}</Text>
            <TextInput
              accessibilityLabel={t('pos.checkIn.searchPlaceholder')}
              value={guestQuery}
              onChangeText={setGuestQuery}
              autoCapitalize="none"
              style={styles.input}
            />
            <Pressable
              accessibilityRole="button"
              accessibilityLabel={t('pos.checkIn.searchGuest')}
              onPress={() => findGuest()}
              style={styles.secondary}
            >
              <Text style={styles.secondaryText}>{t('pos.checkIn.searchGuest')}</Text>
            </Pressable>
            <Text style={styles.meta}>{t('pos.checkIn.guestName')}</Text>
            <TextInput
              accessibilityLabel={t('pos.checkIn.guestName')}
              value={guestName}
              onChangeText={setGuestName}
              style={styles.input}
            />
            <Pressable
              accessibilityRole="button"
              accessibilityLabel={t('pos.checkIn.createGuest')}
              onPress={() => {
                createGuest().catch(() => undefined);
              }}
              style={styles.secondary}
            >
              <Text style={styles.secondaryText}>{t('pos.checkIn.createGuest')}</Text>
            </Pressable>
            {searching ? <ActivityIndicator color={SoftColors.accent} /> : null}
            {guest ? <Text style={styles.guest}>{t('pos.checkIn.guest', { name: guest.name })}</Text> : null}
            <Text style={styles.meta}>{t('pos.checkIn.checkInDate')}</Text>
            <TextInput
              accessibilityLabel={t('pos.checkIn.checkInDate')}
              value={checkInDate}
              onChangeText={setCheckInDate}
              autoCapitalize="none"
              style={styles.input}
            />
            <Text style={styles.meta}>{t('pos.checkIn.nights')}</Text>
            <TextInput
              accessibilityLabel={t('pos.checkIn.nights')}
              value={nights}
              onChangeText={setNights}
              keyboardType="number-pad"
              style={styles.input}
            />
            <Text style={styles.section}>{t('pos.checkIn.assignRoom')}</Text>
            {freeRooms.length === 0 ? <Text style={styles.meta}>{t('pos.checkIn.noFreeRoom')}</Text> : null}
            {freeRooms.map((room) => (
              <Pressable
                key={room.id}
                accessibilityRole="button"
                accessibilityLabel={t('pos.checkIn.select', { number: room.number })}
                onPress={() => setAssignRoomId(room.id)}
                style={[styles.roomRow, assignRoomId === room.id && styles.roomSelected]}
              >
                <Text style={styles.roomNumber}>
                  {room.number} · {room.type}
                </Text>
              </Pressable>
            ))}
            <Pressable
              accessibilityRole="button"
              accessibilityLabel={t('pos.checkIn.submit')}
              disabled={busy}
              onPress={submit}
              style={[styles.button, busy && styles.disabled]}
            >
              <Text style={styles.buttonText}>
                {busy ? t('pos.checkIn.saving') : t('pos.checkIn.submit')}
              </Text>
            </Pressable>
            <Pressable
              accessibilityRole="button"
              accessibilityLabel={t('pos.checkIn.cancel')}
              onPress={onClose}
              style={styles.secondary}
            >
              <Text style={styles.secondaryText}>{t('pos.checkIn.cancel')}</Text>
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
  title: {
    fontSize: 20,
    fontWeight: '700',
    color: SoftColors.textPrimary,
    marginBottom: SoftSpacing.sm,
  },
  section: {
    fontSize: 16,
    fontWeight: '700',
    color: SoftColors.textPrimary,
    marginBottom: SoftSpacing.sm,
  },
  meta: { color: SoftColors.textSecondary, marginTop: 2 },
  guest: { color: SoftColors.textPrimary, fontWeight: '700', marginVertical: SoftSpacing.sm },
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
  roomRow: {
    minHeight: 44,
    justifyContent: 'center',
    borderWidth: 1,
    borderColor: SoftColors.border,
    borderRadius: SoftRadius.sm,
    paddingHorizontal: SoftSpacing.sm,
    marginBottom: SoftSpacing.sm,
    backgroundColor: ROOM_STATUS_BG.Available,
  },
  roomSelected: { borderColor: SoftColors.accent, borderWidth: 2 },
  roomNumber: { color: SoftColors.textPrimary, fontWeight: '700' },
  button: {
    marginTop: SoftSpacing.md,
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
    marginBottom: SoftSpacing.sm,
    marginTop: SoftSpacing.sm,
  },
  secondaryText: { color: SoftColors.accent, fontWeight: '700' },
  disabled: { opacity: 0.6 },
});
