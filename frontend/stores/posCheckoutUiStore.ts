import { create } from 'zustand';

/**
 * POS checkout UI state shared across payment surfaces.
 * Cart line items remain in CartContext; this store holds checkout-only selections.
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
  setSelectedPaymentMethodType: (type) =>
    set({ selectedPaymentMethodType: type, paymentMethodSubmitAttempted: false }),
  setPaymentMethodSubmitAttempted: (value) => set({ paymentMethodSubmitAttempted: value }),
  resetCheckoutPaymentUi: () =>
    set({ selectedPaymentMethodType: null, paymentMethodSubmitAttempted: false }),
}));
