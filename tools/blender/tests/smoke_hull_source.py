"""Headless Blender smoke for Build From Hull Source, with the real OpenSCAD and the real faction language files.

    blender --background --factory-startup --python-use-system-env --python tools/blender/tests/smoke_hull_source.py

Environment: CULTLIB_PACKAGES and SMOKE_GAME_FOLDER as smoke_package.py's header says (the published AetherDb under
ModTools, GameData/Aetheria.cc), msgpack importable, and OpenSCAD 2026.09 or later at SMOKE_OPENSCAD (default: the add-on
preference's default). SMOKE_PID_LOG, when set, receives one line per OpenSCAD process. Each ship is created under
<game folder>/GameData/Mods/smoke.hull.*, which this smoke removes when it ends.

For each of the two fixture hulls (tests/fixtures/hulls: the Zhestokost ammunition tender and the Pirate fighter on a
Rossum & Douglas wedge) the smoke makes a ship from a stand-in box (New Ship), authors its gameplay layout separately
(rows named for the hull source's anchors, at cells that have nothing to do with where the model puts its gear), copies
the hull.scad into the package and runs the panel's operators:

  Build: zone meshes with one palette material each, the mount anchors, the emitter and radiator meshes and a collider,
  all marked aetheria.hull_source under Ship Root, and no layout row changed by Build itself.
  A second Build replaces only its own objects: an object the author made, a mesh and an empty, is still there.
  Build refuses, naming a rule and no input, a hull source that breaks its language's or brief's rule, and changes nothing.
  Build refuses a collection that holds a render mesh it did not make, and changes nothing.
  Package accepts the built ship (the C# validator), writes ship.glb with a node per anchor, and leaves hull.scad.
  Once the author has taken the generated objects over (the mark is gone) Package deletes hull.scad and says so.
  A hull that was never built keeps its hull.scad through the same deletion rule.
"""

import json
import os
import shutil
import struct
import sys
import traceback
from pathlib import Path

import addon_utils
import bmesh
import bpy

REPO = Path(__file__).resolve().parents[3]
PACKAGES = os.environ.get("CULTLIB_PACKAGES") or sys.exit("Set CULTLIB_PACKAGES to CultLib's packages directory")
GAME = Path(os.environ.get("SMOKE_GAME_FOLDER") or sys.exit("Set SMOKE_GAME_FOLDER to a folder holding GameData/Aetheria.cc and ModTools/AetherDb"))
OPENSCAD = os.environ.get("SMOKE_OPENSCAD")
PID_LOG = os.environ.get("SMOKE_PID_LOG")
MODS = GAME / "GameData" / "Mods"
FIXTURES = REPO / "tools" / "blender" / "tests" / "fixtures" / "hulls"
THRUSTER, RADIATOR, BALLISTIC = "3", "6", "10"

# fixture folder, ship id, stand-in box (x, y) metres and its length in cells, the layout rows (mount id, kind, cell),
# the body zone the collider copies, and the palette each zone must carry
HULLS = (
    ("zh-ammo-tender", "smoke.hull.tender", (10.0, 23.4), 12,
     (("Gun.D", BALLISTIC, (2, 3)), ("Th.L", THRUSTER, (0, 0)), ("Th.R", THRUSTER, (1, 0)),
      ("Ra.L", RADIATOR, (2, 0)), ("Ra.R", RADIATOR, (3, 0))),
     "primary", {"primary": (0.42, 0.43, 0.45), "secondary": (0.20, 0.20, 0.22), "accent": (0.55, 0.22, 0.14), "recess": (0.08, 0.08, 0.09)}),
    ("pi-fighter", "smoke.hull.fighter", (8.0, 15.0), 8,
     (("Gun.P", BALLISTIC, (3, 3)), ("Th.Main", THRUSTER, (0, 0)), ("Th.L", THRUSTER, (1, 0))),
     "rnd.primary", {"rnd.primary": (0.72, 0.74, 0.76), "primer": (0.48, 0.27, 0.22), "weld": (0.70, 0.68, 0.62),
      "accent": (0.10, 0.62, 0.60), "rnd.accent": (0.90, 0.89, 0.84)}),
)


def require(condition, message):
    if not condition:
        raise SystemExit(message)


def select(obj):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)


def run(operator):
    try:
        return operator(), ""
    except RuntimeError as exc:
        return {"CANCELLED"}, str(exc)


