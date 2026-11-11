# Changelog: 2026-11-11 · ShipmentService (fourth microservice)

> Design: [ShipmentService/ShipmentService.md](../ShipmentService/ShipmentService.md) (written first, then built).
> Previous: [fleet-service-111126.md](fleet-service-111126.md)

## Decisions (asked and answered before the design)

| # | Decision |
|---|---|
| S1 | A CLIENT creates shipments as the sender and names the recipient **by email** (resolved through AuthService). An ADMIN can create shipments for any sender and recipient. |
| S2 | An ADMIN dispatches to a chosen driver, or ShipmentService **auto-picks** the available driver with the smallest vehicle that fits the weight. |
| S3 | **Public tracking** by tracking number, without login, with limited data and rate-limited at NGINX |

## Added

| What | Where |
|---|---|
| Service definition: description, decisions, stack, FR/NFR, service-level architecture, state machine, UML class diagram, API design, events | `documents/ShipmentService/ShipmentService.md` |
| **ShipmentService** (.NET 10, same layout as the other services plus `Services/Clients/` for M2M calls) | `ShipmentService/` |
| `shipment_db`: `shipments` (snapshots + party/driver user ids, `ux_shipments_driver_active`), `shipment_status` (append-only, `actor_id`), `outbox`. Migration `InitialCreate` | `ShipmentService/Data/` |
| 10 endpoints: create, list, get, public track, dispatch, pickup, deliver, fail, cancel | `ShipmentService/Controllers/` |
| M2M clients with cached client-credentials tokens per audience; any downstream failure → 503 | `ShipmentService/Services/Clients/` |
| `shipment.lifecycle.v1` contracts (6 events) | `shared/LastMile.Contracts/Shipment/` |
| Compose: `shipment-service`, `shipment-postgres` (host port 5436). NGINX: `/api/v1/shipments/track/` public with `limit_req` (5 r/s, burst 20, then 429), and `/api/v1/shipments` behind `auth_request` | `docker-compose.yml`, `nginx.conf` |

## Changed in other services

- **AuthService** (API 1.2.0):
  - `GET /api/v1/users/lookup?email=` for ADMIN, or M2M with scope `users:lookup` (audited as `UserLookedUp`)
  - accepts the M2M audience `auth-service`
  - the dev seed adds `users:lookup` and `auth-service` to the `shipment-service` client, which was reseeded
  - new demo recipient **`mary@doe.com` / `Client12345!`**
- **FleetService:** now consumes `shipment.lifecycle.v1` (`ShipmentEventHandler`), so D3 availability follows dispatch and completion. `AuthUserConsumer` was renamed `FleetEventsConsumer`, because it routes both topics.
- **Docs:** `data-model.md` §4 is now built. The matrix, the Kafka doc (§3, §11), FleetService.md (FR-9, §8.3, diagrams), the AuthService API reference and the schema diagram are updated.

## Verified

The full matrix is in ShipmentService.md §11:
- **Creation and validation:** all rules held.
- **Access:** scoped per role; the public tracking response has no personal data.
- **Dispatch:** chosen and auto-pick both work, including the off-shift and capacity checks. The full lifecycle runs, and FleetService flips Jane's availability on dispatch and completion.
- **Cancel and fail** rules held.
- **Concurrent dispatch:** one 200, one 409.
- **Resilience:** ProfileService down → 503 in 0.14 s, while tracking still works.
- **Rate limit:** 429 beyond the burst allowance.
- **Events:** all 6 published; lookups audited.

## State for the live demo

- Accounts: admin, John (sender), Mary (recipient), Jane (driver with DEMO-001, off shift). DEMO-002 is free.
- `shipment_db` holds 6 test shipments (1 delivered, 1 failed, 4 cancelled).
- **Remaining service:** FleetTrackingService (GPS ingestion, Redis live positions, history), which will consume `fleet.driver.v1` and `shipment.lifecycle.v1`.
