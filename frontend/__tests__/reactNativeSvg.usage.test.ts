import { describe, expect, it } from '@jest/globals';
import fs from 'fs';
import path from 'path';

import { resolveRksvQrEcl } from '../utils/rksvQrEncode';

const root = path.join(__dirname, '..');

function read(rel: string): string {
  return fs.readFileSync(path.join(root, rel), 'utf8');
}

describe('react-native-svg / RKSV QR usage', () => {
  it('pins Expo SDK 56 compatible react-native-svg', () => {
    const pkg = JSON.parse(read('package.json')) as {
      dependencies: Record<string, string>;
    };
    expect(pkg.dependencies['react-native-svg']).toBe('15.15.4');
    expect(pkg.dependencies['react-native-qrcode-svg']).toBeDefined();
  });

  it('has no static .svg assets requiring @svgr (QR uses react-native-qrcode-svg)', () => {
    const walk = (dir: string): string[] => {
      if (!fs.existsSync(dir)) return [];
      const out: string[] = [];
      for (const name of fs.readdirSync(dir)) {
        if (name === 'node_modules' || name === 'backup' || name === '.expo') continue;
        const full = path.join(dir, name);
        const st = fs.statSync(full);
        if (st.isDirectory()) out.push(...walk(full));
        else if (name.toLowerCase().endsWith('.svg')) out.push(full);
      }
      return out;
    };
    expect(walk(root)).toEqual([]);
  });

  it('centralizes QR SVG rendering through RksvQrCodeSvg', () => {
    const shared = read('components/RksvQrCodeSvg.tsx');
    expect(shared).toContain("from 'react-native-qrcode-svg'");
    expect(shared).toContain('memo(');

    const payment = read('components/PaymentSuccessQr.tsx');
    expect(payment).toContain('RksvQrCodeSvg');
    expect(payment).not.toContain("from 'react-native-qrcode-svg'");

    const receipt = read('components/ReceiptTemplate.tsx');
    expect(receipt).toContain('RksvQrCodeSvg');
    expect(receipt).not.toContain("from 'react-native-qrcode-svg'");
  });

  it('resolves ECL for a typical RKSV payload used by the SVG QR path', () => {
    const payload = '_R1-AT1_demo_payload_for_svg_render';
    expect(resolveRksvQrEcl(payload)).toBe('M');
  });
});
