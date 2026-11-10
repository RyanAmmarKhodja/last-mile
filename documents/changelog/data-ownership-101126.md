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

## Update: Kafka message broker implemented (same day)

Decisions: **K1** 30-day retention is enough. **K2** NotificationService is a future service, not in this MVP. **K3** Schema registry adopted: Confluent Schema Registry, JSON Schema, `FORWARD_TRANSITIVE` (chosen by testing against the live registry). **K4** Outbox and inbox code lives in one shared `LastMile.Messaging` library. Details: [kafka-architecture.md §12](../kafka-architecture.md#12-decisions-2026-11-10).

| Change | Where |
|---|---|
| Added | `shared/LastMile.Contracts`: topics, event envelope, JSON conventions, `auth.user.v1` events (`UserRegistered`, `UserEmailChanged`, `UserRoleChanged`, `UserDeactivated`, **`UserReactivated`** (new), `UserDeleted`) |
| Added | `shared/LastMile.Messaging`: transactional outbox + relay, outbox health check, inbox-based `EventConsumer<TDb>` with retries + DLQ, Schema Registry framing, EF mappings, DI extensions |
| Added | `docker-compose.yml`: `kafka` (KRaft, host port 9094), `kafka-init` + `infra/kafka/create-topics.sh` (10 topics incl. DLQs, 30-day retention), `schema-registry` (8081), `kafka-ui` (8090) |
| Changed | **AuthService:** `outbox` table (migration `AddOutbox`); events staged in the same transaction as register / create / email / role / active / delete; `profile` object **required** on `POST /auth/register` and `POST /users`, validated per role (`Core/RegistrationProfileRules.cs`); seeded admin gets a profile (`Seed:Admin:FirstName/LastName`); `/health` includes the outbox lag |
| Changed | AuthService image builds from the **repository root** (needs `shared/`); root `.dockerignore` added |
| Changed | **mile-portal:** register form and admin "create user" form collect the role-specific profile fields |
| Updated | `kafka-architecture.md` (proposal → implemented), `data-ownership-matrix.md` (§6 events, §7 items 6–7 resolved), `data-model.md` (`outbox` table) |

**Verified end to end** (docker compose, through NGINX):
- **Validation:** missing profile → 400, role-specific field errors → 400.
- **Events:** register / create / email / role / deactivate / reactivate / delete each wrote the expected `auth.user.v1` event, all published. Each user's events land in order on one partition, framed with the registry schema id. One subject was registered per event type.
- **Kafka down:** registering with Kafka stopped still returns 201, and the event is delivered after restart.
- **Consumer smoke test:** events deserialized, duplicate skipped, poison message dead-lettered with headers.

Test accounts were deleted afterwards, so `auth.user.v1` holds their `UserRegistered`/`UserDeleted` pairs. That is harmless for tomorrow's consumers.

**Breaking API change:** clients of `POST /auth/register` and `POST /users` must now send `profile`.

## Update: ready for the new services (same day)

| Change | Where |
|---|---|
| **Accounts recreated** | `admin@lastmile.local`, `john@doe.com` (CLIENT), `jane@doe.com` (DRIVER) had no `UserRegistered` event. John and Jane were deleted through the API, and the admin was deleted with SQL (the API refuses to delete the last admin; recorded in `audit_logs`). All three were re-seeded with profiles and their `UserRegistered` events were published. **New ids and dev passwords:** see [AuthService/api-reference.md §6](../AuthService/api-reference.md#6-demo-accounts-development). |
| Added | `Seed:Users` in AuthService: demo accounts with profiles, created at startup if missing, each publishing `UserRegistered` (`appsettings.Development.json`) |
| Added | [`documents/AuthService/api-reference.md`](../AuthService/api-reference.md) (API v1.1.0): every endpoint with access, status codes and the events it publishes; registration contract and per-role rules; implemented claims (`profile_id` from spec v1.0.0 dropped); demo accounts |
| Changed | OpenAPI: register / create user document `400` and `409`, and XML doc comments now appear in `/openapi/v1.json` (`GenerateDocumentationFile`) |
| Fixed | **Gateway down after restarts:** `nginx` had no restart policy (it was not running this morning). Now `restart: unless-stopped`. |
| Fixed | **Gateway pinned to stale container IPs:** NGINX resolved `auth-service` / `mile-portal` once at startup. It now resolves per request through Docker DNS (`resolver 127.0.0.11`), so it starts even if a service is down and survives container recreation. |
| Fixed | `Error: libgssapi_krb5.so.2` at AuthService startup: `GSS Encryption Mode=Disable` in the connection strings |
| Fixed | Removed the obsolete `version:` key from `docker-compose.yml` |

**Verified** after `docker compose down` and `docker compose up -d --build` (volumes kept):
- All containers start, and topics are re-ensured.
- The three accounts are seeded, and their events are published once Schema Registry is up. The relay retried until the registry was ready.
- All three accounts can log in, and `/users/me` and `/auth/validate` work for each.
- Registering returns 201, and a duplicate email returns 409.
- Recreating `auth-service` and `mile-portal` keeps the gateway routing without restarting NGINX.
