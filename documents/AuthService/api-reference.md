# AuthService: API Reference

> **Version:** 1.1.0 (2026-11-10). It supersedes the *API Design* section of [AuthService.pdf](AuthService.pdf) (v1.0.0).
> **What changed in 1.1.0:**
> - `profile` is **required** on `POST /auth/register` and `POST /users`.
> - Every account change publishes an `auth.user.v1` Kafka event.
> - The planned `profile_id` claim is dropped (see §5).
>
> **Machine-readable:** OpenAPI at `GET /openapi/v1.json` on the service (Development only). It is not routed through the gateway. From inside the docker network: `curl auth-service:8080/openapi/v1.json`.
> **Related:** [data-ownership-matrix.md §8.1](../data-ownership-matrix.md#81-registration--profile-contract-d2) · [kafka-architecture.md](../kafka-architecture.md)

## 1. Conventions

| | |
|---|---|
| Base URL | `http://localhost/api/v1` through the NGINX gateway. AuthService itself listens on `auth-service:8080`. |
| Format | JSON, camelCase. Enums are strings (`"CLIENT"`). |
| Authentication | `Authorization: Bearer <access token>` (RS256 JWT, 15 min). Refresh tokens are opaque, last 7 days and are rotated on every use. |
| Errors | RFC 7807 `application/problem+json`: `{ type, title, status, detail, traceId }`. Model-binding errors add `errors: { field: [messages] }`. OAuth endpoints use `{ error, error_description }`. |
| Tracing | Send `X-Request-Id`. It becomes the `correlationId` of every event the request publishes. |

## 2. Endpoints

| Method & path | Access | Success | Errors | Publishes (`auth.user.v1`) |
|---|---|---|---|---|
| `POST /auth/register` | public | `201` UserDto | `400` validation · `409` email taken | `UserRegistered` |
| `POST /auth/login` | public | `200` TokenResponse | `400` · `401` bad credentials / disabled | — |
| `POST /auth/refresh` | public (refresh token) | `200` TokenResponse | `400` · `401` invalid / expired / reused | — |
| `POST /auth/logout` | signed in | `204` | `401` | — |
| `GET /auth/validate` | signed in | `200` + `X-Auth-Subject`, `X-Auth-Role`, `X-Auth-Client-Id` headers | `401` | — |
| `POST /auth/m2m/token` | client credentials (Basic or form) | `200` `{access_token, token_type, expires_in, scope}` | `400`/`401` OAuth error | — |
| `GET /auth/jwks.json` | public | `200` JWKS | — | — |
| `GET /.well-known/openid-configuration` | public | `200` discovery document | — | — |
| `GET /users/me` | signed in | `200` UserDto | `401` | — |
| `GET /users?page&pageSize&role&search` | ADMIN | `200` `{items, page, pageSize, totalCount}` | `401` · `403` | — |
| `POST /users` | ADMIN | `201` UserDto | `400` · `403` · `409` | `UserRegistered` |
| `GET /users/{id}` | owner or ADMIN | `200` UserDto | `403` · `404` | — |
| `PUT /users/{id}` | owner (email, password + `currentPassword`) or ADMIN (anything) | `200` UserDto | `400` · `403` · `404` · `409` | `UserEmailChanged`, `UserRoleChanged`, `UserDeactivated` / `UserReactivated` (one per changed field) |
| `DELETE /users/{id}` | owner or ADMIN | `204` | `403` · `404` · `409` last admin | `UserDeleted` |
| `GET /service-clients` | ADMIN | `200` list | `401` · `403` | — |
| `POST /service-clients` | ADMIN | `201` with `client_secret` (shown once) | `400` · `409` | — |
| `POST /service-clients/{id}/rotate-secret` | ADMIN | `200` new secret | `404` | — |
| `DELETE /service-clients/{id}` | ADMIN | `204` | `404` | — |

A `PUT /users/{id}` that changes nothing publishes nothing. Password changes publish nothing. A password, role or status change also revokes all of that user's refresh tokens.

Events are written to the outbox **in the same transaction** as the change. The HTTP call succeeds even when Kafka is down, and the event follows within seconds of the broker being reachable.

## 3. Registration (changed in 1.1.0)

`POST /auth/register` accepts `CLIENT` or `DRIVER`. `POST /users` (ADMIN) accepts any human role, including `ADMIN`. Both take the same body:

```jsonc
{
  "email": "jane@acme.com",          // required, ≤255, unique (case-insensitive)
  "password": "Passw0rd!",           // required, 8–128, at least one letter and one digit
  "role": "CLIENT",                  // CLIENT | DRIVER (| ADMIN via POST /users)
  "profile": {                       // required since 1.1.0
    "firstName": "Jane",             // required, ≤100
    "lastName": "Doe",               // required, ≤100
    "phoneNumber": "+213 555 12 34", // ≤50
    "companyName": "Acme",           // ≤255
    "address": "12 Rue …, Alger",    // ≤1000
    "licenseNumber": "DZ-123456"     // ≤100
  }
}
```

**Per-role rules** ([`Core/RegistrationProfileRules.cs`](../../AuthService/Core/RegistrationProfileRules.cs)). Values are trimmed, and empty strings count as "not provided".

| Field | CLIENT | DRIVER | ADMIN |
|---|:-:|:-:|:-:|
| `firstName`, `lastName` | required | required | required |
| `phoneNumber` | required | required | optional |
| `address` | required | **not allowed** | not allowed |
| `companyName` | optional | not allowed | not allowed |
| `licenseNumber` | not allowed | required | not allowed |

A field that doesn't apply to the role returns `400` instead of being silently dropped. Example:

```json
{ "title": "Validation failed", "status": 400, "detail": "Address and company only apply to clients." }
```

**Response `201`:** `UserDto`. It does **not** include the profile, because AuthService doesn't store it.

```json
{ "id": "01a1c792-…", "email": "jane@acme.com", "role": "CLIENT", "isActive": true,
  "createdAt": "2026-11-10T22:27:13Z", "updatedAt": "2026-11-10T22:27:13Z" }
```

**Where the profile goes:** it's published once in `UserRegistered` (`auth.user.v1`, key = `id`). ProfileService stores it for `CLIENT` and `ADMIN`, and FleetService for `DRIVER`. From then on, read and edit the profile through those services, never through AuthService. Profiles appear a short time after the `201` (eventual consistency). A client that gets `404` from the profile service right after registering should retry briefly.

**Uniqueness of `licenseNumber`** is checked by FleetService, not here. A duplicate is reported asynchronously with `DriverProfileRejected` (matrix §8.1).

## 4. Shared types

```ts
UserDto           { id: uuid, email, role: "ADMIN"|"DRIVER"|"CLIENT", isActive, createdAt, updatedAt }
TokenResponse     { accessToken, refreshToken, tokenType: "Bearer", expiresIn: seconds, refreshTokenExpiresAt }
UpdateUserRequest { email?, password?, currentPassword?, role?, isActive? }   // omitted = unchanged
Validate (200)    { subject, role?, email?, clientId?, scope?, expiresAt? }
```

## 5. Access-token claims (as implemented)

`iss`, `sub` (user id), `aud`, `exp`, `iat`, `jti`, `role`, `email`. M2M tokens carry `client_id`, `scope` and `gty: "client_credentials"`.

**`profile_id` (in spec v1.0.0) is not issued and is dropped.** Profiles are created asynchronously by other services, so AuthService cannot know their ids when it signs a token. Downstream services look a profile up by **`sub` = `user_id`**, which is unique in `admins`, `clients` and `drivers`.

## 6. Demo accounts (Development)

Seeded at startup from `appsettings.Development.json` (`Seed:Admin`, `Seed:Users`) if the email doesn't exist yet. Each seeded account publishes `UserRegistered` with its profile, so the downstream services will create matching profiles.

| Email | Password | Role | Profile |
|---|---|---|---|
| `admin@lastmile.local` | `Admin12345!` | ADMIN | System Administrator |
| `john@doe.com` | `Client12345!` | CLIENT | John Doe, Doe Trading (demo), 1 Demo Street, Demo City |
| `jane@doe.com` | `Driver12345!` | DRIVER | Jane Doe, license `DEMO-DRV-0001` |

To add a demo account, append it to `Seed:Users` and restart AuthService.
