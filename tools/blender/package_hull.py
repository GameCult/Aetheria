"""Headless hull packager: a bench hull and its manifest become one ship package accepted by the validator.

    blender --background --factory-startup --python-use-system-env --python tools/blender/package_hull.py -- <manifest.json> [--replace]

Environment: GAME_FOLDER (holds GameData/Aetheria.cc and ModTools/AetherDb.exe), CULTLIB_PACKAGES, OPENSCAD. The repo (and so the
add-on) is found from this file. The manifest is creation input only: once GameData/Mods/<id> exists, ship.cc owns rows, stats and
product, and a manifest is never read again; --replace re-derives the package and discards edits made since. Without --replace an
existing package is refused untouched.

Order, one owner per step: New Ship --like, the first Save Layout creates ship.cc, AetherDb stats/price/product (the one writer of
each), hull.scad copied in, Load Layout, Build From Hull Source, Rasterise Hull, the rows through place(), Save Layout, Package Ship.
The last line is PACKAGE_OK or PACKAGE_FAILED. Everything outside package() is pure Python (test_package_hull.py).
"""

import importlib.util
import json
import os
import shutil
import subprocess
import sys
import traceback
from pathlib import Path

HERE = Path(__file__).resolve().parent

MANIFEST_KEYS = ("id", "name", "like", "length", "extent", "hull", "maker", "description", "offered", "price", "stats", "rows")
STATS_KEYS = ("mass", "durability", "armor", "drag")
STATS_OPTIONAL = ("top_speed",)
ROW_KEYS = ("id", "kind", "cell", "footprint", "rotation", "armor", "arc")

created = []  # package folders this run made; removed on failure, so a refused run leaves an existing package alone


class Refused(Exception):
    pass


def _ship_cc():
    """ship_cc.py by path: the add-on's package __init__ imports bpy, and the hardpoint kind names live in ship_cc."""
    spec = importlib.util.spec_from_file_location("ship_cc", HERE / "aetheria_ships" / "ship_cc.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def _keys(where, mapping, required, optional=()):
    if not isinstance(mapping, dict):
        raise Refused(f"{where} is not an object")
    unknown = sorted(set(mapping) - set(required) - set(optional))
    missing = [key for key in required if key not in mapping]
    if unknown:
        raise Refused(f"{where} has unknown key(s): {', '.join(unknown)}")
    if missing:
        raise Refused(f"{where} is missing key(s): {', '.join(missing)}")


def _number(where, value, minimum=None):
    if isinstance(value, bool) or not isinstance(value, (int, float)) or (minimum is not None and value < minimum):
        raise Refused(f"{where} must be a number" + ("" if minimum is None else f" of at least {minimum}"))
    return value


def _text(where, value):
    if not isinstance(value, str) or not value:
        raise Refused(f"{where} must be a non-empty string")
    return value


def valid_footprint(footprint):
    """Top-to-bottom rows of # and . separated by /, all one width, at least one #."""
    rows = footprint.split("/") if isinstance(footprint, str) else []
    return (bool(rows) and len(rows[0]) > 0 and all(len(row) == len(rows[0]) and set(row) <= {"#", "."} for row in rows)
            and any("#" in row for row in rows))


def load_manifest(path):
    """The manifest as a dict, or Refused naming the first fault. Nothing here touches Blender or the game folder."""
    path = Path(path)
    try:
        manifest = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError) as error:
        raise Refused(f"The manifest cannot be read: {error}") from None
    _keys("The manifest", manifest, MANIFEST_KEYS)
    ship_cc = _ship_cc()
    if not isinstance(manifest["id"], str) or not ship_cc.valid_ship_id(manifest["id"]):
        raise Refused(f"id {manifest['id']!r} is not a valid ship id")
    for key in ("name", "like", "maker", "description", "hull"):
        _text(key, manifest[key])
    if isinstance(manifest["length"], bool) or not isinstance(manifest["length"], int) or manifest["length"] < 1:
        raise Refused("length must be a whole number of cells of at least 1")
    extent = manifest["extent"]
    if not isinstance(extent, list) or len(extent) != 3:
        raise Refused("extent must be [x, y, z] in metres")
    for axis, value in zip("xyz", extent):
        _number(f"extent {axis}", value, 1e-6)
    if not isinstance(manifest["offered"], bool):
        raise Refused("offered must be true or false")
    _number("price", manifest["price"], 0)
    stats = manifest["stats"]
    _keys("stats", stats, STATS_KEYS, STATS_OPTIONAL)
    for key, value in stats.items():
        _number(f"stats {key}", value, 0)
    if not (path.parent / manifest["hull"]).is_file():
        raise Refused(f"hull {manifest['hull']!r} is not a file beside the manifest")
    if not isinstance(manifest["rows"], list) or not manifest["rows"]:
        raise Refused("rows must be a non-empty list")
    seen = set()
    for index, row in enumerate(manifest["rows"]):
        _keys(f"row {index}", row, ROW_KEYS)
        _text(f"row {index} id", row["id"])
        where = f"row {row['id']!r}"
        if row["id"] in seen:
            raise Refused(f"{where} is a duplicate row id")
        seen.add(row["id"])
        if row["kind"] not in ship_cc.HARDPOINT_TYPE_NAMES:
            raise Refused(f"{where} has unknown kind {row['kind']!r}")
        cell = row["cell"]
        if (not isinstance(cell, list) or len(cell) != 2
                or any(isinstance(v, bool) or not isinstance(v, int) or v < 0 for v in cell)):
            raise Refused(f"{where} cell must be [x, y] whole numbers of at least 0")
        if not valid_footprint(row["footprint"]):
            raise Refused(f"{where} has a malformed footprint {row['footprint']!r}")
        if isinstance(row["rotation"], bool) or row["rotation"] not in range(len(ship_cc.ROTATION_NAMES)):
            raise Refused(f"{where} rotation must be 0 to {len(ship_cc.ROTATION_NAMES) - 1}")
        _number(f"{where} armor", row["armor"], 0)
        if _number(f"{where} arc", row["arc"], 0) > 360:
            raise Refused(f"{where} arc is over 360")
    return manifest


