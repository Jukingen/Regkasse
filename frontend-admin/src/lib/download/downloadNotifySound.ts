/**
 * Optional short tones for export download notifications (Web Audio API — no asset files).
 */

export type DownloadNotifySoundKind = 'success' | 'error' | 'start';

let sharedCtx: AudioContext | null = null;

function getAudioContext(): AudioContext | null {
  if (typeof window === 'undefined') return null;
  const AC =
    window.AudioContext ||
    (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
  if (!AC) return null;
  if (!sharedCtx) sharedCtx = new AC();
  return sharedCtx;
}

function tone(
  ctx: AudioContext,
  frequency: number,
  startAt: number,
  durationSec: number,
  gainPeak = 0.04
): void {
  const osc = ctx.createOscillator();
  const gain = ctx.createGain();
  osc.type = 'sine';
  osc.frequency.value = frequency;
  gain.gain.setValueAtTime(0.0001, startAt);
  gain.gain.exponentialRampToValueAtTime(gainPeak, startAt + 0.02);
  gain.gain.exponentialRampToValueAtTime(0.0001, startAt + durationSec);
  osc.connect(gain);
  gain.connect(ctx.destination);
  osc.start(startAt);
  osc.stop(startAt + durationSec + 0.02);
}

/** Play a soft chime when preference allows; never throws. */
export function playDownloadNotifySound(kind: DownloadNotifySoundKind): void {
  try {
    const ctx = getAudioContext();
    if (!ctx) return;
    void ctx.resume();
    const t0 = ctx.currentTime + 0.01;
    if (kind === 'start') {
      tone(ctx, 520, t0, 0.08, 0.03);
      return;
    }
    if (kind === 'success') {
      tone(ctx, 523.25, t0, 0.1);
      tone(ctx, 659.25, t0 + 0.09, 0.12);
      return;
    }
    tone(ctx, 220, t0, 0.14, 0.05);
    tone(ctx, 180, t0 + 0.12, 0.16, 0.04);
  } catch {
    /* autoplay / AudioContext blocked */
  }
}
