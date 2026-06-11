import { Ionicons } from '@expo/vector-icons';
import { useLocalSearchParams, useRouter } from 'expo-router';
import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Alert,
  FlatList,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { SoftColors, SoftRadius, SoftSpacing, SoftTypography } from '../../constants/SoftTheme';
import { useCart } from '../../contexts/CartContext';
import { splitService, type SplitItemDto, type SplitSessionDto } from '../../services/api/splitService';
import { WaveLoader } from '../../src/components/common/WaveLoader';
import { VALID_TABLE_NUMBERS } from '../../utils/tableCartUtils';
import { requestMergeSheet } from '../../utils/pendingPosNav';

type Seat = {
  id: number;
  customerName: string;
  itemIds: string[];
};

export type SplitScreenProps = {
  cartRowId: string;
  tableNumber: number;
  onComplete?: (action: 'split' | 'merge') => void;
};

function formatEuro(amount: number): string {
  return `${amount.toFixed(2)} €`;
}

function seatLabel(seatId: number): string {
  return `Gast ${seatId}`;
}

function buildSeatsFromSession(session: SplitSessionDto): Seat[] {
  const bySeat = new Map<number, Seat>();
  for (const item of session.items) {
    if (!item.customerName.trim()) continue;
    const seatId = item.seatNumber > 0 ? item.seatNumber : 1;
    const existing = bySeat.get(seatId) ?? {
      id: seatId,
      customerName: item.customerName,
      itemIds: [],
    };
    existing.itemIds.push(item.id);
    if (!existing.customerName.trim()) existing.customerName = item.customerName;
    bySeat.set(seatId, existing);
  }
  if (bySeat.size === 0) {
    return [{ id: 1, customerName: '', itemIds: [] }];
  }
  return Array.from(bySeat.values()).sort((a, b) => a.id - b.id);
}

