import { useFocusEffect, useRouter } from 'expo-router';
import React, { useCallback, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  ActivityIndicator,
  Alert,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { CheckInModal } from '../../components/lodging/CheckInModal';
import { beginFolioCheckout } from '../../components/lodging/folioCheckoutHandoff';
import { pickStayCarrier } from '../../components/lodging/pickStayCarrier';
import { RoomDetailModal } from '../../components/lodging/RoomDetailModal';
import { ROOM_STATUS_BG, ROOM_STATUS_COLOR } from '../../components/lodging/roomStatusColors';
import { SoftColors, SoftRadius, SoftSpacing } from '../../constants/SoftTheme';
import { useVerticalFeatures } from '../../contexts/VerticalProfileContext';
import {
  chargeFolio,
  createFolio,
  listFolioItems,
  listFolios,
  listRooms,
  stageCheckout,
  updateFolio,
  updateRoomStatus,
  type CreateGuestFolioRequest,
  type GuestFolioDto,
  type GuestFolioItemDto,
  type RoomDto,
  type RoomStatusName,
} from '../../services/api/lodgingService';
import { getAllProducts } from '../../services/api/productService';

function roomStatusLabel(t: (key: string) => string, status: RoomStatusName): string {
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

export default function RoomsScreen() {
  const { t } = useTranslation('verticalProfiles');
  const router = useRouter();
  const { profileId, posLayout } = useVerticalFeatures();
  const enabled = profileId === 'beherbergung' || posLayout === 'rooms';
  const [rooms, setRooms] = useState<RoomDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [selected, setSelected] = useState<RoomDto | null>(null);
  const [detailOpen, setDetailOpen] = useState(false);
  const [checkInOpen, setCheckInOpen] = useState(false);
  const [checkInKey, setCheckInKey] = useState(0);
  const [assignRoomId, setAssignRoomId] = useState<string | null>(null);
  const [folio, setFolio] = useState<GuestFolioDto | null>(null);
  const [items, setItems] = useState<GuestFolioItemDto[]>([]);
  const [busy, setBusy] = useState(false);
  const [confirmingOut, setConfirmingOut] = useState(false);
  const [chargeDescription, setChargeDescription] = useState('');
  const [chargeAmount, setChargeAmount] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const rows = await listRooms();
      setRooms(rows);
      setSelected((current) =>
        current ? rows.find((row) => row.id === current.id) ?? current : current
      );
      return rows;
    } catch {
      setRooms([]);
      Alert.alert(t('pos.rooms.gridTitle'), t('pos.rooms.loadFailed'));
      return [];
    } finally {
      setLoading(false);
    }
  }, [t]);

  const openRoom = useCallback(
    async (room: RoomDto) => {
      setSelected(room);
      setDetailOpen(true);
      setConfirmingOut(false);
      setBusy(true);
      try {
        const folios = await listFolios(room.id, true);
        const open = folios[0] ?? null;
        setFolio(open);
        setItems(open ? await listFolioItems(open.id) : []);
      } catch {
        setFolio(null);
        setItems([]);
        Alert.alert(t('pos.rooms.detailTitle', { number: room.number }), t('pos.rooms.loadFailed'));
      } finally {
        setBusy(false);
      }
    },
    [t]
  );

  useFocusEffect(
    useCallback(() => {
      if (!enabled) return;
      void load();
    }, [enabled, load])
  );

  const startCheckIn = (roomId?: string) => {
    setAssignRoomId(roomId ?? null);
    setCheckInKey((value) => value + 1);
    setCheckInOpen(true);
  };

  const submitCheckIn = async (body: CreateGuestFolioRequest) => {
    setBusy(true);
    try {
      await createFolio(body);
      Alert.alert(t('pos.checkIn.savedTitle'), t('pos.checkIn.saved'));
      setCheckInOpen(false);
      const rows = await load();
      const refreshed = rows.find((row) => row.id === body.roomId);
      if (refreshed) {
        await openRoom({ ...refreshed, status: 'Occupied', occupied: true });
      }
    } catch {
      Alert.alert(t('pos.checkIn.saveFailedTitle'), t('pos.checkIn.saveFailed'));
    } finally {
      setBusy(false);
    }
  };

  const addCharge = async () => {
    if (!folio) return;
    const amount = Number(chargeAmount.replace(',', '.'));
    if (!Number.isFinite(amount) || amount <= 0) {
      Alert.alert(t('pos.rooms.validationTitle'), t('pos.rooms.amountInvalid'));
      return;
    }
    setBusy(true);
    try {
      const updated = await chargeFolio(folio.id, {
        description: chargeDescription.trim() || t('pos.rooms.chargeDefault'),
        amount,
      });
      setFolio(updated.id ? { ...folio, ...updated } : folio);
      setItems(await listFolioItems(folio.id));
      setChargeDescription('');
      setChargeAmount('');
      Alert.alert(
        t('pos.rooms.chargedTitle'),
        t('pos.rooms.chargedBalance', {
          amount: (updated.balance || folio.balance + amount).toFixed(2),
        })
      );
    } catch {
      Alert.alert(t('pos.rooms.saveFailedTitle'), t('pos.rooms.chargeFailed'));
    } finally {
      setBusy(false);
    }
  };

  const sendToRegister = async () => {
    if (!folio || !selected) return;
    if (folio.balance <= 0) {
      Alert.alert(t('pos.checkout.stageFailedTitle'), t('pos.checkout.zeroBalance'));
      return;
    }
    setBusy(true);
    try {
      const carrier = pickStayCarrier(await getAllProducts());
      if (!carrier) {
        Alert.alert(t('pos.checkout.noProductTitle'), t('pos.checkout.noProduct'));
        return;
      }
      const staged = await stageCheckout(folio.id, carrier.id);
      beginFolioCheckout({
        folioId: folio.id,
        roomNumber: selected.number,
        balance: Number(staged.balance || folio.balance),
        tableNumber: staged.tableNumber || 1,
      });
      setConfirmingOut(false);
      setDetailOpen(false);
      router.push('/(tabs)/cash-register');
    } catch {
      Alert.alert(t('pos.checkout.stageFailedTitle'), t('pos.checkout.stageFailed'));
    } finally {
      setBusy(false);
    }
  };

  const performCheckOut = async () => {
    if (!folio) return;
    setBusy(true);
    try {
      await updateFolio(folio.id, { status: 'Closed' });
      Alert.alert(t('pos.rooms.checkedOutTitle'), t('pos.rooms.checkedOut'));
      setSelected(null);
      setFolio(null);
      setItems([]);
      setConfirmingOut(false);
      setDetailOpen(false);
      await load();
    } catch {
      Alert.alert(t('pos.rooms.saveFailedTitle'), t('pos.rooms.checkOutFailed'));
    } finally {
      setBusy(false);
    }
  };

  const changeStatus = async (status: RoomStatusName) => {
    if (!selected) return;
    setBusy(true);
    try {
      const updated = await updateRoomStatus(selected.id, status);
      setSelected(updated);
      await load();
      Alert.alert(t('pos.rooms.statusSavedTitle'), t('pos.rooms.statusSaved'));
    } catch {
      Alert.alert(t('pos.rooms.saveFailedTitle'), t('pos.rooms.statusFailed'));
    } finally {
      setBusy(false);
    }
  };

  if (!enabled) {
    return (
      <SafeAreaView style={styles.page}>
        <Text>{t('screens.unavailable')}</Text>
      </SafeAreaView>
    );
  }

  return (
    <SafeAreaView style={styles.page} accessibilityLabel={t('pos.rooms.gridTitle')}>
      <ScrollView contentContainerStyle={styles.scroll}>
        <View style={styles.headerRow}>
          <Text style={styles.title}>{t('pos.rooms.gridTitle')}</Text>
          <Pressable
            accessibilityRole="button"
            accessibilityLabel={t('pos.rooms.checkIn')}
            onPress={() => startCheckIn()}
            style={styles.button}
          >
            <Text style={styles.buttonText}>{t('pos.rooms.checkIn')}</Text>
          </Pressable>
        </View>
        {loading ? <ActivityIndicator color={SoftColors.accent} /> : null}
        {!loading && rooms.length === 0 ? <Text style={styles.meta}>{t('pos.rooms.empty')}</Text> : null}
        <View style={styles.grid}>
          {rooms.map((room) => {
            const label = roomStatusLabel(t, room.status);
            const tone = ROOM_STATUS_COLOR[room.status] ?? SoftColors.border;
            return (
              <Pressable
                key={room.id}
                testID={`room-card-${room.id}`}
                accessibilityRole="button"
                accessibilityLabel={t('pos.rooms.openRoom', {
                  number: room.number,
                  status: label,
                })}
                onPress={() => openRoom(room)}
                style={[
                  styles.card,
                  { borderColor: tone, backgroundColor: ROOM_STATUS_BG[room.status] ?? SoftColors.bgPrimary },
                ]}
              >
                <Text style={styles.number}>{room.number}</Text>
                <Text style={styles.meta}>{room.type}</Text>
                <Text style={[styles.status, { color: tone }]}>{label}</Text>
              </Pressable>
            );
          })}
        </View>
      </ScrollView>
      <RoomDetailModal
        visible={detailOpen}
        room={selected}
        folio={folio}
        items={items}
        busy={busy}
        confirmingOut={confirmingOut}
        chargeDescription={chargeDescription}
        chargeAmount={chargeAmount}
        onChargeDescription={setChargeDescription}
        onChargeAmount={setChargeAmount}
        onClose={() => {
          setDetailOpen(false);
          setConfirmingOut(false);
        }}
        onCheckIn={() => startCheckIn(selected?.id)}
        onCheckOut={() => setConfirmingOut(true)}
        onConfirmCheckOut={() => performCheckOut()}
        onToRegister={() => sendToRegister()}
        onCancelCheckOut={() => setConfirmingOut(false)}
        onChangeStatus={(status) => {
          changeStatus(status).catch(() => undefined);
        }}
        onAddCharge={() => {
          addCharge().catch(() => undefined);
        }}
      />
      <CheckInModal
        key={checkInKey}
        visible={checkInOpen}
        rooms={rooms}
        initialRoomId={assignRoomId}
        busy={busy}
        onClose={() => setCheckInOpen(false)}
        onValidation={(message) => Alert.alert(t('pos.checkIn.validationTitle'), message)}
        onSubmit={(body) => submitCheckIn(body)}
      />
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  page: { flex: 1, backgroundColor: SoftColors.bgPrimary },
  scroll: { padding: SoftSpacing.md },
  headerRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: SoftSpacing.sm,
  },
  title: { fontSize: 20, fontWeight: '700', color: SoftColors.textPrimary, marginBottom: SoftSpacing.sm },
  grid: { flexDirection: 'row', flexWrap: 'wrap', gap: SoftSpacing.sm },
  card: {
    width: 140,
    minHeight: 96,
    borderWidth: 2,
    borderRadius: SoftRadius.md,
    padding: SoftSpacing.sm,
    marginBottom: SoftSpacing.sm,
  },
  number: { fontSize: 22, fontWeight: '700', color: SoftColors.textPrimary },
  meta: { color: SoftColors.textSecondary, marginTop: 2 },
  status: { marginTop: 4, fontWeight: '700' },
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
});
