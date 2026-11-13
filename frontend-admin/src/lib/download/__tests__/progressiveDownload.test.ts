import { describe, expect, it, vi, beforeEach } from 'vitest';

import {
  ProgressiveDownloadCancelledError,
  ProgressiveDownloadSession,
  fetchBlobProgressive,
} from '@/lib/download/progressiveDownload';

const { mockGet, mockHead } = vi.hoisted(() => ({
  mockGet: vi.fn(),
  mockHead: vi.fn(),
}));

vi.mock('@/lib/axios', () => ({
  AXIOS_INSTANCE: {
    get: mockGet,
    head: mockHead,
  },
}));

describe('fetchBlobProgressive', () => {
  beforeEach(() => {
    mockGet.mockReset();
    mockHead.mockReset();
  });

  it('downloads with Range chunks when HEAD reports Accept-Ranges', async () => {
    mockHead.mockResolvedValue({
      status: 200,
      headers: {
        'accept-ranges': 'bytes',
        'content-length': '10',
      },
    });
    mockGet
      .mockResolvedValueOnce({
        status: 206,
        data: new Uint8Array([1, 2, 3, 4, 5]).buffer,
        headers: {
          'content-range': 'bytes 0-4/10',
          'content-disposition': 'attachment; filename="a.bin"',
        },
      })
      .mockResolvedValueOnce({
        status: 206,
        data: new Uint8Array([6, 7, 8, 9, 10]).buffer,
        headers: {
          'content-range': 'bytes 5-9/10',
          'content-disposition': 'attachment; filename="a.bin"',
        },
      });

    const session = new ProgressiveDownloadSession();
    const phases: string[] = [];
    const result = await fetchBlobProgressive({
      url: '/file.bin',
      fileName: 'fallback.bin',
      chunkSizeBytes: 5,
      session,
      onProgress: (s) => phases.push(s.phase),
    });

    expect(result.blob.size).toBe(10);
    expect(result.fileName).toBe('a.bin');
    expect(phases).toContain('downloading');
    expect(phases.at(-1)).toBe('done');
    expect(mockGet).toHaveBeenCalledTimes(2);
  });

  it('falls back to single GET when Range is unavailable', async () => {
    mockHead.mockResolvedValue({
      status: 200,
      headers: {
        'accept-ranges': 'none',
        'content-length': '3',
      },
    });
    mockGet.mockResolvedValue({
      status: 200,
      data: new Uint8Array([9, 8, 7]).buffer,
      headers: {
        'content-type': 'application/octet-stream',
        'content-disposition': 'attachment; filename="b.bin"',
      },
    });

    const session = new ProgressiveDownloadSession();
    const result = await fetchBlobProgressive({
      url: '/file.bin',
      fileName: 'fallback.bin',
      session,
      onProgress: () => undefined,
    });

    expect(result.blob.size).toBe(3);
    expect(result.fileName).toBe('b.bin');
    expect(mockGet).toHaveBeenCalledTimes(1);
  });

  it('cancels via session', async () => {
    mockHead.mockImplementation(async () => {
      await new Promise((r) => setTimeout(r, 50));
      return { status: 200, headers: { 'accept-ranges': 'bytes', 'content-length': '100' } };
    });
    const session = new ProgressiveDownloadSession();
    const promise = fetchBlobProgressive({
      url: '/file.bin',
      fileName: 'x.bin',
      session,
      onProgress: () => undefined,
    });
    session.cancel();
    await expect(promise).rejects.toBeInstanceOf(ProgressiveDownloadCancelledError);
  });
});
