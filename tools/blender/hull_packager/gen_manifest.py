"""gen_manifest.py <ship.cc> <hull.scad> <out.json> --like <hull name> --extent X Y Z --maker <faction name> [--length N]

A packager manifest (see ../package_hull.md) from an existing package's ship.cc, read through aetheria_ships/ship_cc.py: rows,
stats, price and product come from the .cc; the arguments are what a .cc does not record (the hull to copy, the model extent in
metres, the maker, the schematic length in cells). The id is the package folder's name. The hull.scad is copied beside the
manifest. Needs CULTLIB_PACKAGES.
"""
import argparse
import importlib.util
import json
import os
import shutil
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("ship_cc", HERE.parent / "aetheria_ships" / "ship_cc.py")
sc = importlib.util.module_from_spec(spec)
spec.loader.exec_module(sc)


def footprint(raw):
    w, h, cells = raw[0]
    return "/".join("".join("#" if cells[x * h + y] else "." for x in range(w)) for y in range(h - 1, -1, -1))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("ship_cc")
    parser.add_argument("hull_scad")
    parser.add_argument("out")
    parser.add_argument("--like", required=True)
    parser.add_argument("--extent", required=True, nargs=3, type=float)
    parser.add_argument("--maker", required=True)
    parser.add_argument("--length", type=int, default=30)
    args = parser.parse_args()
    packages = os.environ["CULTLIB_PACKAGES"]
    sys.path.insert(0, str(Path(packages) / "cultcache-py" / "src"))
    import msgpack
    f = sc.read(args.ship_cc, packages)
    _, rows, _ = sc.read_layout(args.ship_cc, packages)
    body = f.hull.body
    if sc.read_hull_type(args.ship_cc, packages) == "Station":
        sys.exit("gen_manifest reads a ship: a station has no top speed and its kind is not in the hull record. "
                 "Station manifests are written by hand (see manifests/arsenal-depot, package_hull.md).")
    limit = [p for u, p in body[11] if u == 11][0][1]
    assert limit[0] == limit[1], "the manifest holds one top speed"
    product = [msgpack.unpackb(e.payload, raw=False) for e in f.store.pull_all() if e.type == sc.PRODUCT_SCHEMA][0]
    manifest = {
        "id": Path(args.ship_cc).resolve().parent.name,
        "name": body[1], "like": args.like, "length": args.length, "extent": args.extent,
        "hull": Path(args.hull_scad).name, "maker": args.maker, "description": product[2], "offered": not product[8],
        "price": body[8],
        "stats": {"mass": body[4], "durability": body[12], "armor": body[27], "drag": round(body[28], 6), "top_speed": limit[0]},
        "rows": [{"id": d["Transform"], "kind": sc.HARDPOINT_TYPE_NAMES[d["Type"]], "cell": list(d["Position"]),
                  "footprint": footprint(d["Shape"]), "rotation": d["Rotation"], "armor": d["Armor"], "arc": d["FiringArc"]}
                 for d in map(sc.decode_hardpoint, rows)],
    }
    out = Path(args.out)
    out.write_text(json.dumps(manifest, indent=1), encoding="utf-8")
    shutil.copy(args.hull_scad, out.parent / Path(args.hull_scad).name)
    print("manifest", out, len(manifest["rows"]), "rows")


if __name__ == "__main__":
    main()
