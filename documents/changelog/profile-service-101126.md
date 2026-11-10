# Changelog: 2026-11-10 · ProfileService (second microservice)

> Design: [ProfileService/ProfileService.md](../ProfileService/ProfileService.md) (written first, then built).
> Previous: [data-ownership-101126.md](data-ownership-101126.md)

## Added

| What | Where |
|---|---|
| Service definition: description, tech stack, functional and non-functional requirements, service-level architecture, UML class diagram, API design, events | `documents/ProfileService/ProfileService.md` |
| **ProfileService** (.NET 10, same layout as AuthService: `Controllers/ Core/ Data/ DTOs/ Enums/ Models/ Options/ Services/`) | `ProfileService/` |
| `profile_db`: `admins`, `clients` (with `status` soft delete + `updated_at`), `inbox`, `outbox`. Migration `InitialCreate` | `ProfileService/Data/` |
| 11 endpoints under `/api/v1/profiles/{clients,admins}`: `me` (read + create-or-update), list/search, get by id, client by user id, admin edit | `ProfileService/Controllers/` |
| Kafka: consumes `auth.user.v1` (group `profile-service`); publishes `profile.client.v1` (`ClientCreated/Updated/Deactivated/Reactivated/Deleted`) | `ProfileService/Services/`, `shared/LastMile.Contracts/Profile/ClientEvents.cs` |
| Compose: `profile-service`, `profile-postgres` (host port 5434). NGINX: `/api/v1/profiles/` behind `auth_request`, JSON 401 | `docker-compose.yml`, `nginx.conf` |

## Changed

- **`LastMile.Messaging`:** `EventConsumer.HandleAsync` now also receives the scope's `IServiceProvider`, so scoped services such as `IEventOutbox` share the inbox transaction.
- **AuthService dev seed:** the `shipment-service` M2M client gets scope `profiles:read` and audience `profile-service`. The client was deleted and re-seeded so it picked these up, and its secret is unchanged.
- **AuthService:** stale `profile_id` comment in `TokenService` corrected.
- **Docs:** `data-model.md` §2 is now built (DDL as implemented). The matrix, the Kafka doc and the schema diagram are updated.

## Verified

- The `auth.user.v1` history was replayed into the empty `profile_db`, giving the demo admin and John profiles. Jane (DRIVER) was skipped.
- A new registration's profile was readable after 1.5 s.
- Deactivate and delete flow through to the profile, and the matching events are published.
- RBAC matrix:
  - no token → 401
  - CLIENT: own profile only
  - DRIVER: 403
  - ADMIN: full access; editing a DELETED profile → 409
  - M2M `profiles:read` inside the network: clients only
- A revoked token gets 401 at the gateway.
- 5 Schema Registry subjects were registered for `profile.client.v1`.

## Notes

- M2M callers must call `http://profile-service:8080` directly. The gateway is for user tokens.
- Test accounts from development appear as `DELETED` client/admin rows (history replay). This is harmless.
