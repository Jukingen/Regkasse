'use client';

/**
 * Ephemeral UI store: open/close the global license renewal modal from axios / guards.
 * Not for license status data (that stays in React Query via useLicenseStatus).
 */
import { create } from 'zustand';

type LicenseRenewalModalState = {
  open: boolean;
  openModal: () => void;
  closeModal: () => void;
};

export const useLicenseRenewalModalStore = create<LicenseRenewalModalState>((set) => ({
  open: false,
  openModal: () => set({ open: true }),
  closeModal: () => set({ open: false }),
}));

/** Non-React entry (axios interceptor). */
export function openLicenseRenewalModal(): void {
  useLicenseRenewalModalStore.getState().openModal();
}

export function closeLicenseRenewalModal(): void {
  useLicenseRenewalModalStore.getState().closeModal();
}
