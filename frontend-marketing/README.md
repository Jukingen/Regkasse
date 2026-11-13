# Regkasse Marketing (`frontend-marketing`)

Public landing page for `regkasse.at`: features, the 11 industries, published starting prices, and a demo request form. The default language is German. English and Turkish are available from the header and are stored in `localStorage` under `marketing_lang`. Not part of POS, FA, or fiscal signing.

Run it from this folder with `npm run dev` (port **3002**) after `npm install` at the repo root. From the root, `npm run dev:marketing` does the same, and `npm run dev:all` includes it. `npm run build` then `npm run start` serves the production build on port 3002.

Set `NEXT_PUBLIC_API_BASE_URL` before the build (see `.env.example`). The demo form posts to `{API}/api/public/demo-requests`. Production should point that variable at `https://api.regkasse.at` and host this app on `https://regkasse.at`.

## Deployment and SEO

Set `NEXT_PUBLIC_SITE_URL` at build time (default `https://regkasse.at`). It is the origin for the canonical URL, Open Graph image, `sitemap.xml`, and `robots.txt`. The sitemap lists `/`, `/impressum`, `/datenschutz`, and `/agb`. Crawlers are allowed. The social image is `public/og-image.png` (1200×630), redrawn with `node scripts/write-og-image.mjs`.

Prices live in `src/i18n/pricing.ts`: Starter €29 per month, Professional €79 per month, Enterprise custom. The yearly toggle shows 10 months (2 months free) and does not take payment. Copy for de, en, and tr is in `src/i18n/*.json`. Legal pages stay German.
