'use client';

import { useParams } from 'next/navigation';

import { TicketSeatEditorPage } from '@/features/tickets/TicketSeatEditorPage';

export default function AdminTicketEventSeatsRoutePage() {
  const params = useParams<{ id: string }>();
  const eventId = typeof params.id === 'string' ? params.id : '';
  return <TicketSeatEditorPage eventId={eventId} />;
}
