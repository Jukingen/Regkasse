import { Alert } from 'react-native';

import { postCreateMonatsbeleg } from '../services/api/rksvSpecialReceiptsService';
import { receiptPrinter } from '../services/receiptPrinter';

export type CreatePosMonatsbelegParams = {
  cashRegisterId: string;
  year: number;
  month: number;
  reason?: string | null;
};

export type CreatePosMonatsbelegResult = {
  paymentId: string;
  printed: boolean;
  isDecemberAnnual: boolean;
};

/** POST special-receipt Monatsbeleg (December → Jahresbeleg semantics) and best-effort print. */
export async function createPosMonatsbelegAndPrint(
  params: CreatePosMonatsbelegParams,
): Promise<CreatePosMonatsbelegResult> {
  const isDecemberAnnual = params.month === 12;
  const created = await postCreateMonatsbeleg({
    cashRegisterId: params.cashRegisterId,
    year: params.year,
    month: params.month,
    reason:
      params.reason ??
      (isDecemberAnnual ? 'POS Jahresbeleg (Dezember)' : 'POS Monatsbeleg'),
  });

  let printed = false;
  try {
    await receiptPrinter.print(String(created.paymentId));
    printed = true;
  } catch {
    /* best-effort print */
  }

  return { paymentId: String(created.paymentId), printed, isDecemberAnnual };
}

export function alertPosMonatsbelegCreateSuccess(result: CreatePosMonatsbelegResult): void {
  const title = result.isDecemberAnnual ? 'Jahresbeleg' : 'Monatsbeleg';
  const okBody = result.isDecemberAnnual
    ? 'Es wurde ein Jahresbeleg erstellt.'
    : 'Es wurde ein Monatsbeleg erstellt.';
  const failPrint = result.isDecemberAnnual
    ? 'Es wurde ein Jahresbeleg erstellt. Der automatische Druck ist fehlgeschlagen — Beleg später erneut drucken.'
    : 'Es wurde ein Monatsbeleg erstellt. Der automatische Druck ist fehlgeschlagen — Beleg später erneut drucken.';
  Alert.alert(title, result.printed ? okBody : failPrint);
}

export function alertPosMonatsbelegCreateError(error: unknown, isDecemberAnnual: boolean): void {
  const err = error as { data?: { message?: string }; message?: string };
  const msg = err?.data?.message ?? err?.message ?? 'Unbekannter Fehler';
  Alert.alert(isDecemberAnnual ? 'Jahresbeleg' : 'Monatsbeleg', String(msg));
}

/**
 * December requires an irreversible Jahresbeleg confirm; other months run immediately.
 */
export function requestPosMonatsbelegCreate(options: {
  year: number;
  month: number;
  run: () => void | Promise<void>;
}): void {
  if (options.month === 12) {
    Alert.alert('Jahresbeleg erstellen', 'Dieser Vorgang kann nicht rückgängig gemacht werden.', [
      { text: 'Abbrechen', style: 'cancel' },
      { text: 'Erstellen', onPress: () => void options.run() },
    ]);
    return;
  }
  void options.run();
}