def cells_of(x, y, footprint):
    rows = footprint.split("/")
    height = len(rows)
    return [(x + fx, y + fy) for fy in range(height) for fx in range(len(rows[0])) if rows[height - 1 - fy][fx] == "#"]


def place(rows, width, height, occupied):
    """Each row at its cell, or at the nearest cell where its whole footprint is inside the shape and clear of earlier rows."""
    taken, placed = set(), []
    for mount_id, kind, (x, y), footprint, rotation, armor, arc in rows:
        def fits(px, py):
            return all(0 <= cx < width and 0 <= cy < height and occupied(cx, cy) and (cx, cy) not in taken
                       for cx, cy in cells_of(px, py, footprint))
        if fits(x, y):
            at = (x, y)
        else:
            candidates = sorted(((px - x) ** 2 + (py - y) ** 2, px, py) for px in range(width) for py in range(height) if fits(px, py))
            if not candidates:
                raise Refused(f"No cell fits row {mount_id or kind}")
            at = candidates[0][1:]
            print(f"DEVIATION row {mount_id or 'kind ' + kind} moved from {(x, y)} to {at}: outside the shape or overlapping", flush=True)
        taken.update(cells_of(*at, footprint))
        placed.append((mount_id, kind, at, footprint, rotation, armor, arc))
    return placed


def rows_of(manifest):
    return [(row["id"], row["kind"], tuple(row["cell"]), row["footprint"], row["rotation"], row["armor"], row["arc"])
            for row in manifest["rows"]]


