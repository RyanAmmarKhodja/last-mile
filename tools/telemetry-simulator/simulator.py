#!/usr/bin/env python3
"""
Last-Mile telemetry simulator (FleetTrackingService, documents/FleetTrackingService/FleetTrackingService.md §10).
Standard library only.

  drive  Log in as a DRIVER and replay a route between two points, one ping every --interval seconds.
         The driver must be on shift (PUT /api/v1/fleet/drivers/me/availability {"isAvailable": true}).

           python3 simulator.py drive --email jane@doe.com --password Driver12345!

  load   Act as the M2M GPS gateway (client telemetry-simulator, scope tracking:ingest) and post batches for the
         given driver ids at --rate pings/s for --seconds. Prints the achieved rate and latency percentiles.

           python3 simulator.py load --driver-id <id> [--driver-id <id> ...] --rate 1000 --seconds 30
"""
import argparse
import json
import math
import random
import statistics
import sys
import threading
import time
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime, timezone

# Algiers: Place des Martyrs → Bab Ezzouar (about 15 km)
DEFAULT_FROM = (36.7856, 3.0603)
DEFAULT_TO = (36.7213, 3.1832)


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="milliseconds").replace("+00:00", "Z")


def request(method: str, url: str, body=None, token: str | None = None, form: bool = False):
    headers = {}
    data = None
    if body is not None:
        if form:
            data = urllib.parse.urlencode(body).encode()
            headers["Content-Type"] = "application/x-www-form-urlencoded"
        else:
            data = json.dumps(body).encode()
            headers["Content-Type"] = "application/json"
    if token:
        headers["Authorization"] = f"Bearer {token}"
    req = urllib.request.Request(url, data=data, method=method, headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=10) as resp:
            raw = resp.read()
            return resp.status, (json.loads(raw) if raw else None)
    except urllib.error.HTTPError as e:
        raw = e.read()
        try:
            return e.code, json.loads(raw)
        except ValueError:
            return e.code, raw.decode(errors="replace")


def login(base: str, email: str, password: str) -> str:
    status, body = request("POST", f"{base}/api/v1/auth/login", {"email": email, "password": password})
    if status != 200:
        sys.exit(f"login failed: {status} {body}")
    return body["accessToken"]


def m2m_token(base: str, client_id: str, secret: str) -> str:
    status, body = request("POST", f"{base}/api/v1/auth/m2m/token", {
        "grant_type": "client_credentials", "client_id": client_id, "client_secret": secret,
        "audience": "tracking-service", "scope": "tracking:ingest tracking:read"}, form=True)
    if status != 200:
        sys.exit(f"token request failed: {status} {body}")
    return body["access_token"] if "access_token" in body else body["accessToken"]


def heading(a, b) -> float:
    lat1, lat2 = math.radians(a[0]), math.radians(b[0])
    dlon = math.radians(b[1] - a[1])
    x = math.sin(dlon) * math.cos(lat2)
    y = math.cos(lat1) * math.sin(lat2) - math.sin(lat1) * math.cos(lat2) * math.cos(dlon)
    return (math.degrees(math.atan2(x, y)) + 360) % 360


def drive(args):
    token = login(args.base, args.email, args.password)
    start, end = tuple(args.start), tuple(args.end)
    hdg = round(heading(start, end), 1)
    steps = max(2, args.steps)
    print(f"driving {start} → {end} in {steps} pings every {args.interval}s (heading {hdg}°)")
    for i in range(steps + 1):
        f = i / steps
        lat = start[0] + (end[0] - start[0]) * f + random.uniform(-0.00005, 0.00005)
        lon = start[1] + (end[1] - start[1]) * f + random.uniform(-0.00005, 0.00005)
        ping = {"latitude": round(lat, 6), "longitude": round(lon, 6), "timestamp": now_iso(),
                "speedKmh": round(random.uniform(25, 45), 1), "headingDeg": hdg, "accuracyM": 8}
        status, body = request("POST", f"{args.base}/api/v1/tracking/telemetry", ping, token)
        print(f"[{i:>3}/{steps}] {ping['latitude']:.5f},{ping['longitude']:.5f} → {status} {body or ''}")
        if status == 401:  # access token expired (15 min): log in again
            token = login(args.base, args.email, args.password)
        if i < steps:
            time.sleep(args.interval)


