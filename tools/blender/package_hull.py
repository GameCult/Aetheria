"""Headless hull packager: a bench hull and its manifest become one ship package accepted by the validator.

    blender --background --factory-startup --python-use-system-env --python tools/blender/package_hull.py -- <manifest.json> [--replace]

Environment: GAME_FOLDER (holds GameData/Aetheria.cc and ModTools/AetherDb.exe) and OPENSCAD. The repo (and so the
add-on) is found from this file. The manifest is creation input only: once GameData/Mods/<id> exists, ship.cc owns rows, stats and
product, and a manifest is never read again; --replace re-derives the package and discards edits made since. Without --replace an
existing package is refused untouched. A run is all or nothing: --replace moves the existing package aside, builds, and deletes the old one
only when the new one is complete; any failure removes what the run built and puts the old package back as it was.

Order, one owner per step: New Ship --like, the first Push and Apply create ship.cc, AetherDb stats/kind/price/product (the one writer of
each; kind only for a station manifest), hull.scad copied in, Open, Build From Hull Source, Rasterise Hull, the rows through place(), Push then Apply, Package Ship.
The last line is PACKAGE_OK (exit 0) or PACKAGE_FAILED (exit 1); a row that does not fit its cell is a failure, never a move.
Manifest schema and the reproducers: package_hull.md. Everything outside package() is pure Python (test_package_hull.py).
"""

import importlib.util
import json
import os
import shutil
import subprocess
import sys
import traceback
from contextlib import contextmanager
from pathlib import Path

HERE = Path(__file__).resolve().parent

MANIFEST_KEYS = ("id", "name", "like", "length", "extent", "hull", "maker", "description", "offered", "price", "stats", "rows")
MANIFEST_OPTIONAL = ("kind",)
STATS_KEYS = ("mass", "durability", "armor", "drag")
STATS_OPTIONAL = ("top_speed",)
ROW_KEYS = ("id", "kind", "cell", "footprint", "rotation", "armor", "arc")


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
    _keys("The manifest", manifest, MANIFEST_KEYS, MANIFEST_OPTIONAL)
    ship_cc = _ship_cc()
    if not isinstance(manifest["id"], str) or not ship_cc.valid_ship_id(manifest["id"]):
        raise Refused(f"id {manifest['id']!r} is not a valid ship id")
    for key in ("name", "like", "maker", "description", "hull"):
        _text(key, manifest[key])
    if "kind" in manifest:
        _text("kind", manifest["kind"])
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
    """Each row at its cell, or Refused naming the row: the manifest's cells are authored against the raster, so a cell off the
    raster, outside the shape or overlapping an earlier row is a fault in the manifest, not something to move."""
    taken, placed = set(), []
    for mount_id, kind, (x, y), footprint, rotation, armor, arc in rows:
        for cx, cy in cells_of(x, y, footprint):
            if not (0 <= cx < width and 0 <= cy < height):
                raise Refused(f"row {mount_id or kind} cell ({cx}, {cy}) is off the {width}x{height} raster")
            if not occupied(cx, cy):
                raise Refused(f"row {mount_id or kind} cell ({cx}, {cy}) is outside the hull shape")
            if (cx, cy) in taken:
                raise Refused(f"row {mount_id or kind} cell ({cx}, {cy}) overlaps an earlier row")
        taken.update(cells_of(x, y, footprint))
        placed.append((mount_id, kind, (x, y), footprint, rotation, armor, arc))
    return placed


@contextmanager
def swap(package_dir, backup, replace):
    """The all-or-nothing run. An existing package is refused untouched, or with replace moved to backup (same volume, a rename)
    before the build. On any failure what the run built is removed and the old package moved back; on success the backup goes."""
    existed = package_dir.exists()
    if existed and not replace:
        raise Refused(f"{package_dir} exists; pass --replace to re-derive it (this discards edits made since)")
    if backup.exists():
        raise Refused(f"{backup} is left from an interrupted run; restore or delete it first")
    if existed:
        backup.parent.mkdir(parents=True, exist_ok=True)
        shutil.move(str(package_dir), str(backup))
    try:
        yield
    except BaseException:
        shutil.rmtree(package_dir, ignore_errors=True)
        if existed:
            shutil.move(str(backup), str(package_dir))
        raise
    if existed:
        shutil.rmtree(backup)


