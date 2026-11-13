import type { RoomStatusName } from '../../services/api/lodgingService';

/** Grid tones: green Frei, yellow Belegt, blue Reinigung, gray Wartung. */
export const ROOM_STATUS_COLOR: Record<RoomStatusName, string> = {
  Available: '#15803D',
  Occupied: '#A16207',
  Cleaning: '#1D4ED8',
  Maintenance: '#6B7280',
};

export const ROOM_STATUS_BG: Record<RoomStatusName, string> = {
  Available: '#DCFCE7',
  Occupied: '#FEF9C3',
  Cleaning: '#DBEAFE',
  Maintenance: '#F3F4F6',
};