def load(args):
    token = m2m_token(args.base, args.client_id, args.client_secret)
    drivers = args.driver_id
    batch_size = min(args.batch, 500)
    batches_per_second = max(1, math.ceil(args.rate / batch_size))
    per_batch = math.ceil(args.rate / batches_per_second)
    latencies, results, lock = [], {"accepted": 0, "ignored": 0, "rejected": 0, "errors": 0}, threading.Lock()
    last_ms = {}
    pos = {d: [36.75 + random.uniform(-0.05, 0.05), 3.05 + random.uniform(-0.05, 0.05)] for d in drivers}

    def send(pings):
        t0 = time.perf_counter()
        status, body = request("POST", f"{args.base}/api/v1/tracking/telemetry/batch", {"pings": pings}, token)
        dt = (time.perf_counter() - t0) * 1000
        with lock:
            latencies.append(dt)
            if status == 200:
                results["accepted"] += body["accepted"]
                results["ignored"] += body["ignored"]
                results["rejected"] += len(body["rejected"])
                if body["rejected"] and results["rejected"] <= len(body["rejected"]):
                    print("first rejection:", body["rejected"][0])
            else:
                results["errors"] += 1
                print("error", status, body)

    print(f"load: {args.rate} pings/s for {args.seconds}s over {len(drivers)} driver(s), "
          f"{batches_per_second} batch(es)/s of {per_batch}")
    started = time.perf_counter()
    threads = []
    for second in range(args.seconds):
        tick = started + second
        for _ in range(batches_per_second):
            pings = []
            for k in range(per_batch):
                d = drivers[k % len(drivers)]
                pos[d][0] += random.uniform(-0.0002, 0.0002)
                pos[d][1] += random.uniform(-0.0002, 0.0002)
                # strictly increasing millisecond timestamps per driver (the service keeps ms precision and
                # ignores a ping that is not newer than the live one)
                last_ms[d] = max(last_ms.get(d, 0) + 1, int(time.time() * 1000))
                ts = datetime.fromtimestamp(last_ms[d] / 1000, timezone.utc)
                pings.append({"driverId": d, "latitude": round(pos[d][0], 6), "longitude": round(pos[d][1], 6),
                              "timestamp": ts.isoformat(timespec="milliseconds").replace("+00:00", "Z"),
                              "speedKmh": 30})
            t = threading.Thread(target=send, args=(pings,))
            t.start()
            threads.append(t)
        time.sleep(max(0.0, tick + 1 - time.perf_counter()))
    for t in threads:
        t.join()
    elapsed = time.perf_counter() - started
    total = results["accepted"] + results["ignored"] + results["rejected"]
    q = statistics.quantiles(latencies, n=100) if len(latencies) >= 2 else latencies * 99
    print(f"sent {total} pings in {elapsed:.1f}s → {total / elapsed:.0f} pings/s")
    print(f"accepted {results['accepted']} · ignored {results['ignored']} · rejected {results['rejected']} · "
          f"HTTP errors {results['errors']}")
    print(f"batch latency ms: p50 {q[49]:.1f} · p95 {q[94]:.1f} · p99 {q[98]:.1f} · per ping p95 "
          f"{q[94] / per_batch:.3f}")


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--base", default="http://localhost", help="gateway base URL (default http://localhost)")
    sub = p.add_subparsers(dest="cmd", required=True)

    d = sub.add_parser("drive", help="replay a route as a logged-in driver")
    d.add_argument("--email", required=True)
    d.add_argument("--password", required=True)
    d.add_argument("--start", nargs=2, type=float, default=DEFAULT_FROM, metavar=("LAT", "LON"))
    d.add_argument("--end", nargs=2, type=float, default=DEFAULT_TO, metavar=("LAT", "LON"))
    d.add_argument("--steps", type=int, default=60)
    d.add_argument("--interval", type=float, default=2.0)
    d.set_defaults(func=drive)

    lo = sub.add_parser("load", help="M2M batch load test")
    lo.add_argument("--driver-id", action="append", required=True)
    lo.add_argument("--rate", type=int, default=1000)
    lo.add_argument("--seconds", type=int, default=30)
    lo.add_argument("--batch", type=int, default=250)
    lo.add_argument("--client-id", default="telemetry-simulator")
    lo.add_argument("--client-secret", default="dev-telemetry-secret-123")
    lo.set_defaults(func=load)

    args = p.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
