# Changelog: 2026-11-11 · FleetTrackingService (fifth microservice)

> Design: [FleetTrackingService/FleetTrackingService.md](../FleetTrackingService/FleetTrackingService.md) (written first, then built).
> Previous: [shipment-service-111126.md](shipment-service-111126.md)

## Decisions (asked and answered before the design)

| # | Decision |
|---|---|
| T1 | The ping carries only coordinates and a timestamp. **The driver comes from the JWT.** Vehicle, shipment and tracking number come from a Kafka-fed read model. An M2M client with `tracking:ingest` (simulator / GPS gateway) names the driver. |
| T2 | Live delivery uses **SSE** plus polling, fanned out through Redis pub/sub. The admin fleet stream is coalesced to one batch per second. |
| T3 | **Public by tracking number**, only while the shipment is DISPATCHED or IN_TRANSIT, without driver identity. ADMIN sees everything. A DRIVER sees only themselves. |
| T4 | Pings are stored **only while the driver is on shift**. NGINX caches `/auth/validate` for 30 s per token. |
| T5 | **30-day** retention (replaces the HLA's 2 months), with daily partitions dropped by the purge |
| T6 | Cold storage **samples** the latest position per driver every 2 minutes |

## Added

| What | Where |
|---|---|
| Service definition: spec review, decisions, FR/NFR, architecture, Redis layout, data model, UML, API, events, verification | `documents/FleetTrackingService/FleetTrackingService.md` |
| **FleetTrackingService** (.NET 10, namespace `LastMile.Tracking`) | `FleetTrackingService/` |
| Redis live store: one Lua script per ping (newer-than check + HSET + GEOADD + ZADD + PUBLISH) | `Services/LivePositionStore.cs` |
| SSE: public stream by tracking number, admin fleet stream, `PositionHub` (one subscription per replica) | `Controllers/`, `Services/PositionHub.cs` |
| Background jobs: `PositionSampler` (2 min, Redis lock), `LiveSweeper` (30 s), `HistoryMaintenance` (startup + 1 h, advisory lock) | `Services/Background/` |
| `tracking_db` (PostGIS): `tracked_drivers`, `tracked_shipments`, `location_history` (daily partitions, generated `geography` column), `inbox`. Migration `InitialCreate`. | `Data/` |
| Consumer group `tracking-service`: `fleet.driver.v1` + `shipment.lifecycle.v1` | `Services/Events/` |
| Compose: `tracking-service`, `tracking-postgres` (PostGIS, host port 5437), `tracking-redis` (no persistence, host port 6380) | `docker-compose.yml` |
| Python simulator, stdlib only: `drive` (route replay as a driver), `load` (M2M batches) | `tools/telemetry-simulator/simulator.py` |

## Changed in other components

- **NGINX:**
  - `/api/v1/tracking/track/` is public, with `limit_req` and `limit_conn`.
  - `/api/v1/tracking/` uses `auth_request /_auth_cached`, a 30 s cache keyed by token.
  - SSE settings, and a keepalive `upstream` for the tracking service.
  - `worker_processes auto` (it was 1: the gateway saturated at about 600 req/s).
- **AuthService (API 1.3.0):**
  - `/auth/validate` uses a new `Gateway` JwtBearer scheme without the audience check, so M2M tokens for other services pass the gateway. Downstream services still check their audience.
  - Dev seed: M2M client `telemetry-simulator` (`tracking:ingest tracking:read`, audience `tracking-service`).
- **Docs:**
  - HLA: retention 30 days.
  - Ownership matrix: FleetTrackingService and Kafka are built, with new rows.
  - `data-model.md` §5 is built.
  - Kafka doc: §11 step 5 done.
  - AuthService API reference and the schema diagram are updated.

## Verified

The full matrix is in FleetTrackingService.md §11:
- **Ingest rules and roles:** all held.
- **Public SSE:** average 31 ms from ping to browser. No position leaks after delivery.
- **Throughput:** **1 000 single pings/s through NGINX at p95 1.7 ms** (2 000/s at p95 4 ms).
- **Sampling, purge and resilience:** sampling into daily partitions, the 30-day partition purge, and a Redis outage (503, then automatic recovery) all behaved as designed.