export function SplitScreenContent({ cartRowId, tableNumber, onComplete }: SplitScreenProps) {
  const router = useRouter();
  const { fetchTableCart } = useCart();

  const [session, setSession] = useState<SplitSessionDto | null>(null);
  const [seats, setSeats] = useState<Seat[]>([{ id: 1, customerName: '', itemIds: [] }]);
  const [selectedSeat, setSelectedSeat] = useState(1);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      try {
        const started = await splitService.start(cartRowId);
        if (cancelled) return;
        setSession(started);
        setSeats(buildSeatsFromSession(started));
        setSelectedSeat(1);
      } catch {
        if (!cancelled) {
          Alert.alert('Fehler', 'Rechnung konnte nicht zum Teilen vorbereitet werden.', [
            { text: 'OK', onPress: () => router.back() },
          ]);
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [cartRowId, router]);

  const assignedIds = useMemo(() => new Set(seats.flatMap((s) => s.itemIds)), [seats]);

  const unassignedItems = useMemo(
    () => (session?.items ?? []).filter((item) => !assignedIds.has(item.id)),
    [session?.items, assignedIds]
  );

  const itemById = useMemo(() => {
    const map = new Map<string, SplitItemDto>();
    for (const item of session?.items ?? []) map.set(item.id, item);
    return map;
  }, [session?.items]);

  const calculateSeatTotal = useCallback(
    (seat: Seat) =>
      seat.itemIds.reduce((sum, id) => sum + (itemById.get(id)?.lineTotal ?? 0), 0),
    [itemById]
  );

  const syncAssign = useCallback(
    async (itemId: string, customerName: string, seatNumber: number) => {
      if (!session) return;
      await splitService.assignItem(session.id, itemId, customerName, seatNumber);
    },
    [session]
  );

  const addSeat = useCallback(() => {
    setSeats((prev) => {
      const nextId = prev.length > 0 ? Math.max(...prev.map((s) => s.id)) + 1 : 1;
      setSelectedSeat(nextId);
      return [...prev, { id: nextId, customerName: '', itemIds: [] }];
    });
  }, []);

  const updateSeatName = useCallback(
    (seatId: number, name: string) => {
      setSeats((prev) =>
        prev.map((seat) => (seat.id === seatId ? { ...seat, customerName: name } : seat))
      );
      if (!session) return;
      const seat = seats.find((s) => s.id === seatId);
      if (!seat?.itemIds.length) return;
      const trimmed = name.trim() || seatLabel(seatId);
      void Promise.all(
        seat.itemIds.map((itemId) => syncAssign(itemId, trimmed, seatId))
      ).catch(() => {
        Alert.alert('Fehler', 'Kundenname konnte nicht gespeichert werden.');
      });
    },
    [session, seats, syncAssign]
  );

  const moveItemToSeat = useCallback(
    async (item: SplitItemDto, seatId: number) => {
      if (!session) return;
      const seat = seats.find((s) => s.id === seatId);
      const customerName = (seat?.customerName.trim() || seatLabel(seatId)).trim();
      setBusy(true);
      try {
        await syncAssign(item.id, customerName, seatId);
        setSeats((prev) =>
          prev.map((s) => {
            const without = s.itemIds.filter((id) => id !== item.id);
            if (s.id === seatId) {
              return { ...s, customerName: s.customerName.trim() || customerName, itemIds: [...without, item.id] };
            }
            return { ...s, itemIds: without };
          })
        );
      } catch {
        Alert.alert('Fehler', 'Position konnte nicht zugewiesen werden.');
      } finally {
        setBusy(false);
      }
    },
    [session, seats, syncAssign]
  );

  const removeItemFromSeat = useCallback(
    async (itemId: string, seatId: number) => {
      if (!session) return;
      setBusy(true);
      try {
        await syncAssign(itemId, '', 0);
        setSeats((prev) =>
          prev.map((seat) =>
            seat.id === seatId
              ? { ...seat, itemIds: seat.itemIds.filter((id) => id !== itemId) }
              : seat
          )
        );
      } catch {
        Alert.alert('Fehler', 'Zuweisung konnte nicht entfernt werden.');
      } finally {
        setBusy(false);
      }
    },
    [session]
  );

  const refreshAllTables = useCallback(async () => {
    await Promise.all(VALID_TABLE_NUMBERS.map((n) => fetchTableCart(n, true)));
  }, [fetchTableCart]);

  const handleCompleteSplit = useCallback(async () => {
    if (!session) return;
    if (unassignedItems.length > 0) {
      Alert.alert('Hinweis', 'Bitte alle Positionen einem Gast zuweisen.');
      return;
    }
    setBusy(true);
    try {
      await splitService.complete(session.id);
      await refreshAllTables();
      onComplete?.('split');
      router.back();
    } catch {
      Alert.alert('Fehler', 'Separate Rechnungen konnten nicht erstellt werden.');
    } finally {
      setBusy(false);
    }
  }, [session, unassignedItems.length, refreshAllTables, onComplete, router]);

  const handleMerge = useCallback(() => {
    onComplete?.('merge');
    requestMergeSheet();
    router.back();
  }, [onComplete, router]);

  if (loading) {
    return (
      <View style={styles.centered}>
        <WaveLoader size={32} color={SoftColors.accent} />
        <Text style={styles.loadingText}>Rechnung wird vorbereitet…</Text>
      </View>
    );
  }

  return (
    <View style={styles.container}>
      <View style={styles.header}>
        <Pressable onPress={() => router.back()} style={styles.iconBtn}>
          <Ionicons name="arrow-back" size={24} color={SoftColors.textPrimary} />
        </Pressable>
        <View style={styles.headerText}>
          <Text style={styles.title}>Rechnung teilen</Text>
          <Text style={styles.subtitle}>Tisch {tableNumber}</Text>
        </View>
      </View>

      <View style={styles.seatList}>
        <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.seatScroll}>
          {seats.map((seat) => (
            <Pressable
              key={seat.id}
              style={[styles.seatCard, selectedSeat === seat.id && styles.selectedSeat]}
              onPress={() => setSelectedSeat(seat.id)}
            >
              <Text style={styles.seatTitle}>Platz {seat.id}</Text>
              <TextInput
                style={styles.seatInput}
                placeholder="Kundenname"
                placeholderTextColor={SoftColors.textMuted}
                value={seat.customerName}
                onChangeText={(text) => updateSeatName(seat.id, text)}
              />
              <Text style={styles.seatMeta}>{seat.itemIds.length} Positionen</Text>
              <Text style={styles.seatTotal}>{formatEuro(calculateSeatTotal(seat))}</Text>
              {seat.itemIds.length > 0 ? (
                <View style={styles.seatItems}>
                  {seat.itemIds.map((itemId) => {
                    const item = itemById.get(itemId);
                    if (!item) return null;
                    return (
                      <Pressable
                        key={itemId}
                        style={styles.seatItemRow}
                        onPress={() => void removeItemFromSeat(itemId, seat.id)}
                      >
                        <Text style={styles.seatItemText} numberOfLines={1}>
                          {item.productName} ×{item.quantity}
                        </Text>
                        <Ionicons name="close-circle" size={16} color={SoftColors.textMuted} />
                      </Pressable>
                    );
                  })}
                </View>
              ) : null}
            </Pressable>
          ))}
          <Pressable style={styles.addSeatButton} onPress={addSeat}>
            <Ionicons name="add" size={20} color={SoftColors.accent} />
            <Text style={styles.addSeatText}>Platz hinzufügen</Text>
          </Pressable>
        </ScrollView>
      </View>

      <View style={styles.itemsList}>
        <Text style={styles.sectionLabel}>Offene Positionen</Text>
        <FlatList
          data={unassignedItems}
          keyExtractor={(item) => item.id}
          style={styles.list}
          ListEmptyComponent={
            <Text style={styles.empty}>Alle Positionen sind zugewiesen.</Text>
          }
          renderItem={({ item }) => (
            <View style={styles.itemRow}>
              <View style={styles.itemInfo}>
                <Text style={styles.itemName}>
                  {item.productName} ×{item.quantity}
                </Text>
                <Text style={styles.itemPrice}>{formatEuro(item.lineTotal)}</Text>
              </View>
              <Pressable
                style={[styles.assignBtn, busy && styles.assignBtnDisabled]}
                onPress={() => void moveItemToSeat(item, selectedSeat)}
                disabled={busy}
              >
                <Text style={styles.assignBtnText}>→ Platz {selectedSeat}</Text>
              </Pressable>
            </View>
          )}
        />
      </View>

      <View style={styles.actions}>
        <Pressable style={styles.mergeButton} onPress={handleMerge} disabled={busy}>
          <Ionicons name="git-merge-outline" size={18} color={SoftColors.textPrimary} />
          <Text style={styles.mergeButtonText}>Rechnungen zusammenführen</Text>
        </Pressable>
        <Pressable
          style={[styles.splitButton, busy && styles.splitButtonDisabled]}
          onPress={() => void handleCompleteSplit()}
          disabled={busy}
        >
          {busy ? (
            <WaveLoader size={20} color={SoftColors.textInverse} />
          ) : (
            <>
              <Ionicons name="cut-outline" size={18} color={SoftColors.textInverse} />
              <Text style={styles.splitButtonText}>Separate Rechnungen erstellen</Text>
            </>
          )}
        </Pressable>
      </View>
    </View>
  );
}

