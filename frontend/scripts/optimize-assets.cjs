/**
 * One-off / occasional asset optimizer (requires: npm i -D --legacy-peer-deps sharp).
 * Keeps native PNG icons; regenerates in-app logo.webp from icon.png.
 *
 * Usage: node scripts/optimize-assets.cjs
 */
const sharp = require('sharp');
const fs = require('fs');
const path = require('path');

const imagesDir = path.join('assets', 'images');

(async () => {
  const keep = ['icon.png', 'adaptive-icon.png', 'favicon.png'];
  for (const f of keep) {
    const p = path.join(imagesDir, f);
    if (!fs.existsSync(p)) {
      console.warn('missing', f);
      continue;
    }
    const before = fs.statSync(p).size;
    const tmp = `${p}.opt.png`;
    await sharp(p).png({ compressionLevel: 9, palette: true, quality: 90, effort: 10 }).toFile(tmp);
    let after = fs.statSync(tmp).size;
    if (after >= before) {
      await sharp(p).png({ compressionLevel: 9, effort: 10 }).toFile(tmp);
      after = fs.statSync(tmp).size;
    }
    if (after < before) {
      fs.renameSync(tmp, p);
      console.log(`OPT ${f}: ${before} -> ${after}`);
    } else {
      fs.unlinkSync(tmp);
      console.log(`SKIP ${f}: no gain`);
    }
  }

  const icon = path.join(imagesDir, 'icon.png');
  const webp = path.join(imagesDir, 'logo.webp');
  await sharp(icon).webp({ quality: 82, effort: 6 }).toFile(webp);
  console.log(`logo.webp: ${fs.statSync(webp).size}b`);
})().catch((e) => {
  console.error(e);
  process.exit(1);
});
