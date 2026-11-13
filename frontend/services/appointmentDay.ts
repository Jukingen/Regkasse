export const APPOINTMENT_DAY_START_HOUR = 8;
export const APPOINTMENT_DAY_END_HOUR = 18;
export const APPOINTMENT_SLOT_MINUTES = 30;

const OCCUPYING_STATUSES = new Set(['Booked', 'Confirmed']);

export function buildAppointmentDaySlots(): string[] {
  const slots: string[] = [];
  for (
    let minutes = APPOINTMENT_DAY_START_HOUR * 60;
    minutes < APPOINTMENT_DAY_END_HOUR * 60;
    minutes += APPOINTMENT_SLOT_MINUTES
  ) {
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    slots.push(`${String(hours).padStart(2, '0')}:${String(mins).padStart(2, '0')}`);
  }
  return slots;
}

export function formatLocalDate(value: Date): string {
  const day = String(value.getDate()).padStart(2, '0');
  const month = String(value.getMonth() + 1).padStart(2, '0');
  return `${day}.${month}.${value.getFullYear()}`;
}

export function localHm(value: Date): string {
  return `${String(value.getHours()).padStart(2, '0')}:${String(value.getMinutes()).padStart(2, '0')}`;
}

export function slotRange(day: Date, slotHm: string): { start: Date; end: Date } {
  const [hours, minutes] = slotHm.split(':').map((part) => Number(part));
  const start = new Date(day);
  start.setHours(hours, minutes, 0, 0);
  const end = new Date(start.getTime() + APPOINTMENT_SLOT_MINUTES * 60_000);
  return { start, end };
}

export function appointmentOccupiesSlot(
  startUtc: string,
  endUtc: string,
  status: string,
  day: Date,
  slotHm: string
): boolean {
  if (!OCCUPYING_STATUSES.has(status)) return false;
  const aptStart = new Date(startUtc);
  const aptEnd = new Date(endUtc);
  if (Number.isNaN(aptStart.getTime()) || Number.isNaN(aptEnd.getTime())) return false;
  const { start, end } = slotRange(day, slotHm);
  return aptStart < end && aptEnd > start;
}
