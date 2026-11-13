# POS critical-path load test

k6 scenario for the cashier sale path. Each virtual user is one cashier and runs the path once:

1. `POST /api/Auth/login` with `clientApp=pos`
2. `POST /api/pos/shift/start` when that cashier has no active shift
3. `POST /api/pos/cart/add-item` five times (five distinct products, quantity 1)
4. `POST /api/pos/payment` (cash, walk-in customer, server cart total)
5. `POST /api/pos/shift/end` (closes that cash register)

Default load is **50 concurrent cashiers**, one iteration each. The summary prints HTTP error rate, per-step p95, and p95 of a completed sale (login through close).

This is a lab tool. It is not part of CI. Each successful iteration writes a fiscal receipt and closes a cash register. Run it against a local Development API.

Fiscal Tagesabschluss (`POST /api/pos/shift/daily-closing`) is not in this scenario. That call is once per register per business day, so 49 of 50 cashiers would fail by design and the error rate would be meaningless.

## Install k6

Install the k6 binary from [grafana.com/docs/k6](https://grafana.com/docs/k6/latest/set-up/install-k6/). The repo does not ship k6 as an npm dependency.

## Prepare the lab

You need all of the following on the target tenant:

- The API running. Local default is `http://localhost:5184`.
- A Development tenant. The script sends `X-Tenant-Id: dev` on localhost. Production ignores that header and uses the JWT tenant.
- **50 cashier users** and **50 cash registers**. One register can be open for one cashier. Reusing a register across virtual users makes shift start return 409, and that counts as an error.
- At least five active catalog products that are not variants, add-ons, or IMEI-tracked, each with a price of at least 0.01. Or pass `PRODUCT_IDS`.
- Cashier accounts that can sign in with password only. A second factor stops the login step.
- A TSE the Development API can reach. A payment that does not persist a receipt is an error (HTTP 201 is required; HTTP 202 offline queue is not success).

Copy the example file and fill real lab credentials. Keep that copy out of git:

```text
scripts/load-test/cashiers.local.json
```

`*.local.json` in this folder is gitignored. `scripts/local/` is gitignored too.

```json
[
  {
    "loginIdentifier": "cashier-01",
    "password": "CHANGE_ME",
    "cashRegisterId": "register-guid"
  }
]
```

One object per cashier. `cashRegisterId` values must be unique.

## Run

From the repo root:

```bash
k6 run -e CASHIERS_FILE=cashiers.local.json scripts/load-test/pos-critical-path.js
```

`CASHIERS_FILE` is relative to `pos-critical-path.js`, not the shell directory. A file in `scripts/local/` is `-e CASHIERS_FILE=../local/cashiers.json`.

Smoke one cashier before the full run:

```bash
k6 run -e VUS=1 -e CASHIERS_FILE=cashiers.local.json scripts/load-test/pos-critical-path.js
```

The script refuses any host other than `localhost`, `127.0.0.1`, or `::1` unless you set `LOAD_TEST_ALLOW_REMOTE=1`. Use that only for a lab you own. Do not point it at `api.regkasse.at`.

## What the summary means

| Line | Meaning |
|------|---------|
| `error_rate` | Share of HTTP calls outside the expected status (login 200, add-item 200, pay 201, close 200) |
| `check_pass_rate` | Share of status checks that passed |
| `http_p95_ms` | p95 of every HTTP call in the run |
| `sale_p95_ms` | p95 of completed cashier iterations, login through close. Failed iterations are excluded so this number is successful-path latency |
| `http_req_duration{step:...}` | p95 for that step: `login`, `catalog`, `add_product`, `pay`, `close_register` |

k6 exits non-zero when a threshold fails. Defaults:

| Variable | Default | Threshold |
|----------|---------|-----------|
| `P95_MS` | `3000` | `pos_sale_ms` p95 under this many milliseconds |
| `MAX_ERROR_RATE` | `0.01` | `http_req_failed` under 1 percent, and checks above 99 percent |

These are lab budgets, not a service-level agreement. Raise `P95_MS` on a slow Development machine.

## Other variables

| Variable | Default | Purpose |
|----------|---------|---------|
| `BASE_URL` | `http://localhost:5184` | API origin, no trailing path |
| `VUS` | `50` | Concurrent cashiers. The credential file must have at least this many rows |
| `PRODUCT_COUNT` | `5` | Distinct products added per sale |
| `PRODUCT_IDS` | empty | Comma-separated product GUIDs. Skips the catalog read |
| `CUSTOMER_ID` | walk-in guest `00000000-0000-0000-0000-000000000001` | `customerId` on the payment |
| `TENANT_SLUG` | `dev` on localhost, empty otherwise | `X-Tenant-Id` when set |
| `TABLE_BASE` | `9000` | Virtual user N uses table `TABLE_BASE + N` |
| `MAX_DURATION` | `10m` | Scenario cap |

Closing balance sent to shift end is the cart total of this sale, with a zero opening balance. If that shift already had other cash sales, the API can still return HTTP 200 with a discrepancy status. That response counts as a successful close.