def rows_of(manifest):
    return [(row["id"], row["kind"], tuple(row["cell"]), row["footprint"], row["rotation"], row["armor"], row["arc"])
            for row in manifest["rows"]]


def package(manifest, manifest_dir, game, openscad):
    """The Blender half, run inside swap(). Builds GameData/Mods/<id>; raises Refused with a message on any failure."""
    import addon_utils
    import bmesh
    import bpy

    ship_id, name = manifest["id"], manifest["name"]
    package_dir = game / "GameData" / "Mods" / ship_id
    ship_cc_path = package_dir / "ship.cc"
    aetherdb = game / "ModTools" / "AetherDb.exe"
    print(f"STEP Blender {bpy.app.version_string}", flush=True)

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

    def land(what):
        """Push (dry run), then Apply: her two buttons, in order, outside any agent turn."""
        result, message = run(bpy.ops.aetheria.push_ship)
        require(result == {"FINISHED"}, f"{what}: Push was refused: {message}")
        result, message = run(bpy.ops.aetheria.apply_ship)
        require(result == {"FINISHED"}, f"{what}: Apply was refused: {message}")

    def db(*args):
        result = subprocess.run([str(aetherdb), "ship-authoring", *map(str, args)], cwd=game, capture_output=True, text=True, timeout=120)
        print(f"AetherDb {args[0]}: exit {result.returncode} {result.stdout.strip()} {result.stderr.strip()}", flush=True)
        return result.returncode

    if bpy.data.objects.get("Cube") is not None:
        bpy.data.objects.remove(bpy.data.objects["Cube"], do_unlink=True)
    sys.path.insert(0, str(HERE))
    addon_utils.enable("aetheria_ships", default_set=True, handle_error=None)
    import aetheria_ships
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
    land("The first Apply")
    require(ship_cc_path.is_file(), "The first Apply did not create ship.cc")
    print(f"STEP new ship and first apply: {ship_cc_path}", flush=True)

    stats = manifest["stats"]
    args = ["stats", ship_cc_path, "--mass", stats["mass"], "--durability", stats["durability"], "--armor", stats["armor"],
            "--drag", stats["drag"]]
    if "top_speed" in stats:
        args += ["--top-speed", stats["top_speed"]]
    require(db(*args) == 0, "stats was refused")
    if "top_speed" in stats:
        require(aetheria_ships._open(bpy.context, str(ship_cc_path))["hull"]["topSpeed"] == stats["top_speed"],
                "stats did not set the top speed")
    if "kind" in manifest:
        # A station's kind is ship.cc's own fact and AetherDb its one writer; the packager holds no kind table.
        require(db("kind", ship_cc_path, manifest["kind"]) == 0, "kind was refused")
    require(db("price", ship_cc_path, manifest["price"]) == 0, "price was refused")
    product = ["product", ship_cc_path, manifest["maker"], "--name", name, "--description", manifest["description"]]
    require(db(*product, *([] if manifest["offered"] else ["--not-offered"])) == 0, "product was refused")
    print("STEP stats, price and product written", flush=True)

    shutil.copy(manifest_dir / manifest["hull"], package_dir / "hull.scad")
    select(box)
    result, message = run(bpy.ops.aetheria.open_ship)
    require(result == {"FINISHED"}, f"Open was refused: {message}")
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
    land("The layout")
    print(f"STEP layout applied: {len(state.hardpoints)} rows", flush=True)
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
    for key in ("GAME_FOLDER", "OPENSCAD"):
        if not os.environ.get(key):
            raise Refused(f"Set {key}")
        env[key] = os.environ[key]
    manifest_path = Path(paths[0]).resolve()
    manifest = load_manifest(manifest_path)
    game = Path(env["GAME_FOLDER"])
    with swap(game / "GameData" / "Mods" / manifest["id"], game / f".hull-packager-backup-{manifest['id']}", replace):
        package(manifest, manifest_path.parent, game, env["OPENSCAD"])


def entry(argv):
    """The exit status: 0 and PACKAGE_OK, or 1 and PACKAGE_FAILED. swap() has already put the game folder back."""
    try:
        main(argv)
    except Refused as stop:
        print(f"PACKAGE_FAILED: {stop}", flush=True)
        return 1
    except Exception:
        traceback.print_exc()
        print("PACKAGE_FAILED", flush=True)
        return 1
    print("PACKAGE_OK", flush=True)
    return 0


if __name__ == "__main__":
    code = entry(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    sys.stdout.flush()
    os._exit(code)
