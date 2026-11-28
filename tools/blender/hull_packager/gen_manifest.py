"""gen_manifest.py <ship.cc> <hull.scad> <out.json> --like <hull name> --extent X Y Z --maker <faction name> [--length N]

A packager manifest (see ../package_hull.md) from an existing package's ship.cc, read through AetherDb's 'ship-authoring
open': rows, stats, price and product come from the .cc; the arguments are what a .cc does not record (the hull to copy, the
model extent in metres, the maker, the schematic length in cells). The id is the package folder's name. The hull.scad is
copied beside the manifest. Needs GAME_FOLDER, whose ModTools holds AetherDb.
"""
import argparse
import json
import shutil
import sys
from pathlib import Path

from compare import opened


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
    o = opened(args.ship_cc)
    hull = o["hull"]
    if o["hullType"] == "Station":
        sys.exit("gen_manifest reads a ship: a station has no top speed and its kind is not in the hull record. "
                 "Station manifests are written by hand (see manifests/arsenal-depot, package_hull.md).")
    if hull["topSpeed"] is None:
        sys.exit("the hull has no single fixed top speed; the manifest holds one")
    if len(o["products"]) != 1:
        sys.exit(f"the package holds {len(o['products'])} products; the manifest holds one")
    product = o["products"][0]
    manifest = {
        "id": Path(args.ship_cc).resolve().parent.name,
        "name": hull["name"], "like": args.like, "length": args.length, "extent": args.extent,
        "hull": Path(args.hull_scad).name, "maker": args.maker, "description": product["description"],
        "offered": product["offered"], "price": hull["price"],
        "stats": {"mass": hull["mass"], "durability": hull["durability"], "armor": hull["armor"],
                  "drag": round(hull["drag"], 6), "top_speed": hull["topSpeed"]},
        "rows": [{"id": row["mount"], "kind": row["kind"], "cell": row["cell"], "footprint": row["footprint"],
                  "rotation": row["rotation"], "armor": row["armor"], "arc": row["arc"]}
                 for row in o["layout"]["hardpoints"]],
    }
    out = Path(args.out)
    out.write_text(json.dumps(manifest, indent=1), encoding="utf-8")
    shutil.copy(args.hull_scad, out.parent / Path(args.hull_scad).name)
    print("manifest", out, len(manifest["rows"]), "rows")


if __name__ == "__main__":
    main()
