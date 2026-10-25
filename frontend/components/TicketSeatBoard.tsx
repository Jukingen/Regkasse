import React, { useCallback, useEffect, useRef, useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import { Colors } from '../constants/Colors';
import { SoftColors } from '../constants/SoftTheme';
import {
  ticketEventService,
  type SoldSeatTicket,
  type TicketEventSummary,
  type TicketSeat,
} from '../services/api/ticketEventService';
import { readTicketSeatErrorCode } from './ticketSeatNotice';

type Props = {
  onError?: (message: string) => void;
};

type SeatCell =
  | { kind: 'seat'; seat: TicketSeat }
  | { kind: 'aisle'; key: string };

/** Category fill from the existing POS palette. Role only changes the border. Price stays a label. */
const seatCategoryColors = {
  standard: SoftColors.bgSecondary,
  vip: SoftColors.warning,
  student: SoftColors.info,
} as const;

const seatRoleBorder = {
  Accessible: SoftColors.focusRing,
  Companion: Colors.accent,
} as const;

export function layoutSeatRows(seats: TicketSeat[]): { row: string; cells: SeatCell[] }[] {
  const order: string[] = [];
  const grouped = new Map<string, TicketSeat[]>();
  for (const seat of seats) {
    const row = seat.row ?? '';
    if (!grouped.has(row)) {
      grouped.set(row, []);
      order.push(row);
    }
    grouped.get(row)!.push(seat);
  }
  return order.map((row) => ({ row, cells: layoutRow(grouped.get(row) ?? []) }));
}

function hasGridColumn(seat: TicketSeat): seat is TicketSeat & { gridColumn: number } {
  return typeof seat.gridColumn === 'number' && Number.isInteger(seat.gridColumn) && seat.gridColumn >= 1;
}

function layoutRow(seats: TicketSeat[]): SeatCell[] {
  const placed = seats.filter(hasGridColumn);
  const rest = seats.filter((seat) => !hasGridColumn(seat));
  if (placed.length === 0) {
    return seats.map((seat) => ({ kind: 'seat', seat }));
  }
  const max = Math.max(...placed.map((seat) => seat.gridColumn));
  const cells: SeatCell[] = Array.from({ length: max }, (_, index) => ({
    kind: 'aisle',
    key: `aisle-${index + 1}`,
  }));
  const overflow: TicketSeat[] = [];
  for (const seat of placed) {
    const index = seat.gridColumn - 1;
    if (cells[index]?.kind === 'aisle') cells[index] = { kind: 'seat', seat };
    else overflow.push(seat);
  }
  return [
    ...cells,
    ...overflow.map((seat) => ({ kind: 'seat' as const, seat })),
    ...rest.map((seat) => ({ kind: 'seat' as const, seat })),
  ];
}

function seatCategoryColor(seat: TicketSeat): string {
  const code = (seat.categoryCode ?? '').trim().toLowerCase();
  if (code === 'vip') return seatCategoryColors.vip;
  if (code === 'student') return seatCategoryColors.student;
  return seatCategoryColors.standard;
}

export function TicketSeatBoard({ onError }: Props) {
  const { t, i18n } = useTranslation('verticalProfiles');
  const onErrorRef = useRef(onError);
  onErrorRef.current = onError;
  const [events, setEvents] = useState<TicketEventSummary[]>([]);
  const [eventId, setEventId] = useState<string | null>(null);
  const [seats, setSeats] = useState<TicketSeat[]>([]);
  const [loading, setLoading] = useState(true);
  const [busySeat, setBusySeat] = useState<string | null>(null);
  const [selectedSeatId, setSelectedSeatId] = useState<string | null>(null);
  const [printed, setPrinted] = useState<SoldSeatTicket | null>(null);

  const loadEvents = useCallback(async () => {
    setLoading(true);
    try {
      const list = await ticketEventService.list();
      setEvents(list);
      setEventId((current) => current ?? list[0]?.id ?? null);
    } catch {
      onErrorRef.current?.(t('screens.tickets.loadError'));
    } finally {
      setLoading(false);
    }
  }, [t]);

  const loadSeats = useCallback(async (id: string) => {
    try {
      setSeats(await ticketEventService.seats(id));
    } catch {
      onErrorRef.current?.(t('screens.tickets.loadError'));
    }
  }, [t]);

  useEffect(() => {
    void loadEvents();
  }, [loadEvents]);

  useEffect(() => {
    if (eventId) void loadSeats(eventId);
  }, [eventId, loadSeats]);

  const companionTargetBlocked = (seat: TicketSeat): boolean => {
    if (seat.seatRole !== 'Companion' || !seat.companionOfSeatId) return false;
    const target = seats.find((item) => item.id === seat.companionOfSeatId);
    return !target || target.status !== 'Sold';
  };

  const reserveSeat = async (seat: TicketSeat) => {
    if (!eventId || seat.status === 'Sold' || busySeat) return;
    setBusySeat(seat.id);
    setPrinted(null);
    try {
      if (seat.status !== 'Reserved') {
        await ticketEventService.reserve(eventId, seat.id);
      }
      setSelectedSeatId(seat.id);
      await loadSeats(eventId);
    } catch {
      onErrorRef.current?.(t('screens.tickets.seatTaken'));
      await loadSeats(eventId);
    } finally {
      setBusySeat(null);
    }
  };

  const onSeatPress = (seat: TicketSeat) => {
    if (companionTargetBlocked(seat)) {
      onErrorRef.current?.('COMPANION_TARGET_NOT_SOLD');
      return;
    }
    void reserveSeat(seat);
  };

  const paySelected = async () => {
    if (!eventId || !selectedSeatId || busySeat) return;
    setBusySeat(selectedSeatId);
    try {
      const ticket = await ticketEventService.sell(eventId, selectedSeatId);
      setPrinted(ticket);
      setSelectedSeatId(null);
      await loadSeats(eventId);
    } catch (error) {
      const code = readTicketSeatErrorCode(error);
      if (code) {
        setSelectedSeatId(null);
        onErrorRef.current?.(code);
      } else {
        onErrorRef.current?.(t('screens.tickets.seatTaken'));
      }
      if (eventId) await loadSeats(eventId);
    } finally {
      setBusySeat(null);
    }
  };

  const categoryLabel = (seat: TicketSeat): string => {
    if (seat.categoryName) return seat.categoryName;
    const code = (seat.categoryCode ?? '').trim().toLowerCase();
    if (code === 'vip') return t('screens.tickets.seatCategoryVip');
    if (code === 'student') return t('screens.tickets.seatCategoryStudent');
    if (code === 'standard') return t('screens.tickets.seatCategoryStandard');
    return '';
  };

  const companionOfText = (seat: TicketSeat): string | null => {
    if (seat.seatRole !== 'Companion' || !seat.companionOfSeatId) return null;
    const target = seats.find((item) => item.id === seat.companionOfSeatId);
    if (!target) return null;
    return t('screens.tickets.companionOfLabel', { row: target.row, number: target.number });
  };

  const priceLabel = (price: number): string => {
    const formatted = new Intl.NumberFormat(i18n.language === 'de' ? 'de-DE' : i18n.language, {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }).format(price);
    return `${formatted} €`;
  };

  if (loading) {
    return <ActivityIndicator testID="ticket-seat-loading" />;
  }

  const selected = events.find((item) => item.id === eventId);
  const stageEdge = selected?.stageEdge === 'bottom' ? 'bottom' : 'top';
  const rows = layoutSeatRows(seats);
  const stage = (
    <Text testID="ticket-stage-label" style={styles.stage}>
      {t('screens.tickets.stageLabel')}
    </Text>
  );

  return (
    <View testID="ticket-seat-board" style={styles.wrap}>
      <Text style={styles.label}>{t('screens.tickets.eventLabel')}</Text>
      <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.events}>
        {events.map((item) => (
          <Pressable
            key={item.id}
            testID={`ticket-event-${item.id}`}
            onPress={() => setEventId(item.id)}
            style={[styles.eventChip, item.id === eventId && styles.eventChipActive]}
          >
            <Text style={styles.eventName}>{item.name}</Text>
            <Text style={styles.eventVenue}>{item.venue}</Text>
          </Pressable>
        ))}
      </ScrollView>
      <View testID="ticket-seat-grid" style={styles.grid}>
        {eventId && seats.length === 0 ? (
          <Text testID="ticket-seat-empty">{t('screens.tickets.emptySeats')}</Text>
        ) : (
          <>
            {stageEdge === 'top' ? stage : null}
            {rows.map((row) => (
              <View key={row.row} testID={`ticket-seat-row-${row.row}`} style={styles.row}>
                {row.cells.map((cell) =>
                  cell.kind === 'aisle' ? (
                    <View
                      key={cell.key}
                      testID="ticket-seat-aisle"
                      accessibilityLabel={t('screens.tickets.seatAisle')}
                      style={styles.aisle}
                    />
                  ) : (
                    <SeatChip
                      key={cell.seat.id}
                      seat={cell.seat}
                      selected={cell.seat.id === selectedSeatId}
                      busy={busySeat === cell.seat.id}
                      label={categoryLabel(cell.seat)}
                      priceText={typeof cell.seat.price === 'number' ? priceLabel(cell.seat.price) : null}
                      accessibleText={t('screens.tickets.seatAccessible')}
                      companionText={t('screens.tickets.seatCompanion')}
                      companionOfText={companionOfText(cell.seat)}
                      onPress={() => onSeatPress(cell.seat)}
                    />
                  ),
                )}
              </View>
            ))}
            {stageEdge === 'bottom' ? stage : null}
          </>
        )}
      </View>
      {selectedSeatId ? (
        <Pressable testID="ticket-seat-pay" onPress={() => void paySelected()} style={styles.pay}>
          <Text style={styles.payText}>{t('screens.tickets.paySeat')}</Text>
        </Pressable>
      ) : null}
      {printed ? (
        <View testID="ticket-print" style={styles.print}>
          <Text style={styles.printTitle}>{t('screens.tickets.printedTitle')}</Text>
          <Text>{printed.eventName}</Text>
          <Text>{printed.seatLabel}</Text>
          <Text testID="ticket-print-code">{printed.code}</Text>
        </View>
      ) : null}
    </View>
  );
}

