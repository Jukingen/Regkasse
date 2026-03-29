/**
 * Başarılı yedek artefaktı için blob indirme; Content-Disposition dosya adını okur (Orval customInstance başlıkları düşürdüğü için doğrudan axios örneği).
 */

import { AXIOS_INSTANCE } from '@/lib/axios';

function parseFilenameFromContentDisposition(header: string | undefined, fallback: string): string {
  if (!header) return fallback;
  const utf8 = /filename\*=UTF-8''([^;]+)/i.exec(header);
  if (utf8?.[1]) {
    try {
      return decodeURIComponent(utf8[1].trim());
    } catch {
      /* yoksay */
    }
  }
  const quoted = /filename="([^"]+)"/i.exec(header);
  if (quoted?.[1]) return quoted[1].trim();
  const plain = /filename=([^;]+)/i.exec(header);
  if (plain?.[1]) return plain[1].trim().replace(/^"|"$/g, '');
  return fallback;
}

export async function downloadBackupArtifactFile(
  runId: string,
  artifactId: string,
  fallbackFilename: string,
): Promise<void> {
  const res = await AXIOS_INSTANCE.get(`/api/admin/backup/runs/${runId}/artifacts/${artifactId}/download`, {
    responseType: 'blob',
  });
  const blob = res.data as Blob;
  const name = parseFilenameFromContentDisposition(
    res.headers['content-disposition'] as string | undefined,
    fallbackFilename,
  );
  const url = URL.createObjectURL(blob);
  try {
    const a = document.createElement('a');
    a.href = url;
    a.download = name;
    a.rel = 'noopener';
    document.body.appendChild(a);
    a.click();
    a.remove();
  } finally {
    URL.revokeObjectURL(url);
  }
}
