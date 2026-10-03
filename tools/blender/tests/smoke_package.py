"""Headless Blender smoke for the Package Ship action, end to end.

    blender --background --factory-startup --python-use-system-env --python tools/blender/tests/smoke_package.py

Environment: CULTLIB_PACKAGES (CultLib's packages directory, as test_ship_cc.py), and whatever AetherDb's build needs
(CULTLIB_ROOT and CULTMATH_ROOT at the pinned revisions), since the add-on runs 'dotnet run --project tools/AetherDb'.
SMOKE_MODS names the mods directory to write into (default: a new temp directory); the package is left there so the
Unity play smoke can load it, and its path is printed as SMOKE_PACKAGE=<ship.cc>.

Pass 1 packages a primitive ship built from role-tagged objects and requires the C# validator to accept it. Pass 2 swaps
the thruster disc for an empty of the same id and requires the validator to refuse it, naming the anchor. Pass 3 puts
the disc back and packages again, so the package left behind is the good one.

Before packaging, the bind cases: a bind that fails while computing the stored path leaves no aetheria.* property; an
unsaved .blend stores the absolute path, which still resolves after Save As to two other folders; a saved .blend on the
.cc's drive stores a '//'-relative path. SMOKE_OTHER_DRIVE names a directory on another drive than SMOKE_MODS; when set,
the .blend is saved there before the final bind, so the bind crosses drives and the package passes prove it resolves.
"""

import os
import sys
import tempfile
import traceback
from pathlib import Path

import addon_utils
import bpy

REPO = Path(__file__).resolve().parents[3]
PACKAGES = os.environ.get("CULTLIB_PACKAGES") or sys.exit("Set CULTLIB_PACKAGES to CultLib's packages directory")
SHIP_ID = "smoke.gale"


def tetrahedron(name):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([(0, 0, 0), (1, 0, 0), (0, 1, 0), (0, 0, 1)], [], [(0, 2, 1), (0, 1, 3), (0, 3, 2), (1, 2, 3)])
    return mesh


def quad(name, size=1.0):
    mesh = bpy.data.meshes.new(name)
    half = size / 2
    mesh.from_pydata([(-half, -half, 0), (half, -half, 0), (half, half, 0), (-half, half, 0)], [], [(0, 1, 2, 3)])
    return mesh


def tagged(collection, name, data, role=None, anchor_id=None, location=(0, 0, 0), parent=None):
    obj = bpy.data.objects.new(name, data)
    obj.location = location
    if role:
        obj["aetheria.role"] = role
        obj["aetheria.id"] = anchor_id
    obj.parent = parent
    collection.objects.link(obj)
    return obj


BIND_KEYS = ("aetheria.asset_kind", "aetheria.id", "aetheria.ship_cc")


def same_file(a, b):
    return os.path.normcase(os.path.normpath(a)) == os.path.normcase(os.path.normpath(b))


def bind(ship, label):
    for key in BIND_KEYS:
        if key in ship:
            del ship[key]
    try:
        result = bpy.ops.aetheria.bind_ship_collection()
    except RuntimeError:
        result = {"CANCELLED"}
    stored = ship.get("aetheria.ship_cc")
    print(f"BIND {label} {sorted(result)}: blend={bpy.data.filepath!r} stored={stored!r}")
    return result, stored


def bind_cases(ship, path, mods):
    if bpy.data.filepath:
        raise SystemExit("Bind cases need an unsaved .blend to start from")
    result, stored = bind(ship, "unsaved")
    if result != {"FINISHED"} or not os.path.isabs(stored) or not same_file(stored, path):
        raise SystemExit("Bind case unsaved: the stored path is not the absolute .cc path")
    for folder in ("blend-a", "blend-b"):
        (mods / folder).mkdir()
        bpy.ops.wm.save_as_mainfile(filepath=str(mods / folder / "ship.blend"))
        if not same_file(bpy.path.abspath(ship["aetheria.ship_cc"]), path):
            raise SystemExit(f"Bind case unsaved: the stored path stopped resolving after Save As to {folder}")

    relpath = bpy.path.relpath
    bpy.path.relpath = lambda *args, **kwargs: (_ for _ in ()).throw(OSError("probe: relpath failed"))
    try:
        result, _ = bind(ship, "failing")
    finally:
        bpy.path.relpath = relpath
    if result != {"CANCELLED"} or any(key in ship for key in BIND_KEYS):
        raise SystemExit("Bind case failing: a failed bind left aetheria.* properties behind")

    result, stored = bind(ship, "same-drive")
    if result != {"FINISHED"} or not stored.startswith("//") or not same_file(bpy.path.abspath(stored), path):
        raise SystemExit("Bind case same-drive: the stored path is not a resolving '//'-relative path")

    other = os.environ.get("SMOKE_OTHER_DRIVE")
    if not other:
        print("BIND cross-drive skipped: SMOKE_OTHER_DRIVE is not set")
        return
    if os.path.splitdrive(other)[0].lower() == os.path.splitdrive(path)[0].lower():
        raise SystemExit("SMOKE_OTHER_DRIVE is on the same drive as SMOKE_MODS")
    Path(other).mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(Path(other) / "ship.blend"))
    result, stored = bind(ship, "cross-drive")
    if result != {"FINISHED"} or not os.path.isabs(stored) or not same_file(stored, path):
        raise SystemExit("Bind case cross-drive: the stored path is not the absolute .cc path")


