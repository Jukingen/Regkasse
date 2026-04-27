# Beta Environment Matrix

Short checklist for beta environments. Set secrets through the target host or CI secret store; do not commit real values.

## Backend

Required environment/config:

- `ConnectionStrings__DefaultConnection`: PostgreSQL connection string.
- `JwtSettings__SecretKey`: JWT signing secret.
- `JwtSettings__Issuer`: Expected JWT issuer.
- `JwtSettings__Audience`: Expected JWT audience.
- `Cors__AllowedOrigins`: Required for non-development environments; include only the deployed admin/POS origins that may call the API.
- `.NET 10 SDK/runtime`: Required by the backend host/build environment.

## Admin

Required build-time environment:

- `NEXT_PUBLIC_API_BASE_URL`: Backend API base URL reachable by the browser.
- `NEXT_PUBLIC_RKSV_ENVIRONMENT=TEST` or `NEXT_PUBLIC_RKSV_ENVIRONMENT=PROD`: Selects the RKSV environment displayed/used by the admin frontend.

`NEXT_PUBLIC_*` values are embedded into the Next.js client bundle at build time. Set them before `next build`; setting them only at container/runtime start is not enough for already-built static client code.

## POS

Required Expo public environment:

- `EXPO_PUBLIC_API_BASE_URL`: Backend API base URL reachable from the device/emulator.

Do not rely on a hardcoded LAN fallback. For native beta/device testing, set `EXPO_PUBLIC_API_BASE_URL` explicitly, for example `http://YOUR_DEV_MACHINE_IP:5183/api` for a local beta backend.

## Minimal Local Beta Startup Order

1. Start PostgreSQL and verify the beta database is reachable.
2. Start the backend with the required connection, JWT, CORS, and .NET 10 configuration.
3. Build/start admin with `NEXT_PUBLIC_API_BASE_URL` and `NEXT_PUBLIC_RKSV_ENVIRONMENT` already set.
4. Start POS with `EXPO_PUBLIC_API_BASE_URL` set to a backend URL reachable from the test device.
5. Perform a smoke check: login, load products, and confirm admin/POS can reach the same backend.

## Preflight Check

Run the read-only beta preflight before manual smoke testing:

```bash
BACKEND_BASE_URL=http://localhost:5183 \
NEXT_PUBLIC_API_BASE_URL=http://localhost:5183/api \
NEXT_PUBLIC_RKSV_ENVIRONMENT=TEST \
EXPO_PUBLIC_API_BASE_URL=http://localhost:5183/api \
node scripts/beta-preflight.mjs
```

`BACKEND_BASE_URL` is used only by the preflight script for `/health` checks. The admin and POS values should match the URLs used by their actual beta builds.
