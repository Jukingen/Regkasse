import * as Clipboard from 'expo-clipboard';
import { Share } from 'react-native';

export type CopyTextResult =
  { ok: true; method: 'clipboard' } | { ok: true; method: 'share' } | { ok: false; method: 'none' };

/**
 * Write plain text to the system clipboard.
 *
 * Permissions:
 * - iOS / Android: writing does not require a runtime permission prompt.
 * - Web: `setStringAsync` may resolve to `false` when the Clipboard API is
 *   unavailable (non-secure context) or the write is denied.
 * - Reading (`getStringAsync`) can prompt on iOS 16+ / Web; this helper never reads.
 *
 * @returns `true` when the string was saved; `false` on empty input, denial, or error.
 */
export async function copyTextToClipboard(text: string): Promise<boolean> {
  const value = text.trim();
  if (!value) return false;
  try {
    const saved = await Clipboard.setStringAsync(value, {
      inputFormat: Clipboard.StringFormat.PLAIN_TEXT,
    });
    // Native always resolves `true`; web returns whether the write succeeded.
    return saved === true;
  } catch {
    return false;
  }
}

/**
 * Prefer clipboard copy; if write fails, fall back to the system share sheet
 * (useful on web and when clipboard APIs throw).
 */
export async function copyTextWithShareFallback(
  text: string,
  options?: { shareTitle?: string }
): Promise<CopyTextResult> {
  const value = text.trim();
  if (!value) return { ok: false, method: 'none' };

  if (await copyTextToClipboard(value)) {
    return { ok: true, method: 'clipboard' };
  }

  try {
    await Share.share({
      message: value,
      title: options?.shareTitle,
    });
    return { ok: true, method: 'share' };
  } catch {
    return { ok: false, method: 'none' };
  }
}
