# Project Architecture — Snapshot of 2026-11-07

> High-level description of the Last-Mile Delivery Fleet & Real-Time Tracking System **as built today**.
> For the target design see [high-level-architecture.md](high-level-architecture.md) and
> [product-requirements-document.md](product-requirements-document.md).

## 1. Where we are

The end goal is a microservice platform that tracks delivery vehicles live on a map. So far we have built
the **identity layer** and a **minimal web portal** to test authentication end to end:

| Component | Status | Tech |
|---|---|---|
| NGINX API gateway | ✅ running | nginx (docker) |
| AuthService | ✅ running | .NET 10, ASP.NET Core, EF Core, PostgreSQL 16 |
| `auth_db` | ✅ running | PostgreSQL 16 (own container, database-per-service) |
| mile-portal (web SPA) | ✅ MVP: auth only | React 19, TypeScript, Vite, react-router, axios |
| ProfileService, FleetService, FleetTrackingService, ShipmentService | ⏳ not started | .NET |
| Kafka, Redis, PostGIS, driver simulator, live map | ⏳ not started | — |

## 2. Runtime topology

Everything runs with `docker compose up --build` on one bridge network (`app-net`). Only the gateway is
published to the host (port 80, plus Postgres on 5433 for local tooling).

```
                Browser  ──────────►  http://localhost  (port 80)
                                          │
                                ┌─────────▼──────────┐
                                │   nginx-gateway    │
                                └──┬──────────────┬──┘
           /api/v1/auth/*          │              │   /  (everything else)
           /api/v1/users*          │              │
           /api/v1/service-clients │              │
           /.well-known/openid-... │              │
                       ┌───────────▼───┐      ┌───▼────────────┐
                       │ auth-service  │      │  mile-portal   │
                       │ .NET 10 :8080 │      │ nginx + static │
                       └───────┬───────┘      │  React bundle  │
                               │              └────────────────┘
                       ┌───────▼───────┐
                       │ auth-postgres │  auth_db
                       └───────────────┘
```

- **Same origin**: the SPA and the API are both served from `http://localhost`, so there is no CORS
  configuration and no API URL baked into the frontend build (it always calls `/api/v1/...`).
- Unknown `/api/*` paths get a JSON 404 from the gateway instead of falling through to the SPA.
- The gateway also exposes an internal `/_auth` location (`auth_request` → `GET /api/v1/auth/validate`) so
  future services can be protected at the edge and receive `X-Auth-Subject` / `X-Auth-Role` headers.

## 3. AuthService (identity provider)

Single source of truth for credentials, roles and token issuance.

**Roles**: `ADMIN`, `DRIVER`, `CLIENT` (humans) and `M2M` (service principals only).

**Tokens**
- Access token: JWT signed **RS256**, 15 min, claims `sub`, `role`, `email`, `jti`, `iss`, `aud`, `exp`.
- Refresh token: opaque, 7 days, **rotated on every use**, stored hashed in the DB.
- Logout blacklists the access token's `jti` and revokes the refresh token; every request checks the blacklist.
- Changing a password, role or active status revokes all of that user's refresh tokens.
- Signing keys live in the `signing_keys` table (survive restarts, shared by replicas) and are published as
  JWKS at `/api/v1/auth/jwks.json`; `/.well-known/openid-configuration` lets downstream .NET services
  validate tokens locally.
- M2M: OAuth 2.0 client-credentials grant at `/api/v1/auth/m2m/token` with scopes and audiences.

**Endpoints used by the portal**

| Endpoint | Who |
|---|---|
| `POST /api/v1/auth/register` | anonymous; CLIENT or DRIVER only |
| `POST /api/v1/auth/login` / `refresh` / `logout` | anonymous / anonymous / signed-in |
| `GET /api/v1/auth/validate` | signed-in (gateway check) |
| `GET /api/v1/users/me` | signed-in |
| `GET /api/v1/users` (search, role filter, paging) | ADMIN |
| `POST /api/v1/users` (any human role incl. ADMIN) | ADMIN |
| `GET/PUT/DELETE /api/v1/users/{id}` | owner or ADMIN. Owners change email/password with `currentPassword`; only ADMIN changes role / isActive |

