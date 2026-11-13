/**
 * Web implementation: expo-file-system document dirs are unavailable in browsers.
 * Triggers a browser download and returns a blob: URL (caller may revoke later).
 */
export function writeBase64ToDocumentFile(fileName: string, base64: string): string {
  const safeName = sanitizeDocumentFileName(fileName);
  if (!safeName) {
    throw new Error('document_file_name_required');
  }
  if (!base64) {
    throw new Error('document_file_content_required');
  }
  if (typeof document === 'undefined' || typeof window === 'undefined') {
    throw new Error('document_directory_unavailable');
  }

  const binary = atob(base64);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i += 1) {
    bytes[i] = binary.charCodeAt(i);
  }

  const mime = mimeFromFileName(safeName);
  const blob = new Blob([bytes], { type: mime });
  const url = URL.createObjectURL(blob);

  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = safeName;
  anchor.rel = 'noopener';
  document.body.appendChild(anchor);
  anchor.click();
  document.body.removeChild(anchor);

  // Keep blob alive briefly for share/print consumers that open the URI.
  setTimeout(() => {
    URL.revokeObjectURL(url);
  }, 120_000);
  return url;
}

function sanitizeDocumentFileName(fileName: string): string {
  return fileName.replace(/[/\\?%*:|"<>]/g, '_').trim();
}

function mimeFromFileName(fileName: string): string {
  const lower = fileName.toLowerCase();
  if (lower.endsWith('.pdf')) return 'application/pdf';
  if (lower.endsWith('.png')) return 'image/png';
  if (lower.endsWith('.jpg') || lower.endsWith('.jpeg')) return 'image/jpeg';
  return 'application/octet-stream';
}
