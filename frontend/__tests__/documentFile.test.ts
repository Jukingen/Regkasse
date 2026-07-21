import { beforeEach, describe, expect, it, jest } from '@jest/globals';

const mockCreate = jest.fn();
const mockWrite = jest.fn();
const mockUri = 'file:///documents/invoice_1.pdf';

jest.mock('expo-file-system', () => ({
  EncodingType: { Base64: 'base64', UTF8: 'utf8' },
  Paths: {
    document: { uri: 'file:///documents/' },
  },
  File: jest.fn().mockImplementation(() => ({
    create: mockCreate,
    write: mockWrite,
    uri: mockUri,
  })),
}));

// require after jest.mock so factories see mockCreate/mockWrite

const { writeBase64ToDocumentFile } =
  require('../utils/documentFile') as typeof import('../utils/documentFile');

const { File, Paths, EncodingType } =
  require('expo-file-system') as typeof import('expo-file-system');

describe('writeBase64ToDocumentFile', () => {
  beforeEach(() => {
    mockCreate.mockClear();
    mockWrite.mockClear();
    (File as unknown as jest.Mock).mockClear();
  });

  it('writes Base64 into Paths.document and returns the file URI', () => {
    const uri = writeBase64ToDocumentFile('invoice_1.pdf', 'JVBERi0x');

    expect(File).toHaveBeenCalledWith(Paths.document, 'invoice_1.pdf');
    expect(mockCreate).toHaveBeenCalledWith({ overwrite: true });
    expect(mockWrite).toHaveBeenCalledWith('JVBERi0x', { encoding: EncodingType.Base64 });
    expect(uri).toBe(mockUri);
  });

  it('sanitizes unsafe file name characters', () => {
    writeBase64ToDocumentFile('beleg/../x?.pdf', 'abc');

    expect(File).toHaveBeenCalledWith(Paths.document, 'beleg_.._x_.pdf');
  });

  it('rejects empty file names and payloads', () => {
    expect(() => writeBase64ToDocumentFile('   ', 'abc')).toThrow('document_file_name_required');
    expect(() => writeBase64ToDocumentFile('a.pdf', '')).toThrow('document_file_content_required');
  });
});
