import { describe, expect, it } from 'vitest';

import {
  createJsonExportBlob,
  estimateJsonByteSize,
} from '@/lib/download/exportDownload';

describe('exportDownload helpers', () => {
  it('createJsonExportBlob_produces_json_mime_and_matching_size', () => {
    const data = { hello: 'world' };
    const blob = createJsonExportBlob(data);
    expect(blob.type).toBe('application/json');
    expect(blob.size).toBe(estimateJsonByteSize(data));
  });
});