function SeatChip({
  seat,
  selected,
  busy,
  label,
  priceText,
  accessibleText,
  companionText,
  companionOfText,
  onPress,
}: {
  seat: TicketSeat;
  selected: boolean;
  busy: boolean;
  label: string;
  priceText: string | null;
  accessibleText: string;
  companionText: string;
  companionOfText: string | null;
  onPress: () => void;
}) {
  const backgroundColor = seatCategoryColor(seat);
  const roleBorder =
    seat.seatRole === 'Accessible' || seat.seatRole === 'Companion'
      ? { borderWidth: 2, borderColor: seatRoleBorder[seat.seatRole] }
      : null;
  return (
    <Pressable
      testID={`ticket-seat-${seat.row}-${seat.number}`}
      accessibilityLabel={[`${seat.row}${seat.number}`, label, priceText, companionOfText].filter(Boolean).join(' ')}
      disabled={seat.status === 'Sold' || busy}
      onPress={onPress}
      style={[
        styles.seat,
        { backgroundColor },
        roleBorder,
        seat.status === 'Sold' && styles.seatSold,
        selected && styles.seatSelected,
      ]}
    >
      <Text style={styles.seatText}>{seat.row}{seat.number}</Text>
      {priceText ? <Text style={styles.price}>{priceText}</Text> : null}
      {seat.seatRole === 'Accessible' ? (
        <Text style={styles.role}>♿ {accessibleText}</Text>
      ) : null}
      {seat.seatRole === 'Companion' ? (
        <Text style={styles.role}>{companionText}</Text>
      ) : null}
      {companionOfText ? (
        <Text style={styles.role}>{`↳ ${companionOfText}`}</Text>
      ) : null}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  wrap: { gap: 8, marginBottom: 12 },
  label: { fontWeight: '600' },
  events: { gap: 8 },
  eventChip: { padding: 8, borderWidth: 1, borderColor: '#ccc', borderRadius: 8, marginRight: 8 },
  eventChipActive: { borderColor: '#1677ff', backgroundColor: '#e6f4ff' },
  eventName: { fontWeight: '600' },
  eventVenue: { fontSize: 12, color: '#666' },
  grid: { gap: 8 },
  stage: { alignSelf: 'center', fontWeight: '700', letterSpacing: 1 },
  row: { flexDirection: 'row', gap: 8 },
  aisle: { width: 64, height: 64 },
  seat: {
    width: 64,
    minHeight: 64,
    borderRadius: 6,
    alignItems: 'center',
    justifyContent: 'center',
    padding: 4,
  },
  seatSold: { opacity: 0.45 },
  seatSelected: { borderWidth: 2, borderColor: '#1677ff' },
  pay: { alignSelf: 'flex-start', backgroundColor: '#1677ff', borderRadius: 8, paddingHorizontal: 12, paddingVertical: 8 },
  payText: { color: '#fff', fontWeight: '700' },
  seatText: { fontSize: 12, fontWeight: '600' },
  price: { fontSize: 10 },
  role: { fontSize: 9, fontWeight: '700' },
  print: { padding: 12, borderWidth: 1, borderColor: '#222', borderRadius: 8 },
  printTitle: { fontWeight: '700', marginBottom: 4 },
});