def package(addon):
    # An operator that reports an error raises it out of bpy.ops; the panel's report is the verdict either way.
    try:
        result = bpy.ops.aetheria.package_ship()
    except RuntimeError:
        result = {"CANCELLED"}
    report = bpy.context.scene.aetheria_package_report
    print(f"PACKAGE {sorted(result)}: {report}")
    return result, report


def main():
    sys.path.insert(0, str(REPO / "tools" / "blender"))
    addon_utils.enable("aetheria_ships", default_set=True, handle_error=None)
    import aetheria_ships
    # Brokkr is the Blender host's CultLib path provider and is not installed in a factory-startup Blender.
    aetheria_ships._brokkr_cultlib = lambda context: PACKAGES
    bpy.context.preferences.addons["aetheria_ships"].preferences.aetheria_repo = str(REPO)

    mods = Path(os.environ.get("SMOKE_MODS") or tempfile.mkdtemp(prefix="aetheria-smoke-mods-"))
    path = str(mods / SHIP_ID / "ship.cc")
    if Path(path).exists():
        raise SystemExit(f"{path} already exists; point SMOKE_MODS at an empty directory")
    code, message = aetheria_ships._aetherdb(bpy.context, path, "create", path, SHIP_ID, "Smoke Gale", "--like", "Djinni")
    print(f"CREATE {code}: {message}")
    if code != 0:
        raise SystemExit("create --like Djinni failed")

    scene = bpy.context.scene
    ship = bpy.data.collections.new("Smoke Gale")
    scene.collection.children.link(ship)
    hull = tagged(ship, "Hull", tetrahedron("Hull"))
    tagged(ship, "Map Icon", quad("Map Icon"), "map-icon", "map", (0, 0, -1))
    tagged(ship, "Hull Collider", tetrahedron("Hull Collider"), "hull-collider", "collider")
    tagged(ship, "Shield", None, "shield", "shield")
    tagged(ship, "Tractor", None, "tractor", "tractor", (0, 2, 0))
    disc = tagged(ship, "Thruster", quad("Thruster", 0.4), "thruster-emitter", "thruster", (0, -1, 0))
    gun = tagged(ship, "Gun", None, "weapon-mount", "gun", (1, 0, 0))
    tagged(ship, "Gun Muzzle", None, "weapon-muzzle", "gun.muzzle", (0, 0.5, 0), parent=gun)
    tagged(ship, "Radiator", quad("Radiator", 0.5), "radiator-mesh", "radiator", (-1, 0, 0))

    scene.aetheria_ship_cc_path = path
    bpy.context.view_layer.objects.active = hull
    bind_cases(ship, path, mods)

    ship_cc = aetheria_ships.ship_cc
    _, _, revision = ship_cc.read_layout(path, PACKAGES)

    def hardpoint(kind, x, mount):
        return ship_cc.encode_hardpoint(Type=ship_cc.HARDPOINT_TYPE_NAMES.index(kind), Position=[x, 0],
                                        Shape=[[1, 1, [True]]], Transform=mount, Rotation=0, Armor=0.0, FiringArc=0.0)

    ship_cc.replace_layout(path, PACKAGES, SHIP_ID, revision, [4, 1, [True] * 4], [
        hardpoint("Thruster", 0, "thruster"), hardpoint("Radiator", 1, "radiator"),
        hardpoint("Energy", 2, "gun"), hardpoint("Reactor", 3, "reactor")])

    result, report = package(aetheria_ships)
    if result != {"FINISHED"}:
        raise SystemExit("Pass 1: the validator refused the primitive ship")

    ship.objects.unlink(disc)
    hollow = tagged(ship, "Thruster Empty", None, "thruster-emitter", "thruster", (0, -1, 0))
    result, report = package(aetheria_ships)
    if result != {"CANCELLED"} or "thruster-emitter anchor thruster needs a mesh" not in report:
        raise SystemExit("Pass 2: an empty thruster node was not refused by name")

    ship.objects.unlink(hollow)
    ship.objects.link(disc)
    result, report = package(aetheria_ships)
    if result != {"FINISHED"}:
        raise SystemExit("Pass 3: the restored ship was refused")
    print(f"SMOKE_PACKAGE={path}")
    print("SMOKE_PACKAGE_OK")


try:
    main()
except SystemExit as stop:
    print(f"SMOKE_PACKAGE_FAILED: {stop}", flush=True)
    os._exit(1)
except Exception:
    traceback.print_exc()
    print("SMOKE_PACKAGE_FAILED", flush=True)
    os._exit(1)
sys.stdout.flush()
os._exit(0)
