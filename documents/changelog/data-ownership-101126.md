# Changelog — 2026-11-10 · Data ownership & schema diagram

> Session goal: decide **which service owns every piece of data** before ProfileService, FleetService,
> FleetTrackingService and ShipmentService are built, so data does not end up in the wrong place as the project grows.
> Previous snapshot: [project-architecture-071126.md](project-architecture-071126.md)

## Added

| File | What |
|---|---|
| [`documents/data-ownership-matrix.md`](../data-ownership-matrix.md) | The data ownership matrix: rules, service→store map, per-table read/write matrix, contested fields, replacements for cross-service FKs, proposed Kafka topics/events, mismatches found, decisions, and a "where does new data go?" checklist. |
| [`documents/diagrams/data-schema.mmd`](../diagrams/data-schema.mmd) | Mermaid UML class diagram of the schema, grouped by owning service. Solid lines are same-DB FKs, dashed lines are logical ID references across services. |
| [`documents/diagrams/data-schema.png`](../diagrams/data-schema.png) | Rendered PNG of the diagram above. |
| [`documents/diagrams/render_schema.py`](../diagrams/render_schema.py) | Renders the PNG from the `.mmd` with matplotlib, so the two stay in sync. Usage: `python3 documents/diagrams/render_schema.py documents/diagrams/data-schema.mmd documents/diagrams/data-schema.png` |

No code, `data.sql` or database changes in this session; documentation only.

## Ownership assigned

| Service | Owns | Store |
|---|---|---|
| AuthService | `users`, `refresh_tokens`, `revoked_tokens`, `service_clients`, `signing_keys`, `audit_logs` | auth_db |
| ProfileService | `admins`, `clients` | profile_db |
| FleetService | `drivers` (incl. `is_available`), `vehicles` (incl. driver↔vehicle assignment) | fleet_db |
| FleetTrackingService | live positions *(proposed, Redis)*, `location_history` *(proposed, 2-month retention)* | Redis + tracking_db |
| ShipmentService | `shipments` (incl. shipment↔driver/vehicle assignment, address snapshots), `shipment_status` | shipment_db |

Key rules adopted:
- Only the owner writes its data. There are no foreign keys between service databases.
- Other services read data from the JWT claims, by calling the owner's API, or from a local copy kept up to date by the owner's events.
- `users.id` is the identity key everywhere, stored as `user_id`.
- Deletes across services are soft deletes plus an event.

## Decisions

| # | Decision |
|---|---|
| D1 | **Every shipment recipient needs an account**: a `CLIENT` user with a `clients` profile. `clients.user_id` stays `NOT NULL`. |
| D2 | **Profiles are created from the `UserRegistered` event.** The profile fields are collected at registration: `POST /auth/register` and `POST /users` get a `profile` object (firstName, lastName, phoneNumber, plus address/companyName for CLIENT and licenseNumber for DRIVER). AuthService validates the fields and relays them in the event but does not store them. Contract: matrix §8.1. |
| D3 | **`drivers.is_available` = "on shift and ready to deliver"** for this MVP. FleetService owns it and flips it on `ShipmentDispatched` / `ShipmentDelivered` / `ShipmentFailed` / `ShipmentCancelled`. To be split into separate fields in a later MVP. |

## Issues found (not fixed yet)

1. `data.sql` labels `admins`, `clients`, `drivers`, `vehicles` as ShipmentService. The HLA puts them under ProfileService / FleetService, and the matrix follows the HLA.
2. `data.sql` is written as one database and has FKs across services (`*.user_id → users`, `shipments → clients/drivers/vehicles`). These must be dropped when it is split per service.
3. `data.sql` does not include the as-built AuthService tables (`AuthDbContext.cs` is the source of truth for those).
4. There is no FleetTrackingService schema in `data.sql`. One is proposed in the matrix and diagram.
5. AuthService does not publish events yet, and D2 depends on it.
6. `RegisterDto` / `CreateUserDto` and the portal register form have no `profile` object yet. Build this together with #5.
7. Known risk with D2: `license_number` uniqueness can only be checked by FleetService, after the account exists. The proposed handling is a `DriverProfileRejected` event.

## Next steps

- Add an outbox and a Kafka publisher to AuthService, then the registration `profile` object (#5 and #6 together).
- Start ProfileService and FleetService with `UserRegistered` consumers that can safely receive the same event twice.

## Update: legacy `data.sql` retired (same day)

`data.sql` was the initial **monolithic** schema from before the split into services. It is not the target model.

| Change | File |
|---|---|
| Added | [`documents/data-model.md`](../data-model.md): the data model as of today, per service database. It contains the as-built auth_db tables, DDL for profile_db, fleet_db and shipment_db (carried over from `data.sql`, regrouped per the HLA, cross-database FKs removed, decisions D1–D3 applied), the proposed tracking schema, and known gaps. |
| Deleted | `data.sql` (still in git history) |
| Updated | `data-ownership-matrix.md` (sources, §5, issues 1–4 in §7 marked resolved/proposed), `diagrams/data-schema.mmd`, `diagrams/render_schema.py`, `diagrams/data-schema.png` (legend no longer mentions `data.sql`) |

Issues 1–3 above are resolved by this update. Issue 4 (the tracking schema) is now in `data-model.md` §5 as a proposal.

## Update: Kafka broker architecture proposed (same day)

| Change | File |
|---|---|
| Added | [`documents/kafka-architecture.md`](../kafka-architecture.md): proposal for the Kafka component. It covers what goes on Kafka and what doesn't, the topic catalogue (`<service>.<aggregate>.v1`, keyed by aggregate id), the JSON event envelope and evolution rules, transactional outbox for producers, inbox-based idempotent consumers with retries and DLQs, the registration and dispatch flows (D2/D3), a single-broker KRaft docker-compose setup with kafka-ui, production hardening (RF 3, TLS/SASL, ACLs mirroring the ownership matrix), observability, and the implementation order. |
| Updated | `data-model.md` §7: the outbox/inbox gap now points to the proposal. The changelog link now points to `changelog/` instead of `milestones/`. |

Status: proposal only. Nothing has been added to `docker-compose.yml` or the services yet.
