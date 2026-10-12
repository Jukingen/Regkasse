/** Server-priced stay line handed from the rooms screen to the cash register. */
export type PendingFolioCheckout = {
  folioId: string;
  roomNumber: string;
  balance: number;
  tableNumber: number;
  openPayment: boolean;
};

let pending: PendingFolioCheckout | null = null;

export function beginFolioCheckout(value: Omit<PendingFolioCheckout, 'openPayment'>): void {
  pending = { ...value, openPayment: true };
}

export function consumeOpenPayment(): PendingFolioCheckout | null {
  if (!pending?.openPayment) return null;
  pending = { ...pending, openPayment: false };
  return pending;
}

export function takeFolioCheckout(): PendingFolioCheckout | null {
  const value = pending;
  pending = null;
  return value;
}
