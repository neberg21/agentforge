# Nostr relay (AAE)

## Integration Guide

### What it does

Self-hosted Nostr relay (`nostr-rs-relay`) used as AAE’s interface ingress for DMs and related events. Live host: **https://nostr.neberg.de** · WebSocket **wss://nostr.neberg.de**.

### Prerequisites

- Clients or n8n nodes that speak Nostr over WSS
- For local runs: Docker volume for SQLite

### Setup

**Deployed (preferred for workflows):**

- HTTPS: `https://nostr.neberg.de`
- WSS: `wss://nostr.neberg.de`

**Local image:**

```cmd
cd infrastructure\nostr
docker build -t aae-nostr .
docker run -p 8080:8080 -v %CD%\data:/usr/src/app/db aae-nostr
```

Optional: mount `config.toml` read-only at `/usr/src/app/config.toml`.

In n8n Nostr nodes, set relay to `wss://nostr.neberg.de`.

### Configuration

| Item | Value |
|------|--------|
| Public HTTPS | `https://nostr.neberg.de` |
| Public WSS | `wss://nostr.neberg.de` |
| Container port | `8080` |
| Image | `scsibug/nostr-rs-relay:0.10.0` |

Full host table: [`docs/deployed-services.md`](../../docs/deployed-services.md).

### Common mistakes

- Pointing AAE workflows only at third-party relays
- Forgetting WSS (`wss://`) when configuring Nostr nodes

## Implementation Details

### Runtime flow

Clients publish to the relay; n8n reads via poll or event trigger and may write replies back through the same WSS endpoint. See [`docs/deployed-services.md`](../../docs/deployed-services.md) §4.

### Key types

| Artifact | Responsibility |
|----------|----------------|
| [`Dockerfile`](Dockerfile) | Relay image, expose 8080, volume/config comments |

### Extension points

- Optional `config.toml` mount for relay policy
- Persist DB outside the container (`./data` → `/usr/src/app/db`)

### Limitations

- Relay alone does not encrypt or route business logic — n8n/Flowise own that
- Test account notes (do not commit secrets to public docs): [`docs/nostr-test-account.md`](../../docs/nostr-test-account.md)

### References

- [`docs/deployed-services.md`](../../docs/deployed-services.md)
- [`../n8n/README.md`](../n8n/README.md)