**Other internals**: domain model (`Core/User.cs`) holds validation rules (normalized email, password
8–128 chars with a letter and a digit); errors come back as RFC 7807 problem details; an audit log records
every auth event; a hosted service purges expired blacklist/refresh rows hourly; the last active admin
cannot be demoted, disabled or deleted. In Development, an admin (`admin@lastmile.local`) and a
`shipment-service` M2M client are seeded at startup.

## 4. mile-portal (React SPA)

MVP whose only job today is exercising AuthService through the gateway.

### Pages

| Route | Access | Purpose |
|---|---|---|
| `/login` | anonymous only | Email + password sign-in |
| `/register` | anonymous only | Create a CLIENT or DRIVER account |
| `/home` | signed-in | Profile (email, role, id, status, dates), role capabilities, **live server-side access checks** (calls real endpoints and shows 200/403/404 against what your role should get), decoded access-token claims with expiry countdown |
| `/account` | signed-in | Change email, change password (then forced sign-out), delete own account |
| `/admin/users` | **ADMIN** | List/search/filter/paginate users, create users of any role, change role, enable/disable, reset password, delete |
| `/forbidden` | signed-in | Where the role guard sends you (shows required role vs yours) |

The sidebar keeps the admin entry visible with a 🔒 for non-admins so the client-side guard can be
demonstrated; the "access checks" card shows the backend enforcing the same rules.

### Folder structure

```
mile-portal/
├── Dockerfile            # node build → nginx:alpine static server
├── nginx.conf            # SPA fallback (try_files … /index.html)
├── vite.config.ts        # dev server :5173, proxies /api → gateway on :80
└── src/
    ├── main.tsx
    ├── shared/
    │   ├── api/          # axios client + interceptors, tokenStore, authApi, usersApi, error parsing
    │   ├── components/   # Button, TextField, SelectField, Alert, Card, RoleBadge, Spinner
    │   ├── context/      # AuthContext (session state, login/logout, hasRole)
    │   ├── layout/       # AppLayout (Navbar + Sidebar), AuthLayout
    │   ├── router/       # AppRouter, ProtectedRoute (auth + roles), PublicOnlyRoute
    │   ├── pages/        # NotFoundPage
    │   ├── styles/       # globals.css (design tokens, .NET purple/blue)
    │   ├── types/        # DTO types mirroring AuthService
    │   └── utils/
    └── features/
        ├── auth/         # pages, components, styles, hooks, utils (jwt decode, validation, role info)
        └── admin-portal/ # Users management page
```

### Session handling

- The access token lives **in memory only**. The refresh token is in `localStorage` so a reload can resume
  the session (`refresh` → `GET /users/me`). This is acceptable for the MVP. Production should move the
  refresh token to an HttpOnly cookie.
- An axios response interceptor catches a 401, does **one** refresh, and retries the request. Refresh is
  single-flight because tokens rotate: concurrent callers (or React StrictMode's double effects) share
  one refresh call, so nobody reuses a rotated token. If the refresh fails, the session ends with a
  "session expired" notice.
- The UI reads the role from `/users/me`. The backend authorizes using the token's `role` claim. After a
  role change, the two can disagree until the token is replaced, and the token card warns about this.

## 5. Running it

```bash
docker compose up -d --build        # gateway, auth-service, auth-postgres, mile-portal
open http://localhost               # portal (admin: admin@lastmile.local / Admin12345!)

# Frontend hot-reload against the running stack:
cd mile-portal && npm install && npm run dev   # http://localhost:5173
```

## 6. Known gaps / next steps

- No "username" or profile data yet; the email is the identifier. Names, phone and addresses belong to
  the future **ProfileService**.
- No HTTPS, rate limiting or HttpOnly-cookie refresh flow at the gateway yet.
- No automated frontend tests. End-to-end testing is manual for now.
- Next: ProfileService / FleetService / ShipmentService behind `auth_request`, Kafka, the
  FleetTrackingService + Redis geo cache, the driver simulator, and the live map in mile-portal.
