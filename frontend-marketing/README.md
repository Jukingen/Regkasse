# Regkasse Marketing (`frontend-marketing`)

Public landing page for `regkasse.at`: features, the 11 industries, placeholder pricing, and a demo request form. German (de-DE) only. Not part of POS, FA, or fiscal signing.

Run it from this folder with `npm run dev` (port **3002**) after `npm install` at the repo root. From the root, `npm run dev:marketing` does the same, and `npm run dev:all` includes it. `npm run build` then `npm run start` serves the production build on port 3002.

Set `NEXT_PUBLIC_API_BASE_URL` before the build (see `.env.example`). The demo form posts to `{API}/api/public/demo-requests`. Production should point that variable at `https://api.regkasse.at` and host this app on `https://regkasse.at`.
