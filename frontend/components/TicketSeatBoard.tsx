import React, { useCallback, useEffect, useRef, useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import {
  ticketEventService,
  type SoldSeatTicket,
  type TicketEventSummary,
  type TicketSeat,
} from '../services/api/ticketEventService';

type Props = {
  onError?: (message: string) => void;
};

export function TicketSeatBoard({ onError }: Props) {
  const { t } = useTranslation('verticalProfiles');
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

  const paySelected = async () => {
    if (!eventId || !selectedSeatId || busySeat) return;
    setBusySeat(selectedSeatId);
    try {
      const ticket = await ticketEventService.sell(eventId, selectedSeatId);
      setPrinted(ticket);
      setSelectedSeatId(null);
      await loadSeats(eventId);
    } catch {
      onErrorRef.current?.(t('screens.tickets.seatTaken'));
      await loadSeats(eventId);
    } finally {
      setBusySeat(null);
    }
  };

  if (loading) {
    return <ActivityIndicator testID="ticket-seat-loading" />;
  }

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
        {seats.map((seat) => (
          <Pressable
            key={seat.id}
            testID={`ticket-seat-${seat.row}-${seat.number}`}
            disabled={seat.status === 'Sold' || busySeat === seat.id}
            onPress={() => void reserveSeat(seat)}
            style={[styles.seat, seat.id === selectedSeatId && styles.seatSelected, seatStyle(seat.status)]}
          >
            <Text style={styles.seatText}>{seat.row}{seat.number}</Text>
          </Pressable>
        ))}
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

function seatStyle(status: string) {
  if (status === 'Sold') return styles.seatSold;
  if (status === 'Reserved') return styles.seatReserved;
  return styles.seatFree;
}

const styles = StyleSheet.create({
  wrap: { gap: 8, marginBottom: 12 },
  label: { fontWeight: '600' },
  events: { gap: 8 },
  eventChip: { padding: 8, borderWidth: 1, borderColor: '#ccc', borderRadius: 8, marginRight: 8 },
  eventChipActive: { borderColor: '#1677ff', backgroundColor: '#e6f4ff' },
  eventName: { fontWeight: '600' },
  eventVenue: { fontSize: 12, color: '#666' },
  grid: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  seat: { width: 48, height: 40, borderRadius: 6, alignItems: 'center', justifyContent: 'center' },
  seatFree: { backgroundColor: '#d9f7be' },
  seatReserved: { backgroundColor: '#fff1b8' },
  seatSold: { backgroundColor: '#ffccc7' },
  seatSelected: { borderWidth: 2, borderColor: '#1677ff' },
  pay: { alignSelf: 'flex-start', backgroundColor: '#1677ff', borderRadius: 8, paddingHorizontal: 12, paddingVertical: 8 },
  payText: { color: '#fff', fontWeight: '700' },
  seatText: { fontSize: 12, fontWeight: '600' },
  print: { padding: 12, borderWidth: 1, borderColor: '#222', borderRadius: 8 },
  printTitle: { fontWeight: '700', marginBottom: 4 },
});
