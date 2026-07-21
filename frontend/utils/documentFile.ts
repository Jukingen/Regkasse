import { EncodingType, File, Paths } from 'expo-file-system';

/**
 * Writes a Base64 payload into the app document directory (persistent, app-private).
 * Uses the Expo SDK 56+ File / Paths API (`Paths.document` ≡ legacy `documentDirectory`).
 *
 * @returns Absolute `file://` URI suitable for `expo-sharing` / `expo-print`.
 */
export function writeBase64ToDocumentFile(fileName: string, base64: string): string {
  const safeName = sanitizeDocumentFileName(fileName);
  if (!safeName) {
    throw new Error('document_file_name_required');
  }
  if (!base64) {
    throw new Error('document_file_content_required');
  }

  const documentRoot = Paths.document;
  if (!documentRoot?.uri) {
    throw new Error('document_directory_unavailable');
  }

  const file = new File(documentRoot, safeName);
  file.create({ overwrite: true });
  file.write(base64, { encoding: EncodingType.Base64 });
  return file.uri;
}

function sanitizeDocumentFileName(fileName: string): string {
  return fileName.replace(/[/\\?%*:|"<>]/g, '_').trim();
}
