import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ActivityIndicator, Alert, Pressable, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { SoftColors, SoftRadius, SoftSpacing } from '../../constants/SoftTheme';
import { useVerticalFeatures } from '../../contexts/VerticalProfileContext';
import {
  listFolioItems,
  listFolios,
  listRooms,
  updateFolio,
  type GuestFolioDto,
  type GuestFolioItemDto,
  type RoomDto,
} from '../../services/api/lodgingService';

const STATUS_COLOR: Record<string, string> = {
  Available: '#1B7F4E',
  Occupied: '#C2410C',
  Cleaning: '#1D4ED8',
  Maintenance: '#6B7280',
};

export default function RoomsScreen() {
  const { t } = useTranslation('verticalProfiles');
  const { profileId, posLayout } = useVerticalFeatures();
  const enabled = profileId === 'beherbergung' || posLayout === 'rooms';
  const [rooms, setRooms] = useState<RoomDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [selected, setSelected] = useState<RoomDto | null>(null);
  const [folio, setFolio] = useState<GuestFolioDto | null>(null);
  const [items, setItems] = useState<GuestFolioItemDto[]>([]);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setRooms(await listRooms());
    } catch {
      setRooms([]);
      Alert.alert(t('screens.lodging.title'), t('screens.lodging.loadFailed'));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    if (!enabled) return;
    void load();
  }, [enabled, load]);

  const openRoom = async (room: RoomDto) => {
    setSelected(room);
    setBusy(true);
    try {
      const folios = await listFolios(room.id, true);
      const open = folios[0] ?? null;
      setFolio(open);
      setItems(open ? await listFolioItems(open.id) : []);
    } catch {
      setFolio(null);
      setItems([]);
      Alert.alert(t('screens.lodging.folioTitle'), t('screens.lodging.loadFailed'));
    } finally {
      setBusy(false);
    }
  };

  const checkOut = async () => {
    if (!folio) return;
    setBusy(true);
    try {
      await updateFolio(folio.id, { status: 'Closed' });
      Alert.alert(t('screens.lodging.checkedOutTitle'), t('screens.lodging.checkedOut'));
      setSelected(null);
      setFolio(null);
      setItems([]);
      await load();
    } catch {
      Alert.alert(t('screens.lodging.saveFailedTitle'), t('screens.lodging.checkOutFailed'));
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
    <SafeAreaView style={styles.page} accessibilityLabel={t('screens.lodging.gridTitle')}>
      <Text style={styles.title}>{t('screens.lodging.gridTitle')}</Text>
      {loading ? <ActivityIndicator color={SoftColors.accent} /> : null}
      {!loading && rooms.length === 0 ? <Text style={styles.meta}>{t('screens.lodging.empty')}</Text> : null}
      <View style={styles.grid}>
        {rooms.map((room) => {
          const label = t(`screens.lodging.status.${room.status}`, { defaultValue: room.status });
          return (
            <Pressable
              key={room.id}
              accessibilityRole="button"
              accessibilityLabel={t('screens.lodging.openRoom', { number: room.number, status: label })}
              onPress={() => {
                openRoom(room).catch(() => undefined);
              }}
              style={[styles.card, { borderColor: STATUS_COLOR[room.status] ?? SoftColors.border }]}
            >
              <Text style={styles.number}>{room.number}</Text>
              <Text style={styles.meta}>{room.type}</Text>
              <Text style={styles.meta}>{label}</Text>
            </Pressable>
          );
        })}
      </View>
      {selected ? (
        <View style={styles.folio} accessibilityLabel={t('screens.lodging.folioTitle')}>
          <Text style={styles.title}>
            {t('screens.lodging.folioRoom', { number: selected.number })}
          </Text>
          {busy ? <ActivityIndicator color={SoftColors.accent} /> : null}
          {!folio ? <Text style={styles.meta}>{t('screens.lodging.noOpenFolio')}</Text> : null}
          {folio ? (
            <>
              <Text style={styles.meta}>
                {folio.customerName ?? t('screens.lodging.guestUnknown')} · {folio.balance.toFixed(2)}
              </Text>
              {items.length === 0 ? <Text style={styles.meta}>{t('screens.lodging.noCharges')}</Text> : null}
              {items.map((item) => (
                <Text key={item.id} accessibilityLabel={t('screens.lodging.chargeLine', { description: item.description })}>
                  {item.description} · {item.amount.toFixed(2)}
                </Text>
              ))}
              <Pressable
                accessibilityRole="button"
                accessibilityLabel={t('screens.lodging.checkOut')}
                disabled={busy}
                onPress={() => {
                  checkOut().catch(() => undefined);
                }}
                style={styles.button}
              >
                <Text style={styles.buttonText}>{t('screens.lodging.checkOut')}</Text>
              </Pressable>
            </>
          ) : null}
        </View>
      ) : null}
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  page: { flex: 1, padding: SoftSpacing.md, backgroundColor: SoftColors.bgPrimary },
  title: { fontSize: 20, fontWeight: '700', color: SoftColors.textPrimary, marginBottom: SoftSpacing.sm },
  grid: { flexDirection: 'row', flexWrap: 'wrap', gap: SoftSpacing.sm },
  card: {
    width: 140,
    minHeight: 96,
    borderWidth: 2,
    borderRadius: SoftRadius.md,
    padding: SoftSpacing.sm,
    marginBottom: SoftSpacing.sm,
    backgroundColor: SoftColors.bgPrimary,
  },
  number: { fontSize: 22, fontWeight: '700', color: SoftColors.textPrimary },
  meta: { color: SoftColors.textSecondary, marginTop: 2 },
  folio: {
    marginTop: SoftSpacing.md,
    padding: SoftSpacing.md,
    borderWidth: 1,
    borderColor: SoftColors.border,
    borderRadius: SoftRadius.md,
  },
  button: {
    marginTop: SoftSpacing.md,
    minHeight: 44,
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: SoftRadius.sm,
    backgroundColor: SoftColors.accent,
  },
  buttonText: { color: SoftColors.textInverse, fontWeight: '700' },
});