def approx(a, b, tolerance=1e-3):
    return all(abs(x - y) <= tolerance for x, y in zip(a, b))


def generated(collection):
    return {obj.name: obj for obj in collection.all_objects if obj.get("aetheria.hull_source")}


def layout_rows(scene):
    return [(hp.mount_id, hp.kind, hp.armor, hp.firing_arc, hp.footprint) for hp in scene.aetheria_layout.hardpoints]


def glb_nodes(path):
    data = Path(path).read_bytes()
    length, kind = struct.unpack_from("<II", data, 12)
    require(kind == 0x4E4F534A, "The GLB's first chunk is not JSON")
    return json.loads(data[20:20 + length])["nodes"]


def make_ship(scene, fixture, ship_id, extent, length, rows):
    """A ship from a stand-in box, its layout authored by hand, hull.scad copied into its package; returns the collection."""
    collection = bpy.data.collections.new(f"Smoke {ship_id}")
    scene.collection.children.link(collection)
    mesh = bpy.data.meshes.new(f"{ship_id} stand-in")
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bm.to_mesh(mesh)
    bm.free()
    box = bpy.data.objects.new(f"{ship_id} stand-in", mesh)
    box.scale = (extent[0], extent[1], 2.0)
    collection.objects.link(box)
    select(box)
    scene.aetheria_new_ship_id, scene.aetheria_new_ship_name = ship_id, ship_id
    scene.aetheria_new_ship_like, scene.aetheria_new_ship_length = "Djinni", length
    result, message = run(bpy.ops.aetheria.new_ship)
    require(result == {"FINISHED"}, f"New Ship was refused: {message}")
    state = scene.aetheria_layout
    for mount_id, kind, (x, y) in rows:
        bpy.ops.aetheria.add_ship_hardpoint()
        hp = state.hardpoints[len(state.hardpoints) - 1]
        hp.mount_id, hp.kind, hp.x, hp.y, hp.footprint = mount_id, kind, x, y, "#"
    result, message = run(bpy.ops.aetheria.save_ship_layout)
    require(result == {"FINISHED"}, f"Save was refused: {message}")
    package = MODS / ship_id
    shutil.copy(FIXTURES / fixture / "hull.scad", package / "hull.scad")
    select(box)
    return collection, box, package


def check_built(collection, root, body, palette, anchors_by_role):
    objects = generated(collection)
    for zone, colour in palette.items():
        obj = objects.get(zone)
        require(obj is not None and obj.type == "MESH" and obj.parent == root and len(obj.data.polygons) > 0,
                f"Zone {zone} is not a mesh under Ship Root")
        material = obj.data.materials[0]
        base = next(n for n in material.node_tree.nodes if n.type == "BSDF_PRINCIPLED").inputs["Base Color"].default_value
        require(approx(tuple(base)[:3], colour), f"Zone {zone} is not its palette colour: {tuple(base)}")
        require(not any(p.use_smooth for p in obj.data.polygons), f"Zone {zone} is not flat shaded")
    for role, ids in anchors_by_role.items():
        for anchor_id in ids:
            obj = objects.get(anchor_id)
            require(obj is not None and obj.get("aetheria.role") == role and obj.get("aetheria.id") == anchor_id and obj.parent is not None,
                    f"Anchor {anchor_id} ({role}) was not built")
    colliders = [o for o in objects.values() if o.get("aetheria.role") == "hull-collider"]
    require(len(colliders) == 1 and colliders[0].type == "MESH", "Build did not make exactly one collider mesh")
    shape = lambda obj: sorted(tuple(round(c, 4) for c in v.co) for v in obj.data.vertices)
    require(shape(colliders[0]) == shape(objects[body]), "The collider is not the body zone's mesh")
    return objects


