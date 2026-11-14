# Changelog: 2026-11-14 · mile-portal v2 (live maps & operations)

> Design: [mile-portal/mile-portal.md](../mile-portal/mile-portal.md) (written first, then built).
> Previous: [fleet-tracking-service-111126.md](fleet-tracking-service-111126.md) · portal v1: [project-architecture-071126.md](project-architecture-071126.md) §4

The portal goes from an authentication test bench to the product's entry point: live maps for customers and admins, shipment and fleet operations, a driver workspace, and a public tracking page.

## Decisions (made for this build, open to review)

| # | Decision |
|---|---|
| P1 | MapLibre GL JS + **OpenFreeMap** vector tiles (free, no key). The style URL can be changed with `VITE_MAP_STYLE_URL`. |
| P2 | The client map uses the **public** tracking stream by tracking number, so signed-in clients get exactly the T3 data. No backend change. |
| P3 | The admin map uses the authenticated fleet SSE through a fetch-based reader (refresh on `401`, backoff), with a 5 s polling fallback. |
| P4 | Vehicles are one GeoJSON layer (built for 1 000+ vehicles). The single delivery marker is animated between pings. |
| P5 | No destination pins: geocoding addresses would send them to a third party. This waits for coordinates on shipments. |
| P6 | TanStack Query for server state, on top of the existing axios client |
| P7 | The sidebar shows each role only what it can open. The v1 diagnostics move to Account → Session. |
| P8 | One public SSE connection per tracking number, shared across components (NGINX allows 10 per IP) |

## Added

| What | Where |
|---|---|
| Frontend definition: decisions, FR/NFR, architecture, live data paths, pages, role rules, build notes | `documents/mile-portal/mile-portal.md` |
| Map kit: `MapView`, `VehicleLayer` (heading arrows, plate labels, stale fade), `TrailLayer`, animated `DeliveryMarker` | `mile-portal/src/shared/map/` |
| API modules for Profile, Fleet, Shipment and Tracking; fetch-based SSE reader; DTO types | `src/shared/api/`, `src/shared/types/` |
| **Admin:** operations dashboard (KPIs, live mini-map, status breakdown, one-click auto-dispatch), full-screen **live fleet map** (search, filter, follow, 2 h trail), shipments, fleet (drivers: shift / edit; vehicles: add / edit / assign / retire) | `features/dashboard`, `features/tracking`, `features/shipments`, `features/fleet` |
| **Client:** "My deliveries" with the **live map of the current delivery**, shipment list (sender / recipient), send a parcel by recipient email, shipment page with live map and timeline | same |
| **Driver:** workspace with shift toggle, current job (pick up / deliver / fail), own position and trail | `features/dashboard/pages/DriverWorkspace.tsx` |
| **Public tracking page** `/track/:trackingNumber` (no login): status timeline + live map | `features/public-tracking` |
| Account: role profile editing (ProfileService for CLIENT/ADMIN, FleetService for DRIVER); Session tab | `features/auth` |
| Dependencies: `maplibre-gl` 5, `@tanstack/react-query` 5, `@types/geojson` (dev) | `mile-portal/package.json` |

## Changed in other components

- **NGINX:** both tracking locations set `proxy_connect_timeout 3s`. With FleetTrackingService down, they used to fail only after 60 s, with a 504.
- **Docs:**
  - HLA §4.2: MapLibre chosen, SSE + polling.
  - FleetTrackingService.md:
    - the `offline` event shape is `{driverIds: [...]}`;
    - the new gateway timeout is documented;
    - new open point 5 (off-shift driver lingers after a delivery).

## Verified

The details are in mile-portal.md §10:
- **Build:** typecheck and build pass, and the container was rebuilt.
- **Pages:** all of them load for each role with no JS errors.
- **Live maps:** the admin, client, shipment and public views follow Jane in real time.
- **Lifecycle through the UI:**
  1. A client creates a shipment.
  2. The admin's dispatch attempt surfaces the backend's `409`.
  3. The admin cancels it with a note.
  4. Jane picks up her parcel, and the public page switches to *In transit* live.
  5. Jane delivers it, and the public page hides the position and shows *Delivered*.
  6. The admin sees the route replay.
- **Tracking outage:** the map shows "unavailable", the rest keeps working, and the map recovers on its own.
- **Phone width:** 390 px without horizontal scroll.

**Not verified:**
- 1 000 vehicles on the map, since there is only one driver.
- A token refresh during an open admin stream.

## State left

- `LM-EPJC85BD1889` is DELIVERED.
- `LM-KJS1W7ADPH7J` (UI test) is CANCELLED.
- Jane is off shift.
- No accounts or vehicles were added.
