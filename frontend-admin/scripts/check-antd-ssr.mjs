/**
 * Smoke-check Ant Design styles + basic paint timing after navigation.
 * Usage: `node scripts/check-antd-ssr.mjs [baseUrl]`
 */
import { chromium } from '@playwright/test';

const baseUrl = process.argv[2] ?? 'http://127.0.0.1:3015';

const browser = await chromium.launch();
const page = await browser.newPage();
const response = await page.goto(`${baseUrl}/login`, { waitUntil: 'networkidle' });
await page.waitForSelector('.ant-btn, input, form', { timeout: 20_000 });

const html = await page.content();
const bodyText = await page.locator('body').innerText();
const styleCount = await page.locator('style').count();
const computedBtn = await page
  .locator('.ant-btn')
  .first()
  .evaluate((el) => {
    const s = getComputedStyle(el);
    return {
      display: s.display,
      backgroundColor: s.backgroundColor,
      fontFamily: s.fontFamily,
    };
  });

const paint = await page.evaluate(() => {
  const nav = performance.getEntriesByType('navigation')[0];
  const paints = Object.fromEntries(
    performance.getEntriesByType('paint').map((e) => [e.name, Math.round(e.startTime)])
  );
  return {
    domContentLoaded: nav ? Math.round(nav.domContentLoadedEventEnd) : null,
    loadEvent: nav ? Math.round(nav.loadEventEnd) : null,
    ...paints,
  };
});

const raw = await page.request.get(`${baseUrl}/login`);
const rawHtml = await raw.text();

const report = {
  status: response?.status(),
  hydrated: {
    hasAntBtnMarkup: html.includes('ant-btn'),
    hasLoginCopy: /Anmelden|Passwort|login/i.test(bodyText),
    styleTagCount: styleCount,
    antButtonComputed: computedBtn,
    stylesApplied:
      computedBtn.display !== 'inline' &&
      computedBtn.backgroundColor !== 'rgba(0, 0, 0, 0)' &&
      computedBtn.backgroundColor !== 'transparent',
  },
  paintTimingMs: paint,
  ssrDocument: {
    length: rawHtml.length,
    hasAntdCssinjs: rawHtml.includes('antd-cssinjs'),
    hasAntBtn: rawHtml.includes('ant-btn'),
    note: 'Static App Router shells may stream client trees via RSC; AntdRegistry wraps ConfigProvider for css-in-js extraction when SSR runs.',
  },
};

console.log(JSON.stringify(report, null, 2));

if (!report.hydrated.hasAntBtnMarkup || !report.hydrated.stylesApplied) {
  console.error('Ant Design styles do not appear applied after hydration.');
  process.exit(1);
}

await browser.close();