def one_hull(scene, number, fixture, ship_id, extent, length, rows, body, palette):
    collection, box, package = make_ship(scene, fixture, ship_id, extent, length, rows)
    state = scene.aetheria_layout
    root = bpy.data.objects[next(o.name for o in collection.all_objects if o.get("aetheria.ship_root"))]
    rows_before = layout_rows(scene)
    source = package / "hull.scad"
    require(source.is_file(), "The fixture hull.scad was not copied")
    require("Build From Hull Source" == bpy.ops.aetheria.build_hull_source.get_rna_type().name, "Build is not registered")

    # Build, and a rule that fails before anything changes.
    result, message = run(bpy.ops.aetheria.build_hull_source)
    require(result == {"FINISHED"}, f"Build was refused: {message}")
    roles = {}
    for obj in generated(collection).values():
        role = obj.get("aetheria.role")
        if role in ("weapon-mount", "thruster-emitter", "radiator-mesh", "weapon-muzzle"):
            roles.setdefault(role, []).append(obj.name)
    require(roles.get("weapon-mount") and roles.get("weapon-muzzle") and roles.get("thruster-emitter"), f"Anchors missing: {roles}")
    first = check_built(collection, root, body, palette, roles)
    for muzzle in roles["weapon-muzzle"]:
        mount = first[muzzle].parent
        require(mount.get("aetheria.role") == "weapon-mount" and first[muzzle].get("aetheria.order") == 1, "A muzzle is not ordered under its mount")
        require(first[muzzle].matrix_world.translation.y < mount.matrix_world.translation.y, "A muzzle is not nose-ward of its mount")
    require(layout_rows(scene) == rows_before, "Build changed the layout buffer")
    print(f"PASS {number}.1 {fixture} builds: {len(first)} objects, {sum(len(v) for v in roles.values())} anchors")

    # A second Build replaces only its own objects; the author's own empty and mesh (outside render meshes' reach: a mesh
    # is tested separately) stay.
    keep = bpy.data.objects.new("author note", None)
    collection.objects.link(keep)
    keep.parent = root
    for obj in first.values():
        obj["smoke.stale"] = True
    select(box)
    result, message = run(bpy.ops.aetheria.build_hull_source)
    require(result == {"FINISHED"}, f"The second Build was refused: {message}")
    second = generated(collection)
    require(sorted(second) == sorted(first), "The second Build changed the set of generated objects")
    require(not any(obj.get("smoke.stale") for obj in second.values()), "The second Build kept an object of the first")
    require(bpy.data.objects.get("author note") is keep and keep.parent == root, "Build removed an object the author made")
    require(len([o for o in collection.all_objects if o.name.startswith("Hull Collider")]) == 1, "A second Build left a second collider")
    print(f"PASS {number}.2 a second Build replaces only its own objects")

    # Operator art in the collection: Build refuses and changes nothing.
    art_mesh = bpy.data.meshes.new("author hull")
    art_mesh.from_pydata([(0, 0, 0), (1, 0, 0), (0, 1, 0)], [], [(0, 1, 2)])
    art = bpy.data.objects.new("author hull", art_mesh)
    collection.objects.link(art)
    art.parent = root
    names = sorted(o.name for o in collection.all_objects)
    select(box)
    result, message = run(bpy.ops.aetheria.build_hull_source)
    require(result == {"CANCELLED"} and "did not make" in message, f"Build overwrote or ignored operator art: {message!r}")
    require(sorted(o.name for o in collection.all_objects) == names, "A refused Build changed objects")
    for echoed in (ship_id, str(GAME), "author hull"):
        require(echoed not in message, f"The refusal echoes an input ({echoed})")
    bpy.data.objects.remove(art)
    print(f"PASS {number}.3 Build refuses operator art and changes nothing")

    # A hull source that breaks the brief: the refusal names the rule and nothing changes.
    original = source.read_text(encoding="utf-8")
    source.write_text(original.replace("HULL_LENGTH = [", "HULL_LENGTH = [200, 300]; UNUSED = ["), encoding="utf-8")
    names = sorted(o.name for o in collection.all_objects)
    select(box)
    result, message = run(bpy.ops.aetheria.build_hull_source)
    require(result == {"CANCELLED"} and "the length lies inside the brief's range" in message, f"A broken brief was built: {message!r}")
    require(sorted(o.name for o in collection.all_objects) == names, "A refused Build changed objects")
    for echoed in (ship_id, str(GAME), fixture):
        require(echoed not in message, f"The refusal echoes an input ({echoed})")
    source.write_text(original, encoding="utf-8")
    print(f"PASS {number}.4 Build refuses a hull that breaks its brief")

    # Package: the validator accepts the built ship and the GLB holds a node per anchor.
    select(box)
    result, message = run(bpy.ops.aetheria.package_ship)
    report = scene.aetheria_package_report
    require(result == {"FINISHED"} and "validator accepted" in report, f"Package refused the built ship: {report!r} {message!r}")
    require(source.is_file(), "Package deleted a hull source whose hull is still built from it")
    nodes = {n.get("name"): n for n in glb_nodes(package / "ship.glb")}
    for role, ids in roles.items():
        for anchor_id in ids:
            require(anchor_id in nodes, f"The GLB has no node for {anchor_id}")
    for zone in palette:
        require(zone in nodes, f"The GLB has no mesh node for zone {zone}")
    moved = [r for r in (hp for hp in state.hardpoints) if r.mount_id in roles["weapon-mount"]]
    print(f"INFO {number}.5 weapon rows after Package (fork package-derives-schematic-from-model): "
          f"{[(hp.mount_id, (hp.x, hp.y)) for hp in moved]} (authored at {[r[2] for r in rows if r[1] == BALLISTIC]})")
    print(f"PASS {number}.5 Package accepts the built ship and keeps hull.scad")

    # The author takes the generated objects over: Package deletes hull.scad and says so.
    for obj in generated(collection).values():
        del obj["aetheria.hull_source"]
    select(box)
    result, message = run(bpy.ops.aetheria.package_ship)
    report = scene.aetheria_package_report
    require(result == {"FINISHED"} and "deleted hull.scad" in report, f"Package did not retire the hull source: {report!r} {message!r}")
    require(not source.exists(), "hull.scad is still there after the author's art was packaged")
    require(all(echoed not in report for echoed in (ship_id, str(GAME))), "The report echoes an input")
    print(f"PASS {number}.6 operator art retires hull.scad, with a report")
    return collection


