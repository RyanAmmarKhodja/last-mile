# Changelog: 2026-11-11 · FleetService (third microservice)

> Design: [FleetService/FleetService.md](../FleetService/FleetService.md) (written first, then built).
> Previous: [profile-service-101126.md](profile-service-101126.md)

## Added

| What | Where |
|---|---|
| Service definition: description, tech stack, FR/NFR, service-level architecture, UML class diagram, API design, events | `documents/FleetService/FleetService.md` |
| **FleetService** (.NET 10, same layout as AuthService and ProfileService) | `FleetService/` |
| `fleet_db`: `drivers` (status, normalized license, `is_available` default false), `vehicles` (normalized plate, retire instead of delete), D3 check constraints, `inbox`, `outbox`. Migration `InitialCreate` | `FleetService/Data/` |
| 14 endpoints under `/api/v1/fleet`: driver `me` (read, create-or-update, start/end shift), driver list/get/by-user/edit/availability (admin), vehicle list/get/create/edit/assign/retire | `FleetService/Controllers/` |
| Kafka: consumes `auth.user.v1` (group `fleet-service`); publishes `fleet.driver.v1` (7 types incl. `DriverProfileRejected`) and `fleet.vehicle.v1` (5 types) | `FleetService/Services/`, `shared/LastMile.Contracts/Fleet/` |
| Dev seed: demo vehicles `DEMO-001`, `DEMO-002` | `FleetService/appsettings.Development.json` |
| Compose: `fleet-service`, `fleet-postgres` (host port 5435). NGINX: `/api/v1/fleet/` behind `auth_request` | `docker-compose.yml`, `nginx.conf` |

## Changed

- **Event names finalized** (they were provisional in the matrix and the Kafka doc): `DriverUnavailable` was replaced by `DriverDeactivated` + `DriverAvailabilityChanged(false)`. Added `DriverUpdated`, `DriverReactivated`, `DriverDeleted`, `VehicleUpdated` and `VehicleReactivated`.
- **Docs:** `data-model.md` §3 is now built (DDL as implemented). The matrix (store map, §3 rows, §5, §6), the Kafka doc (§3, §11) and the schema diagram are updated.

## Verified

The full matrix is in FleetService.md §11:
- **Availability and assignment rules:** a driver can't go available without a vehicle. A driver who already has a vehicle, or a retired vehicle, can't be assigned. Losing the vehicle ends the shift.
- **Duplicate license:** registering with a taken license produces `DriverProfileRejected`, and the user then fixes it with `PUT /me`.
- **Account lifecycle:** deactivate, reactivate and delete reach the driver, and a deleted driver's vehicle is released.
- **RBAC:** CLIENT 403, DRIVER limited to their own profile and vehicle, ADMIN everything, M2M read-only.
- **Validation:** identifiers are unique regardless of case and spacing.
- **Events:** all 12 event types were published and 10 schema subjects registered.

## State left for the next service

- Jane (`jane@doe.com` / `Driver12345!`) operates **DEMO-001** and is currently not on shift. **DEMO-002** is free.
- Pending, by design: the FleetService consumer for `shipment.lifecycle.v1` (availability on dispatch and completion) is added with ShipmentService (FleetService.md §8.3).
