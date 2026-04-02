/**
 * Blob hata gövdelerinde API code → BackupArtifactDownloadFailureCode eşlemesi.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import axios from 'axios';

const { mockGet } = vi.hoisted(() => ({ mockGet: vi.fn() }));

vi.mock('@/lib/axios', () => ({
  AXIOS_INSTANCE: {
    get: mockGet,
  },
}));

import {
  downloadBackupArtifactFile,
  BackupArtifactDownloadError,
} from '@/features/backup-dr/logic/downloadBackupArtifactFile';

function axiosErrorWithBlob(status: number, jsonBody: object): Error {
  const blob = new Blob([JSON.stringify(jsonBody)], { type: 'application/json' });
  const err = new Error('Request failed') as import('axios').AxiosError;
  (err as unknown as { isAxiosError: boolean }).isAxiosError = true;
  err.response = {
    status,
    statusText: '',
    data: blob,
    headers: {},
    config: {} as import('axios').InternalAxiosRequestConfig,
  };
  return err;
}

describe('downloadBackupArtifactFile', () => {
  beforeEach(() => {
    mockGet.mockReset();
    vi.spyOn(axios, 'isAxiosError').mockImplementation(
      (p: unknown) => p !== null && typeof p === 'object' && (p as { isAxiosError?: boolean }).isAxiosError === true,
    );
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('maps 404 + BACKUP_ARTIFACT_FILE_MISSING to file_missing', async () => {
    mockGet.mockRejectedValue(
      axiosErrorWithBlob(404, { code: 'BACKUP_ARTIFACT_FILE_MISSING', message: 'x' }),
    );
    try {
      await downloadBackupArtifactFile('r1', 'a1', 'f.bin');
      expect.fail('expected throw');
    } catch (e) {
      expect(e).toBeInstanceOf(BackupArtifactDownloadError);
      expect((e as BackupArtifactDownloadError).code).toBe('file_missing');
    }
  });

  it('maps 403 + BACKUP_ARTIFACT_NOT_DOWNLOADABLE_SIMULATED to simulated_not_downloadable', async () => {
    mockGet.mockRejectedValue(
      axiosErrorWithBlob(403, {
        code: 'BACKUP_ARTIFACT_NOT_DOWNLOADABLE_SIMULATED',
        message: 'Stub',
      }),
    );
    try {
      await downloadBackupArtifactFile('r1', 'a1', 'f.bin');
      expect.fail('expected throw');
    } catch (e) {
      expect(e).toBeInstanceOf(BackupArtifactDownloadError);
      expect((e as BackupArtifactDownloadError).code).toBe('simulated_not_downloadable');
    }
  });

  it('maps 404 + BACKUP_RUN_NOT_FOUND to run_not_found', async () => {
    mockGet.mockRejectedValue(axiosErrorWithBlob(404, { code: 'BACKUP_RUN_NOT_FOUND' }));
    try {
      await downloadBackupArtifactFile('r1', 'a1', 'f.bin');
      expect.fail('expected throw');
    } catch (e) {
      expect(e).toBeInstanceOf(BackupArtifactDownloadError);
      expect((e as BackupArtifactDownloadError).code).toBe('run_not_found');
    }
  });

  it('HTTP 200 with empty octet-stream body yields empty_payload', async () => {
    mockGet.mockResolvedValue({
      data: new Blob([], { type: 'application/octet-stream' }),
      status: 200,
      statusText: 'OK',
      headers: { 'content-type': 'application/octet-stream' },
      config: {} as import('axios').InternalAxiosRequestConfig,
    });
    await expect(downloadBackupArtifactFile('r1', 'a1', 'f.bin')).rejects.toMatchObject({
      code: 'empty_payload',
    });
  });

  it('HTTP 200 with application/json error envelope maps code', async () => {
    mockGet.mockResolvedValue({
      data: new Blob([JSON.stringify({ code: 'BACKUP_ARTIFACT_FILE_MISSING', message: 'x' })], {
        type: 'application/json',
      }),
      status: 200,
      statusText: 'OK',
      headers: { 'content-type': 'application/json' },
      config: {} as import('axios').InternalAxiosRequestConfig,
    });
    await expect(downloadBackupArtifactFile('r1', 'a1', 'f.bin')).rejects.toMatchObject({
      code: 'file_missing',
    });
  });
});