def clear(collection):
    """Removes a finished ship's objects: Blender names objects once per file, and anchors are found by name."""
    for obj in list(collection.all_objects if collection else ()):
        bpy.data.objects.remove(obj)


def never_built(scene):
    """A hull.scad whose hull was never built has no art in the collection: the deletion rule keeps it."""
    ship_id = "smoke.hull.unbuilt"
    collection, box, package = make_ship(scene, "pi-fighter", ship_id, (8.0, 15.0), 8, ())
    api = sys.modules["aetheria_ships"]
    require(api._retire_hull_source(collection) is False and (package / "hull.scad").is_file(),
            "A hull that was never built lost its hull.scad")
    print("PASS 3 a hull that was never built keeps its hull.scad")


def main():
    require(bpy.app.version[:2] == (5, 2), f"This smoke is for Blender 5.2; this is {bpy.app.version_string}")
    bpy.data.objects.remove(bpy.data.objects["Cube"], do_unlink=True)
    sys.path.insert(0, str(REPO / "tools" / "blender"))
    addon_utils.enable("aetheria_ships", default_set=True, handle_error=None)
    import aetheria_ships
    aetheria_ships._brokkr_cultlib = lambda context: PACKAGES
    preferences = bpy.context.preferences.addons["aetheria_ships"].preferences
    preferences.game_folder = str(GAME)
    if OPENSCAD:
        preferences.openscad = OPENSCAD
    real = aetheria_ships.hull_source.openscad_runner

    def logged(executable, source, **kwargs):
        def on_start(pid):
            if PID_LOG:
                with open(PID_LOG, "a") as log:
                    log.write(f"{pid}\n")
        return real(executable, source, on_start=on_start, **kwargs)

    aetheria_ships.hull_source.openscad_runner = logged
    for _, ship_id, *_ in HULLS:
        require(not (MODS / ship_id).exists(), f"{MODS / ship_id} exists; remove it first")
    scene = bpy.context.scene
    previous = None
    for number, (fixture, ship_id, extent, length, rows, body, palette) in enumerate(HULLS, start=1):
        clear(previous)
        previous = one_hull(scene, number, fixture, ship_id, extent, length, rows, body, palette)
    clear(previous)
    never_built(scene)


created = not MODS.exists()
try:
    main()
except SystemExit as stop:
    print(f"SMOKE_HULL_SOURCE_FAILED: {stop}", flush=True)
    code = 1
except Exception:
    traceback.print_exc()
    print("SMOKE_HULL_SOURCE_FAILED", flush=True)
    code = 1
else:
    print("SMOKE_HULL_SOURCE_OK", flush=True)
    code = 0
finally:
    for _, ship_id, *_ in HULLS + ((None, "smoke.hull.unbuilt"),):
        shutil.rmtree(MODS / ship_id, ignore_errors=True)
    if created:
        shutil.rmtree(MODS, ignore_errors=True)
sys.stdout.flush()
os._exit(code)
