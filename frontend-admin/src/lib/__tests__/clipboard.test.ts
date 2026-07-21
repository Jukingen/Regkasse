import { afterEach, describe, expect, it, vi } from 'vitest';

import { copyTextToClipboard } from '@/lib/clipboard';

describe('copyTextToClipboard', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('uses navigator clipboard when available', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: { writeText },
    });

    await expect(copyTextToClipboard('Temp#Pass123')).resolves.toBe(true);
    expect(writeText).toHaveBeenCalledWith('Temp#Pass123');
  });

  it('falls back to execCommand when clipboard api is unavailable', async () => {
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: undefined,
    });

    const execCommand = vi.fn().mockReturnValue(true);
    Object.defineProperty(document, 'execCommand', {
      configurable: true,
      value: execCommand,
    });

    await expect(copyTextToClipboard('Fallback#Pass123')).resolves.toBe(true);
    expect(execCommand).toHaveBeenCalledWith('copy');
  });
});
