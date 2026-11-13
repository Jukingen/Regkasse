import { Platform, Vibration } from 'react-native';

type WebAudioContextCtor = new () => {
  createOscillator: () => {
    type: string;
    frequency: { value: number };
    connect: (node: unknown) => void;
    start: () => void;
    stop: (when?: number) => void;
    onended: (() => void) | null;
  };
  createGain: () => {
    gain: { value: number };
    connect: (node: unknown) => void;
  };
  destination: unknown;
  currentTime: number;
  close: () => Promise<void>;
};

function webAudioContextCtor(): WebAudioContextCtor | null {
  const globalObj = globalThis as {
    AudioContext?: WebAudioContextCtor;
    webkitAudioContext?: WebAudioContextCtor;
  };
  return globalObj.AudioContext ?? globalObj.webkitAudioContext ?? null;
}

/** Plays a short beep (web) and vibrates when the device allows it. */
export function playKitchenNewOrderSound(): void {
  try {
    Vibration.vibrate(80);
  } catch {
    // Vibration is optional.
  }

  if (Platform.OS !== 'web') {
    return;
  }

  try {
    const Ctor = webAudioContextCtor();
    if (!Ctor) return;
    const ctx = new Ctor();
    const oscillator = ctx.createOscillator();
    const gain = ctx.createGain();
    oscillator.type = 'sine';
    oscillator.frequency.value = 880;
    gain.gain.value = 0.08;
    oscillator.connect(gain);
    gain.connect(ctx.destination);
    oscillator.start();
    oscillator.stop(ctx.currentTime + 0.18);
    oscillator.onended = () => {
      void ctx.close();
    };
  } catch {
    // Autoplay or hardware may block audio.
  }
}
