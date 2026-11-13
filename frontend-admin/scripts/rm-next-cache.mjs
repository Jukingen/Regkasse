/**
 * Türkçe: `.next` klasörünü siler — gömülü NEXT_PUBLIC_* değerleri için dev öncesi temiz başlangıç.
 */
import { rmSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const adminRoot = join(dirname(fileURLToPath(import.meta.url)), '..');
try {
  rmSync(join(adminRoot, '.next'), { recursive: true, force: true });
  console.info('[frontend-admin] Removed .next (clean Next cache).');
} catch {
  /* klasör yoksa yok say */
}