export default function SplitScreenRoute() {
  const router = useRouter();
  const params = useLocalSearchParams<{ tableNumber?: string }>();
  const tableNumber = Number(params.tableNumber ?? '1');
  const { getCartForTable, fetchTableCart } = useCart();
  const cart = getCartForTable(tableNumber);
  const cartRowId = cart.cartRowId;
  const [hydrating, setHydrating] = useState(!cartRowId && cart.items.length > 0);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      if (cartRowId || cart.items.length === 0) {
        setHydrating(false);
        return;
      }
      setHydrating(true);
      try {
        await fetchTableCart(tableNumber, true);
      } finally {
        if (!cancelled) setHydrating(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [cartRowId, cart.items.length, fetchTableCart, tableNumber]);

  if (hydrating) {
    return (
      <SafeAreaView style={styles.safe}>
        <View style={styles.centered}>
          <WaveLoader size={32} color={SoftColors.accent} />
        </View>
      </SafeAreaView>
    );
  }

  const resolvedCart = getCartForTable(tableNumber);
  const resolvedCartRowId = resolvedCart.cartRowId;

  if (!resolvedCartRowId || resolvedCart.items.length === 0) {
    return (
      <SafeAreaView style={styles.safe}>
        <View style={styles.centered}>
          <Text style={styles.errorText}>Kein aktiver Warenkorb für Tisch {tableNumber}.</Text>
          <Pressable style={styles.backBtn} onPress={() => router.back()}>
            <Text style={styles.backBtnText}>Zurück</Text>
          </Pressable>
        </View>
      </SafeAreaView>
    );
  }

  return (
    <SafeAreaView style={styles.safe}>
      <SplitScreenContent cartRowId={resolvedCartRowId} tableNumber={tableNumber} />
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: SoftColors.bgPrimary },
  container: { flex: 1, backgroundColor: SoftColors.bgPrimary, padding: SoftSpacing.md },
  centered: { flex: 1, alignItems: 'center', justifyContent: 'center', padding: SoftSpacing.lg },
  loadingText: { marginTop: SoftSpacing.md, color: SoftColors.textMuted },
  errorText: { ...SoftTypography.body, color: SoftColors.textPrimary, textAlign: 'center' },
  backBtn: {
    marginTop: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm,
    paddingHorizontal: SoftSpacing.lg,
    borderRadius: SoftRadius.md,
    backgroundColor: SoftColors.accent,
  },
  backBtnText: { color: SoftColors.textInverse, fontWeight: '600' },
  header: { flexDirection: 'row', alignItems: 'center', marginBottom: SoftSpacing.md, gap: SoftSpacing.sm },
  iconBtn: { padding: SoftSpacing.xs },
  headerText: { flex: 1 },
  title: { ...SoftTypography.h2, color: SoftColors.textPrimary },
  subtitle: { ...SoftTypography.caption, color: SoftColors.textMuted, marginTop: 2 },
  seatList: { marginBottom: SoftSpacing.md },
  seatScroll: { gap: SoftSpacing.sm, paddingVertical: SoftSpacing.xs },
  seatCard: {
    width: 168,
    padding: SoftSpacing.sm,
    borderRadius: SoftRadius.md,
    borderWidth: 1,
    borderColor: SoftColors.border,
    backgroundColor: SoftColors.bgSecondary,
  },
  selectedSeat: { borderColor: SoftColors.accent, borderWidth: 2 },
  seatTitle: { fontWeight: '700', color: SoftColors.textPrimary, marginBottom: SoftSpacing.xs },
  seatInput: {
    borderWidth: 1,
    borderColor: SoftColors.border,
    borderRadius: SoftRadius.sm,
    paddingHorizontal: SoftSpacing.sm,
    paddingVertical: 6,
    marginBottom: SoftSpacing.xs,
    color: SoftColors.textPrimary,
    backgroundColor: SoftColors.bgPrimary,
  },
  seatMeta: { fontSize: 12, color: SoftColors.textMuted },
  seatTotal: { fontWeight: '700', color: SoftColors.textPrimary, marginTop: 2 },
  seatItems: { marginTop: SoftSpacing.xs, gap: 4 },
  seatItemRow: { flexDirection: 'row', alignItems: 'center', gap: 4 },
  seatItemText: { flex: 1, fontSize: 11, color: SoftColors.textSecondary },
  addSeatButton: {
    width: 120,
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: SoftRadius.md,
    borderWidth: 1,
    borderStyle: 'dashed',
    borderColor: SoftColors.accent,
    gap: 4,
  },
  addSeatText: { color: SoftColors.accent, fontWeight: '600', fontSize: 12 },
  itemsList: { flex: 1 },
  sectionLabel: {
    ...SoftTypography.caption,
    color: SoftColors.textSecondary,
    marginBottom: SoftSpacing.xs,
  },
  list: { flex: 1 },
  empty: { color: SoftColors.textMuted, textAlign: 'center', marginTop: SoftSpacing.lg },
  itemRow: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: SoftSpacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: SoftColors.border,
    gap: SoftSpacing.sm,
  },
  itemInfo: { flex: 1 },
  itemName: { fontSize: 15, color: SoftColors.textPrimary },
  itemPrice: { fontSize: 13, color: SoftColors.textMuted, marginTop: 2 },
  assignBtn: {
    paddingVertical: SoftSpacing.xs,
    paddingHorizontal: SoftSpacing.sm,
    borderRadius: SoftRadius.sm,
    backgroundColor: SoftColors.accentLight,
  },
  assignBtnDisabled: { opacity: 0.5 },
  assignBtnText: { color: SoftColors.accent, fontWeight: '600', fontSize: 12 },
  actions: { gap: SoftSpacing.sm, marginTop: SoftSpacing.md },
  mergeButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: SoftSpacing.xs,
    paddingVertical: SoftSpacing.md,
    borderRadius: SoftRadius.md,
    borderWidth: 1,
    borderColor: SoftColors.border,
  },
  mergeButtonText: { color: SoftColors.textPrimary, fontWeight: '600' },
  splitButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: SoftSpacing.xs,
    paddingVertical: SoftSpacing.md,
    borderRadius: SoftRadius.md,
    backgroundColor: SoftColors.accent,
  },
  splitButtonDisabled: { opacity: 0.6 },
  splitButtonText: { color: SoftColors.textInverse, fontWeight: '700' },
});
