"""Render documents/diagrams/data-schema.mmd (Mermaid classDiagram) to a UML-style PNG with matplotlib.
Parses namespaces / classes / members / relations from the .mmd so the PNG stays in sync with the source."""
import re, sys
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import FancyBboxPatch, Rectangle

src, out = sys.argv[1], sys.argv[2]
text = open(src).read()

# ---------- parse
classes, ns_of, ns_order, rels = {}, {}, [], []
cur_ns = cur_cls = None
for raw in text.splitlines():
    line = raw.strip()
    if not line or line.startswith("%%"):
        continue
    if m := re.match(r"namespace (\w+) \{", line):
        cur_ns = m[1]; ns_order.append(cur_ns); continue
    if m := re.match(r"class (\w+) \{", line):
        cur_cls = m[1]; classes[cur_cls] = {"stereo": "", "members": []}; ns_of[cur_cls] = cur_ns; continue
    if line == "}":
        if cur_cls: cur_cls = None
        else: cur_ns = None
        continue
    if cur_cls:
        if line.startswith("<<"):
            classes[cur_cls]["stereo"] = line
        else:
            classes[cur_cls]["members"].append(line)
        continue
    if m := re.match(r'(\w+)\s*(?:"([^"]+)")?\s*(\*--|<--|\.\.>)\s*(?:"([^"]+)")?\s*(\w+)\s*:\s*(.+)', line):
        rels.append(dict(a=m[1], ca=m[2], kind=m[3], cb=m[4], b=m[5], label=m[6].strip()))

# ---------- layout (namespace -> origin, columns of classes)
LAYOUT = {
    "AuthService_auth_db":                       ((0.4, 0.4), [["service_clients", "signing_keys", "revoked_tokens"], ["refresh_tokens", "users", "audit_logs"]]),
    "ProfileService_profile_db":                 ((9.8, 0.4), [["admins", "clients"]]),
    "FleetService_fleet_db":                     ((9.8, 8.4), [["drivers", "vehicles"]]),
    "ShipmentService_shipment_db":               ((14.9, 0.4), [["shipments", "shipment_status"]]),
    "FleetTrackingService_tracking_db_and_redis": ((14.9, 10.2), [["driver_live_position", "location_history"], ["tracked_drivers", "tracked_shipments"]]),
}
NS_STYLE = {  # fill, edge
    "AuthService_auth_db": ("#EEF2FF", "#4F46E5"),
    "ProfileService_profile_db": ("#ECFDF5", "#059669"),
    "FleetService_fleet_db": ("#FFF7ED", "#EA580C"),
    "ShipmentService_shipment_db": ("#FDF2F8", "#DB2777"),
    "FleetTrackingService_tracking_db_and_redis": ("#F0F9FF", "#0284C7"),
}
NS_TITLE = {
    "AuthService_auth_db": "AuthService\nauth_db  (PostgreSQL)",
    "ProfileService_profile_db": "ProfileService\nprofile_db  (PostgreSQL)",
    "FleetService_fleet_db": "FleetService\nfleet_db  (PostgreSQL)",
    "ShipmentService_shipment_db": "ShipmentService\nshipment_db  (PostgreSQL)",
    "FleetTrackingService_tracking_db_and_redis": "FleetTrackingService\nRedis + tracking_db  (PostgreSQL + PostGIS)",
}
W, HEAD, ROW, GAPX, GAPY, PAD, TOP = 3.7, 0.62, 0.30, 0.6, 0.55, 0.35, 1.15

box, ns_box = {}, {}
for ns, ((ox, oy), cols) in LAYOUT.items():
    maxy = 0
    for ci, col in enumerate(cols):
        y = oy + TOP
        for c in col:
            h = HEAD + ROW * len(classes[c]["members"]) + 0.15
            box[c] = (ox + PAD + ci * (W + GAPX), y, W, h)
            y += h + GAPY
        maxy = max(maxy, y - GAPY)
    ns_box[ns] = (ox, oy, PAD * 2 + len(cols) * W + (len(cols) - 1) * GAPX, maxy - oy + PAD)

missing = set(classes) - set(box)
assert not missing, f"classes without layout: {missing}"

total_w = max(x + w for x, y, w, h in ns_box.values()) + 0.4
total_h = max(y + h for x, y, w, h in ns_box.values()) + 1.9
S = 0.9
fig = plt.figure(figsize=(total_w * S, total_h * S), dpi=160)
ax = fig.add_axes([0, 0, 1, 1]); ax.set_xlim(0, total_w); ax.set_ylim(total_h, 0); ax.axis("off")
fig.patch.set_facecolor("white")

for ns, (x, y, w, h) in ns_box.items():
    fill, edge = NS_STYLE[ns]
    ax.add_patch(FancyBboxPatch((x, y), w, h, boxstyle="round,pad=0,rounding_size=0.25", fc=fill, ec=edge, lw=1.6, zorder=0))
    ax.text(x + 0.3, y + 0.6, NS_TITLE[ns], fontsize=12, linespacing=1.4, weight="bold", color=edge, va="center", zorder=1)

