# AgentForge — NanoGPT billing monitor (operator)

**Date:** 2026-07-31  
**Status:** Design approved for planning  
**Scope:** Operator endpoints to monitor and top up the host NanoGPT account used by Agents LLM calls.

## Goal

Give the operator a first-class AgentForge API to:

1. Read current NanoGPT balance and a configurable low-balance warning.
2. Read aggregate API-key usage (spend / tokens / by model).
3. Create and track **BTC Lightning** deposit intents against that same account.

End-user billing is intentionally deferred.

## Decisions (locked)

| Topic | Choice |
|---|---|
| Audience (v1) | Operator only; end-user / Partner Auth later |
| Approach | Thin proxy under Agents (`INanoGptAccountClient` + `/api/agents/billing/...`) |
| Top-up method | `btc-ln` only |
| Persistence | None for deposits — status always from NanoGPT |
| Auth | Existing area auth; NanoGPT uses `Areas:Agents:Llm:ApiKey` |
| Low balance | `usdBalance < Areas:Agents:Billing:LowBalanceUsdThreshold` |
| Tests | Fake account client; no live NanoGPT in CI |

## Out of scope

- End-user balances, Partner Auth, multi-tenant billing
- Deposit methods other than `btc-ln` (card, Nano direct, other tickers)
- Proxying NanoGPT deposit SSE (`/transaction/status/events`)
- Auto-recharge configuration against NanoGPT
- UI / dashboard (API only in this slice)
- Local ledger of deposit history

## Architecture

```
Client ──► /api/agents/billing/*
              │
              ▼
         Billing endpoints (Agents area)
              │
              ▼
         INanoGptAccountClient
              │
              ├── Fake (UseFake / tests)
              └── Http ──► nano-gpt.com/api
                            ├── POST /check-balance
                            ├── GET  /v1/usage
                            ├── GET  /transaction/limits/btc-ln
                            ├── POST /transaction/create/btc-ln
                            └── GET  /transaction/status/btc-ln/{txId}
```

- Lives in `AgentForge.Areas.Agents` next to the LLM client.
- Account HTTP base is the NanoGPT **API root** (`https://nano-gpt.com/api`), not the chat `.../api/v1` path. Derive root from `Llm:BaseUrl` by trimming a trailing `/v1` when present; allow an optional override later only if needed.
- Reuse the same API key and timeout policy as LLM where practical; deposit/balance calls remain short request/response (no streaming).

## Configuration

Under `Areas:Agents`:

```json
"Billing": {
  "LowBalanceUsdThreshold": 5.0
}
```

- `LowBalanceUsdThreshold` default: `5`.
- No separate billing API key in v1.

## API surface

All routes under `/api/agents/billing`, protected by existing area authorization.

### `GET /balance`

Upstream: `POST /check-balance`.

Response shape (conceptual):

```json
{
  "usdBalance": 12.34,
  "nanoBalance": 1.2,
  "nanoDepositAddress": "nano_...",
  "lowBalance": false,
  "lowBalanceThresholdUsd": 5.0
}
```

Parse NanoGPT string balances to decimals. `lowBalance` is computed in AgentForge.

### `GET /usage`

Upstream: `GET /v1/usage` with optional query params `from`, `to`, `group_by` (same semantics as NanoGPT).

Return a typed DTO mirroring NanoGPT aggregates: `from`, `to`, `totals`, and optional `byDay` / `byModel` / `byDayModel` when present. Do not invent AgentForge-side rollups.

### `GET /deposits/limits`

Upstream: `GET /transaction/limits/btc-ln`.

Return minimum/maximum (and fiat equivalents if NanoGPT provides them).

### `POST /deposits`

Body:

```json
{ "amount": 0.00001 }
```

Upstream: `POST /transaction/create/btc-ln` with `{ "amount": ... }`.

Response includes at least: `txId`, `amount`, `status`, `paymentLink`, `address` (if any), `createdAt`, `expiresAt`.

### `GET /deposits/{txId}`

Upstream: `GET /transaction/status/btc-ln/{txId}`.

Return status and any payment fields NanoGPT exposes for that transaction. Status values pass through as strings (`New`, `Pending`, `Processing`, `Paid`, `Completed`, `Expired`, `Failed`, …).

## Error handling

| Upstream | AgentForge |
|---|---|
| Validation / bad amount (400) | 400 with safe message |
| Auth failure (401) | 502 with provider-auth failure message; never echo the API key |
| Rate limit (429) | 429 |
| Timeout / 5xx / transport | 502 |

Document NanoGPT deposit-create rate limit (~10 requests / 10 minutes per key or IP). No local queue or backoff service in v1.

When `Llm:UseFake` is true, register a fake `INanoGptAccountClient` with deterministic fixtures so billing endpoints work offline.

## Components

| Unit | Responsibility |
|---|---|
| `INanoGptAccountClient` | Balance, usage, limits, create deposit, get deposit status |
| `NanoGptAccountClient` | HTTP implementation against NanoGPT |
| `FakeNanoGptAccountClient` | Deterministic test/dev double |
| `BillingEndpoints` | Minimal API maps under `/billing` |
| `AgentsBillingOptions` | `LowBalanceUsdThreshold` on `AgentsOptions` |

Prefer not to overload `ILlmClient` with account APIs — different base path and concern.

## Testing

- Unit: threshold → `lowBalance`; DTO mapping; status-code mapping from fake upstream failures.
- Integration: factory with fake account client; assert the five endpoints return expected shapes and codes.
- No live NanoGPT calls in CI.

## Later phase (not this plan)

End-user billing via NanoGPT Partner Auth (`balance:read`, `deposit:create`, `usage:read`) with per-user linked balances. Operator endpoints remain the host-key path.

## Success criteria

- Operator can read balance and see `lowBalance` without opening nano-gpt.com.
- Operator can inspect usage for the configured API key over a date range.
- Operator can create a BTC-LN invoice and poll until `Completed` (or terminal failure/expiry) via AgentForge.
- CI stays offline with the fake client.
