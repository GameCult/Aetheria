"""compare.py <committed ship.cc> <derived ship.cc>: exit 0 and COMPARE_EQUAL when the two packages hold the same shape, rows (id,
type, cell, footprint, rotation, armour, arc), stats, price and product (maker, name, description, offered), both read through
aetheria_ships/ship_cc.py. GLB bytes are not compared. Needs CULTLIB_PACKAGES."""
import importlib.util
import os
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("ship_cc", HERE.parent / "aetheria_ships" / "ship_cc.py")
sc = importlib.util.module_from_spec(spec)
spec.loader.exec_module(sc)
P = os.environ["CULTLIB_PACKAGES"]
sys.path.insert(0, str(Path(P) / "cultcache-py" / "src"))
import msgpack


def view(path):
    f = sc.read(path, P)
    shape, rows, _ = sc.read_layout(path, P)
    b = f.hull.body
    limit = [p for u, p in b[11] if u == 11][0][1]
    prods = [msgpack.unpackb(e.payload, raw=False) for e in f.store.pull_all() if e.type == sc.PRODUCT_SCHEMA]
    return {"shape": shape, "rows": [sc.decode_hardpoint(r) for r in rows], "name": b[1], "mass": b[4], "price": b[8],
            "durability": b[12], "armor": b[27], "drag": b[28], "top": (limit[0], limit[1]), "makers": sc.product_makers(path, P),
            "products": [(p[1], p[2], p[8]) for p in prods]}


a, b = view(sys.argv[1]), view(sys.argv[2])
bad = [k for k in a if a[k] != b[k]]
for k in bad:
    print("DIFF", k, *([] if k == "rows" else [a[k], b[k]]))
if "rows" in bad:
    for x, y in zip(a["rows"], b["rows"]):
        if x != y:
            print("DIFF row", x, y)
print("COMPARE_EQUAL" if not bad else "COMPARE_DIFFERENT", len(a["rows"]), "rows")
sys.exit(1 if bad else 0)
