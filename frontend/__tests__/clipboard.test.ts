import { beforeEach, describe, expect, it, jest } from '@jest/globals';

import { copyTextToClipboard, copyTextWithShareFallback } from '../utils/clipboard';

const mockSetStringAsync = jest.fn<(text: string, options?: unknown) => Promise<boolean>>();
const mockShare =
  jest.fn<(content: { message: string; title?: string }) => Promise<{ action: string }>>();

jest.mock('expo-clipboard', () => ({
  setStringAsync: (...args: unknown[]) => mockSetStringAsync(...(args as [string, unknown?])),
  StringFormat: { PLAIN_TEXT: 'plainText', HTML: 'html' },
}));

jest.mock('react-native', () => ({
  Share: {
    share: (...args: unknown[]) => mockShare(...(args as [{ message: string; title?: string }])),
  },
  Platform: {
    OS: 'ios',
    select: (spec: Record<string, unknown>) => spec.ios ?? spec.default,
  },
}));

describe('clipboard utils (expo-clipboard)', () => {
  beforeEach(() => {
    mockSetStringAsync.mockReset();
    mockShare.mockReset();
  });

  /**
   * Writing via setStringAsync does not prompt for paste permission on native.
   * Same code path for iOS and Android (expo-clipboard always resolves true on write).
   */
  describe.each(['iOS', 'Android'] as const)('%s write path', (_platform) => {
    it('copies plain text without requiring a permission prompt', async () => {
      mockSetStringAsync.mockResolvedValue(true);

      await expect(copyTextToClipboard('  queue-id-123  ')).resolves.toBe(true);

      expect(mockSetStringAsync).toHaveBeenCalledWith('queue-id-123', {
        inputFormat: 'plainText',
      });
      expect(mockSetStringAsync).toHaveBeenCalledTimes(1);
    });

    it('returns false for empty / whitespace-only input without calling native API', async () => {
      await expect(copyTextToClipboard('   ')).resolves.toBe(false);
      expect(mockSetStringAsync).not.toHaveBeenCalled();
    });

    it('succeeds via clipboard when setStringAsync resolves true', async () => {
      mockSetStringAsync.mockResolvedValue(true);

      await expect(copyTextWithShareFallback('id-2')).resolves.toEqual({
        ok: true,
        method: 'clipboard',
      });
      expect(mockShare).not.toHaveBeenCalled();
    });

    it('falls back to Share when clipboard write throws', async () => {
      mockSetStringAsync.mockRejectedValue(new Error('clipboard unavailable'));
      mockShare.mockResolvedValue({ action: 'sharedAction' });

      await expect(
        copyTextWithShareFallback('handoff-payload', { shareTitle: 'Support' })
      ).resolves.toEqual({ ok: true, method: 'share' });

      expect(mockShare).toHaveBeenCalledWith({
        message: 'handoff-payload',
        title: 'Support',
      });
    });

    it('reports none when clipboard and Share both fail', async () => {
      mockSetStringAsync.mockRejectedValue(new Error('clipboard unavailable'));
      mockShare.mockRejectedValue(new Error('share cancelled'));

      await expect(copyTextWithShareFallback('id-1')).resolves.toEqual({
        ok: false,
        method: 'none',
      });
    });
  });

  it('treats setStringAsync(false) as a write denial (web Clipboard API)', async () => {
    mockSetStringAsync.mockResolvedValue(false);
    mockShare.mockResolvedValue({ action: 'sharedAction' });

    await expect(copyTextWithShareFallback('web-id')).resolves.toEqual({
      ok: true,
      method: 'share',
    });
  });
});
