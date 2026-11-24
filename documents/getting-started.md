# Getting started: launching the app

How to run the whole Last-Mile platform on your machine, sign in, and see a vehicle move on the map.

## 1. Prerequisites

| Tool | Why | Check |
|---|---|---|
| Docker + Docker Compose v2 | runs every service | `docker compose version` |
| Python 3.10+ | the telemetry simulator (standard library only) | `python3 --version` |
| Node.js 22 *(optional)* | only for frontend hot reload | `node -v` |
| Internet access | the first build pulls images and packages, and the map tiles come from OpenFreeMap | |

Ports **80, 5433–5437, 6380, 8081, 8090 and 9094** must be free.

## 2. Start everything

From the repository root:

```bash
docker compose up -d --build
```

The first build takes a few minutes. Then check that everything is up:

```bash
docker compose ps
```

All services should be `Up`. The databases, Kafka and Schema Registry should be `healthy`. `kafka-init` runs once, creates the topics and exits, so it may be missing from the list. That is normal.

Give it about 30 seconds after the containers start. The services seed the demo accounts and publish their events once Kafka and Schema Registry are ready.

## 3. Open the portal

Go to **http://localhost** and sign in with a demo account:

| Email | Password | Role | What to look at |
|---|---|---|---|
| `admin@lastmile.local` | `Admin12345!` | ADMIN | Dashboard, **Live map**, Shipments, Fleet, Users |
| `john@doe.com` | `Client12345!` | CLIENT | **My deliveries** (live map), send a parcel |
| `mary@doe.com` | `Client12345!` | CLIENT | parcels sent to her |
| `jane@doe.com` | `Driver12345!` | DRIVER | driver workspace (shift, current job) |

No account is needed for **http://localhost/track**: enter a tracking number to follow a parcel.

## 4. See a delivery move on the map (demo scenario)

1. **As John:** *Send a parcel* → recipient `mary@doe.com`, weight `2` → **Create shipment**. Note the tracking number (`LM-…`).
2. **As the admin:** open the shipment → **Dispatch** → leave *Automatic* → **Dispatch**. ShipmentService picks Jane, who operates `DEMO-001`.
   - If no driver is available, open **Fleet → Drivers** and click **Start shift** on Jane, then retry.
   - On a fresh database Jane has no vehicle yet. First open **Fleet → Vehicles** → `DEMO-001` → **Assign** → Jane.
3. **Start the driver simulator.** It logs in as Jane and drives a route through Algiers, one GPS ping per second:

   ```bash
   python3 tools/telemetry-simulator/simulator.py drive --email jane@doe.com --password 'Driver12345!'
   ```

4. Watch the vehicle move on:
   - the admin's **Live map**;
   - John's **My deliveries**;
   - the public page `http://localhost/track/LM-…`.
5. **As Jane:** **Confirm pick-up**, then **Mark delivered**. The public page switches to *Delivered* and stops showing the position.

Stop the simulator with `Ctrl+C`. After the delivery, Jane's pings are refused (`409 Driver is not on shift`), by design.

## 5. Developer tools

| URL / port | What |
|---|---|
| http://localhost | Portal and API gateway (`/api/v1/...`) |
| http://localhost:8090 | Kafka UI: topics, events, consumer groups |
| http://localhost:8081 | Schema Registry |
| `localhost:5433` | `auth_db` (user `auth_user`) |
| `localhost:5434` | `profile_db` (`profile_user`) |
| `localhost:5435` | `fleet_db` (`fleet_user`) |
| `localhost:5436` | `shipment_db` (`shipment_user`) |
| `localhost:5437` | `tracking_db`, PostGIS (`tracking_user`) |
| `localhost:6380` | Redis live positions |

All dev database passwords are `letmein`. These are development settings only.

**Frontend hot reload** against the running stack:

```bash
cd mile-portal && npm install && npm run dev     # http://localhost:5173, API proxied to the gateway
```

**Load test** (M2M GPS gateway, many pings per second):

```bash
python3 tools/telemetry-simulator/simulator.py load --driver-id <driverId> --rate 1000 --seconds 30
```

## 6. Everyday commands

```bash
docker compose logs -f tracking-service          # follow one service's logs
docker compose up -d --build mile-portal         # rebuild one service after a change
docker compose restart nginx                     # restart the gateway
docker compose down                              # stop everything, keep the data
docker compose down -v                           # stop everything and DELETE all data (fresh start)
```

After `down -v`, the next `up` re-seeds the demo accounts and the vehicles `DEMO-001` / `DEMO-002`, but no vehicle is assigned: assign one to Jane (Fleet → Vehicles) before her first shift.

## 7. Troubleshooting

| Symptom | Likely cause / fix |
|---|---|
| `port is already allocated` | Another program uses that port (often 80 or a local PostgreSQL on 543x). Stop it, or change the host port in `docker-compose.yml`. |
| Portal says *"Cannot reach the server"* | The gateway isn't running: `docker compose ps nginx`, then `docker compose up -d nginx`. |
| A page says *"… is unavailable"* | That backend service is down or still starting. Check it with `docker compose ps` and `docker compose logs <service>`. The rest of the portal keeps working. |
| Grey map, *"The base map could not be loaded"* | No internet access to `tiles.openfreemap.org`. Live positions still work. |
| Map is empty, chip says *Live* | No driver on shift is pinging. Run the simulator (§4). |
| Simulator gets `409 Driver is not on shift` | Jane needs an active shipment, or **Start shift** (Fleet → Drivers, or her workspace). |
| Dispatch: *"No available driver…"* | Every driver is busy or off shift. Start Jane's shift, or wait for her current delivery to finish. |
| **Start shift** is greyed out | The driver has no vehicle. Assign one in Fleet → Vehicles. |
| A new account has no profile right after registering | Profiles are created from Kafka events. Wait a few seconds and reload. |

## Further reading

- [High-level architecture](high-level-architecture.md) · [Product requirements](product-requirements-document.md)
- Each service's design: [AuthService](AuthService/api-reference.md) · [ProfileService](ProfileService/ProfileService.md) · [FleetService](FleetService/FleetService.md) · [ShipmentService](ShipmentService/ShipmentService.md) · [FleetTrackingService](FleetTrackingService/FleetTrackingService.md) · [mile-portal](mile-portal/mile-portal.md)
- What changed and when: [changelog/](changelog/)