def clip(c, tx, ty):
    """point where the segment from box centre towards (tx,ty) exits the box"""
    x, y, w, h = box[c]; cx, cy = x + w / 2, y + h / 2
    dx, dy = tx - cx, ty - cy
    t = min(abs(w / 2 / dx) if dx else 1e9, abs(h / 2 / dy) if dy else 1e9)
    return cx + dx * t, cy + dy * t

for c, (x, y, w, h) in box.items():
    edge = NS_STYLE[ns_of[c]][1]
    proposed = "proposed" in classes[c]["stereo"]
    ax.add_patch(Rectangle((x, y), w, h, fc="white", ec=edge, lw=1.3, ls="--" if proposed else "-", zorder=3))
    ax.add_patch(Rectangle((x, y), w, HEAD, fc=edge, ec=edge, lw=1.3, zorder=3))
    stereo = classes[c]["stereo"].replace("<<", "«").replace(">>", "»")
    ax.text(x + w / 2, y + 0.19, stereo, fontsize=7.5, color="white", ha="center", va="center", style="italic", zorder=4)
    ax.text(x + w / 2, y + 0.43, c, fontsize=10.5, color="white", ha="center", va="center", weight="bold", zorder=4)
    for i, mbr in enumerate(classes[c]["members"]):
        parts = mbr.split()
        typ, rest = (parts[0], " ".join(parts[1:])) if len(parts) > 1 else ("", mbr)
        yy = y + HEAD + 0.08 + ROW * i + ROW / 2
        if mbr.startswith(("purge", "TTL")):
            ax.text(x + 0.12, yy, mbr, fontsize=7.8, color="#6B7280", style="italic", va="center", zorder=4); continue
        name, *flags = rest.split(" ")
        ax.text(x + 0.12, yy, name, fontsize=8.3, family="monospace", va="center", zorder=4,
                weight="bold" if "PK" in flags else "normal")
        ax.text(x + w - 0.12, yy, (" ".join(flags) + "  " if flags else "") + typ, fontsize=7.6,
                family="monospace", color="#6B7280", ha="right", va="center", zorder=4)

for r in rels:
    a, b = r["a"], r["b"]
    xa, ya, wa, ha = box[a]; xb, yb, wb, hb = box[b]
    pa = clip(a, xb + wb / 2, yb + hb / 2); pb = clip(b, xa + wa / 2, ya + ha / 2)
    logical = r["kind"] == "..>"
    col = "#9CA3AF" if logical else "#111827"
    style = "-|>" if logical else "-"
    ax.annotate("", xy=pb, xytext=pa, zorder=2,
                arrowprops=dict(arrowstyle=style if logical else "-", color=col, lw=1.3,
                                ls=(0, (5, 3)) if logical else "-", shrinkA=0, shrinkB=0, mutation_scale=14))
    if r["kind"] == "*--":   # composition diamond on the owner side
        ax.plot(*pa, marker="D", ms=8, color=col, zorder=5)
    mx, my = (pa[0] + pb[0]) / 2, (pa[1] + pb[1]) / 2
    if abs(pa[0] - pb[0]) < 0.05:  # short vertical link: label beside the line
        mx += 0.12 + 0.065 * len(r["label"])
    ax.text(mx, my, r["label"], fontsize=7.6, color="#374151" if not logical else "#6B7280", ha="center", va="center",
            family="monospace", zorder=6, bbox=dict(fc="white", ec="none", pad=1.2, alpha=0.9))
    for card, (px, py), (ox, oy) in ((r["ca"], pa, pb), (r["cb"], pb, pa)):
        if card:
            dx, dy = ox - px, oy - py; n = (dx * dx + dy * dy) ** 0.5 or 1
            ax.text(px + dx / n * 0.30 - dy / n * 0.22, py + dy / n * 0.30 + dx / n * 0.22, card, ha="center", va="center", fontsize=7.5, color=col, zorder=6)

ly = total_h - 1.35
ax.plot([0.6, 1.6], [ly, ly], color="#111827", lw=1.3); ax.plot(0.6, ly, marker="D", ms=7, color="#111827")
ax.text(1.8, ly, "foreign key inside one database (diamond = owning/composite side)", fontsize=9, va="center")
ax.annotate("", xy=(1.6, ly + 0.4), xytext=(0.6, ly + 0.4), arrowprops=dict(arrowstyle="-|>", color="#9CA3AF", lw=1.3, ls=(0, (5, 3)), mutation_scale=14))
ax.text(1.8, ly + 0.4, "logical reference by ID across services - NO database FK", fontsize=9, va="center")
ax.add_patch(Rectangle((0.6, ly + 0.65), 1.0, 0.3, fc="white", ec="#6B7280", ls="--"))
ax.text(1.8, ly + 0.8, "<<proposed>> - not confirmed yet (from HLA / PRD, see data-model.md section 5)", fontsize=9, va="center")
ax.text(total_w - 0.4, ly + 0.8, "source: documents/diagrams/data-schema.mmd  |  rules: documents/data-ownership-matrix.md",
        fontsize=8.5, color="#6B7280", va="center", ha="right")

fig.savefig(out, facecolor="white")
print("wrote", out, f"{len(classes)} classes, {len(rels)} relations")
