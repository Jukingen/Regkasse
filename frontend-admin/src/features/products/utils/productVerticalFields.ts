/** Product form duration/staff fields follow vertical posFeatures, not fiscal flags. */
export function shouldShowDurationAndStaffFields(
  posFeatures: Record<string, boolean> | undefined | null
): boolean {
  return posFeatures?.serviceDuration === true || posFeatures?.appointment === true;
}

export function shouldShowImeiTracking(
  posFeatures: Record<string, boolean> | undefined | null
): boolean {
  return posFeatures?.imeiTracking === true;
}

export function shouldShowTicketFlag(
  profileId: string | undefined | null,
  posFeatures?: Record<string, boolean> | undefined | null
): boolean {
  return profileId === 'ticket-sales' || posFeatures?.ticketScan === true;
}
