import * as Print from 'expo-print';
import * as Sharing from 'expo-sharing';
import { Platform } from 'react-native';

/** Typical ESC/POS / thermal receipt width for expo-print HTML layout. */
export const THERMAL_RECEIPT_PRINT_WIDTH = 300;

export class PrintCancelledError extends Error {
  readonly code = 'PRINT_CANCELLED' as const;

  constructor(message = 'Print dialog was dismissed without printing') {
    super(message);
    this.name = 'PrintCancelledError';
  }
}

export class ShareUnavailableError extends Error {
  readonly code = 'SHARE_UNAVAILABLE' as const;

  constructor(message = 'System sharing is not available on this device') {
    super(message);
    this.name = 'ShareUnavailableError';
  }
}

/**
 * iOS rejects Print.printAsync when the user closes the print UI without printing.
 * Android usually resolves after showing the dialog (cancel is not rejected).
 */
export function isPrintDialogCancelled(error: unknown): boolean {
  if (error instanceof PrintCancelledError) return true;
  const msg = (error instanceof Error ? error.message : String(error)).toLowerCase();
  return (
    msg.includes('printing did not complete') ||
    msg.includes('print was cancelled') ||
    msg.includes('print was canceled') ||
    msg.includes('user cancelled') ||
    msg.includes('user canceled')
  );
}

export function isPrintCancelled(error: unknown): boolean {
  return isPrintDialogCancelled(error);
}

export function isShareUnavailable(error: unknown): boolean {
  return error instanceof ShareUnavailableError;
}

function isShareSheetDismissed(error: unknown): boolean {
  const msg = (error instanceof Error ? error.message : String(error)).toLowerCase();
  return (
    msg.includes('user did not share') ||
    msg.includes('sharing cancelled') ||
    msg.includes('sharing canceled') ||
    msg.includes('share cancelled') ||
    msg.includes('share canceled')
  );
}

/**
 * Opens the native print preview for HTML (thermal width by default).
 * On failure of HTML→print, falls back to PDF file then print-by-URI (often more reliable on Android).
 * User cancel → {@link PrintCancelledError} (callers should treat as soft success, not a printer fault).
 */
export async function printHtmlAsync(html: string, options?: { width?: number }): Promise<void> {
  if (Platform.OS === 'web') {
    throw new Error('printHtmlAsync is for native; use a web iframe print path on web');
  }

  const width = options?.width ?? THERMAL_RECEIPT_PRINT_WIDTH;

  try {
    await Print.printAsync({ html, width });
    return;
  } catch (err) {
    if (isPrintDialogCancelled(err)) {
      throw new PrintCancelledError();
    }
  }

  try {
    const file = await Print.printToFileAsync({ html, width, base64: false });
    await Print.printAsync({ uri: file.uri });
  } catch (fallbackErr) {
    if (isPrintDialogCancelled(fallbackErr)) {
      throw new PrintCancelledError();
    }
    const message = fallbackErr instanceof Error ? fallbackErr.message : 'Printer not available';
    throw new Error(`Print failed: ${message}`);
  }
}

/** Print a local PDF / file URI via the system print UI. */
export async function printPdfUriAsync(uri: string): Promise<void> {
  try {
    await Print.printAsync({ uri });
  } catch (err) {
    if (isPrintDialogCancelled(err)) {
      throw new PrintCancelledError();
    }
    const message = err instanceof Error ? err.message : 'Printer not available';
    throw new Error(`Print failed: ${message}`);
  }
}

/**
 * Share a local file (PDF, APK, …). Checks {@link Sharing.isAvailableAsync} first.
 * Share-sheet dismiss is treated as success (no throw).
 */
export async function shareDocumentAsync(
  uri: string,
  options?: { mimeType?: string; dialogTitle?: string; UTI?: string }
): Promise<void> {
  const available = await Sharing.isAvailableAsync();
  if (!available) {
    throw new ShareUnavailableError();
  }

  try {
    await Sharing.shareAsync(uri, {
      mimeType: options?.mimeType,
      dialogTitle: options?.dialogTitle,
      UTI: options?.UTI,
    });
  } catch (err) {
    if (isShareSheetDismissed(err)) {
      return;
    }
    throw err;
  }
}
