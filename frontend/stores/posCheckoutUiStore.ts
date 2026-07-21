import { create } from 'zustand';

/**
 * Ephemeral POS checkout UI only (not persisted).
 *
 * Allowed here: selected payment-method chip + submit-attempt flag.
 * Not allowed: tokens, user profile, cart lines, payment card/voucher secrets,
 * or TSE payloads — those live in AuthContext / secureStorage / CartContext / payment APIs.
 *
 * Cart line items: `contexts/CartContext` (not Zustand).
 * Theme / language: `ThemeContext` + `i18n` (not Zustand).
 */
type PosCheckoutUiState = {
  selectedPaymentMethodType: string | null;
  paymentMethodSubmitAttempted: boolean;
  setSelectedPaymentMethodType: (type: string | null) => void;
  setPaymentMethodSubmitAttempted: (value: boolean) => void;
  resetCheckoutPaymentUi: () => void;
};

export const usePosCheckoutUiStore = create<PosCheckoutUiState>((set) => ({
  selectedPaymentMethodType: null,
  paymentMethodSubmitAttempted: false,
  setSelectedPaymentMethodType: (type) => {
    set({ selectedPaymentMethodType: type, paymentMethodSubmitAttempted: false });
  },
  setPaymentMethodSubmitAttempted: (value) => {
    set({ paymentMethodSubmitAttempted: value });
  },
  resetCheckoutPaymentUi: () => {
    set({ selectedPaymentMethodType: null, paymentMethodSubmitAttempted: false });
  },
}));

/** Primitive selectors — subscribe only to the slice you render. */
export const selectSelectedPaymentMethodType = (s: PosCheckoutUiState) =>
  s.selectedPaymentMethodType;
export const selectPaymentMethodSubmitAttempted = (s: PosCheckoutUiState) =>
  s.paymentMethodSubmitAttempted;

/** Stable action accessors (prefer over hooking actions when you only need to call them). */
export const posCheckoutUiActions = {
  setSelectedPaymentMethodType: (type: string | null) => {
    usePosCheckoutUiStore.getState().setSelectedPaymentMethodType(type);
  },
  setPaymentMethodSubmitAttempted: (value: boolean) => {
    usePosCheckoutUiStore.getState().setPaymentMethodSubmitAttempted(value);
  },
  resetCheckoutPaymentUi: () => {
    usePosCheckoutUiStore.getState().resetCheckoutPaymentUi();
  },
};