def package(manifest, manifest_dir, game, packages, openscad, replace):
    """The Blender half. Leaves GameData/Mods/<id> in place; raises Refused with a message on any failure."""
    import addon_utils
    import bmesh
    import bpy
    import msgpack

    ship_id, name = manifest["id"], manifest["name"]
    package_dir = game / "GameData" / "Mods" / ship_id
    ship_cc_path = package_dir / "ship.cc"
    aetherdb = game / "ModTools" / "AetherDb.exe"
    if package_dir.exists() and not replace:
        raise Refused(f"{package_dir} exists; pass --replace to re-derive it (this discards edits made since)")
    print(f"STEP Blender {bpy.app.version_string}", flush=True)
    if package_dir.exists():
        shutil.rmtree(package_dir)
    created.append(package_dir)

    def require(condition, message):
        if not condition:
            raise Refused(message)

    def select(obj):
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)

    def run(operator):
        try:
            return operator(), ""
        except RuntimeError as exc:
            return {"CANCELLED"}, str(exc)

    def db(*args):
        result = subprocess.run([str(aetherdb), "ship-authoring", *map(str, args)], cwd=game, capture_output=True, text=True, timeout=120)
        print(f"AetherDb {args[0]}: exit {result.returncode} {result.stdout.strip()} {result.stderr.strip()}", flush=True)
        return result.returncode

    def top_speed():
        """The VelocityLimit TopSpeed (Min, Max) in the new ship.cc's hull record (union key 11, VelocityLimitData)."""
        sys.path.insert(0, str(Path(packages) / "cultcache-py" / "src"))
        import cultcache_py
        hulls = [e for e in cultcache_py.SingleFileMessagePackBackingStore(str(ship_cc_path)).pull_all() if e.type == "aetheria.hulldata"]
        require(len(hulls) == 1, "ship.cc holds no single hull record")
        body = msgpack.unpackb(hulls[0].payload, raw=False)
        limits = [payload for union, payload in body[11] if union == 11]
        require(len(limits) == 1, f"the copied hull has {len(limits)} VelocityLimits")
        return limits[0][1][0], limits[0][1][1]

    if bpy.data.objects.get("Cube") is not None:
        bpy.data.objects.remove(bpy.data.objects["Cube"], do_unlink=True)
    sys.path.insert(0, str(HERE))
    addon_utils.enable("aetheria_ships", default_set=True, handle_error=None)
    import aetheria_ships
    aetheria_ships._brokkr_cultlib = lambda context: packages
    preferences = bpy.context.preferences.addons["aetheria_ships"].preferences
    preferences.game_folder = str(game)
    preferences.openscad = openscad
    scene = bpy.context.scene
    kind_value = {kind: value for value, kind, _ in aetheria_ships.HARDPOINT_TYPES}

    collection = bpy.data.collections.new(name)
    scene.collection.children.link(collection)
    mesh = bpy.data.meshes.new("stand-in")
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bm.to_mesh(mesh)
    bm.free()
    box = bpy.data.objects.new("stand-in", mesh)
    box.scale = tuple(manifest["extent"])
    collection.objects.link(box)
    select(box)
    scene.aetheria_new_ship_id, scene.aetheria_new_ship_name = ship_id, name
    scene.aetheria_new_ship_like, scene.aetheria_new_ship_length = manifest["like"], manifest["length"]
    result, message = run(bpy.ops.aetheria.new_ship)
    require(result == {"FINISHED"}, f"New Ship was refused: {message}")
    result, message = run(bpy.ops.aetheria.save_ship_layout)
    require(result == {"FINISHED"} and ship_cc_path.is_file(), f"The first Save was refused: {message}")
    print(f"STEP new ship and first save: {ship_cc_path}", flush=True)

    stats = manifest["stats"]
    args = ["stats", ship_cc_path, "--mass", stats["mass"], "--durability", stats["durability"], "--armor", stats["armor"],
            "--drag", stats["drag"]]
    if "top_speed" in stats:
        args += ["--top-speed", stats["top_speed"]]
    require(db(*args) == 0, "stats was refused")
    if "top_speed" in stats:
        require(top_speed() == (stats["top_speed"], stats["top_speed"]), "stats did not set the top speed")
    require(db("price", ship_cc_path, manifest["price"]) == 0, "price was refused")
    product = ["product", ship_cc_path, manifest["maker"], "--name", name, "--description", manifest["description"]]
    require(db(*product, *([] if manifest["offered"] else ["--not-offered"])) == 0, "product was refused")
    print("STEP stats, price and product written", flush=True)

    shutil.copy(manifest_dir / manifest["hull"], package_dir / "hull.scad")
    select(box)
    result, message = run(bpy.ops.aetheria.load_ship_layout)
    require(result == {"FINISHED"}, f"Load Layout was refused: {message}")
    select(box)
    result, message = run(bpy.ops.aetheria.build_hull_source)
    require(result == {"FINISHED"}, f"Build was refused: {message}")
    built = collection.get("aetheria.hull_built")
    objects = [obj for obj in collection.all_objects if built is not None and obj.name in built]
    zones = sorted(obj.name for obj in objects if obj.type == "MESH" and not obj.get("aetheria.role"))
    anchors = sorted(obj.get("aetheria.id") for obj in objects if obj.get("aetheria.role") in
                     ("weapon-mount", "weapon-muzzle", "thruster-emitter", "radiator-mesh"))
    print(f"STEP Build: {len(zones)} zones {zones}; {len(anchors)} anchors {anchors}", flush=True)

    select(box)
    result, message = run(bpy.ops.aetheria.rasterise_hull)
    require(result == {"FINISHED"}, f"Rasterise was refused: {message}")
    state = scene.aetheria_layout
    width, height = state.width, state.height
    print(f"STEP Rasterise: {width}x{height}", flush=True)
    for y in range(height - 1, -1, -1):
        print("GRID " + "".join("#" if state.cells[x * height + y].occupied else "." for x in range(width)), flush=True)

    occupied = lambda x, y: state.cells[x * height + y].occupied
    for mount_id, kind, (x, y), footprint, rotation, armor, arc in place(rows_of(manifest), width, height, occupied):
        bpy.ops.aetheria.add_ship_hardpoint()
        hp = state.hardpoints[len(state.hardpoints) - 1]
        hp.mount_id, hp.kind, hp.x, hp.y, hp.footprint = mount_id, kind_value[kind], x, y, footprint
        hp.rotation, hp.armor, hp.firing_arc = str(rotation), armor, arc
    select(box)
    result, message = run(bpy.ops.aetheria.save_ship_layout)
    require(result == {"FINISHED"}, f"The layout Save was refused: {message}")
    print(f"STEP layout saved: {len(state.hardpoints)} rows", flush=True)
    for hp in state.hardpoints:
        print(f"ROW {hp.mount_id or '-'} kind {hp.kind} at ({hp.x},{hp.y}) {hp.footprint} rot {hp.rotation} armor {hp.armor:g} arc {hp.firing_arc:g}", flush=True)

    select(box)
    result, message = run(bpy.ops.aetheria.package_ship)
    report = scene.aetheria_package_report
    require(result == {"FINISHED"} and "validator accepted" in report, f"Package refused the ship: {report!r} {message!r}")
    require((package_dir / "ship.glb").is_file() and (package_dir / "hull.scad").is_file(), "Package did not leave ship.glb and hull.scad")
    print(f"STEP Package: {report}", flush=True)


def main(argv):
    replace = "--replace" in argv
    paths = [a for a in argv if a != "--replace"]
    if len(paths) != 1:
        raise Refused("usage: package_hull.py -- <manifest.json> [--replace]")
    env = {}
    for key in ("GAME_FOLDER", "CULTLIB_PACKAGES", "OPENSCAD"):
        if not os.environ.get(key):
            raise Refused(f"Set {key}")
        env[key] = os.environ[key]
    manifest_path = Path(paths[0]).resolve()
    manifest = load_manifest(manifest_path)
    package(manifest, manifest_path.parent, Path(env["GAME_FOLDER"]), env["CULTLIB_PACKAGES"], env["OPENSCAD"], replace)


if __name__ == "__main__":
    try:
        main(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    except Refused as stop:
        print(f"PACKAGE_FAILED: {stop}", flush=True)
        code = 1
    except Exception:
        traceback.print_exc()
        print("PACKAGE_FAILED", flush=True)
        code = 1
    else:
        print("PACKAGE_OK", flush=True)
        code = 0
    if code:
        for folder in created:
            shutil.rmtree(folder, ignore_errors=True)
    sys.stdout.flush()
    os._exit(code)
