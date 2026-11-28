"""compare.py <committed ship.cc> <derived ship.cc>: exit 0 and COMPARE_EQUAL when the two packages hold the same grid, hardpoint
rows (mount, kind, cell, footprint, rotation, armour, arc, in order), hull section (name, price, stats, top speed, kind) and
products (maker, name, description, offered), both read through AetherDb's 'ship-authoring open'. GLB bytes are not compared.
Needs GAME_FOLDER, whose ModTools holds AetherDb."""
import json
import os
import subprocess
import sys
from pathlib import Path


def opened(path):
    """'ship-authoring open' of a ship.cc as a dict."""
    game = Path(os.environ["GAME_FOLDER"])
    tool = game / "ModTools" / ("AetherDb.exe" if sys.platform == "win32" else "AetherDb")
    run = subprocess.run([str(tool), "ship-authoring", "open", str(path)], cwd=game, capture_output=True, text=True, timeout=120)
    if run.returncode != 0:
        sys.exit(f"open refused {path}: {run.stdout.strip()} {run.stderr.strip()}")
    return json.loads(run.stdout)


def view(path):
    o = opened(path)
    return {"grid": o["layout"]["grid"],
            "rows": [{k: v for k, v in row.items() if k != "element"} for row in o["layout"]["hardpoints"]],
            "hull": o["hull"], "products": o["products"]}


if __name__ == "__main__":
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
