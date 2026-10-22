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
  all recorded on the collection by name and fingerprint under Ship Root, each at the pose the hull source gives it
  (a mount yawed 90 degrees included), and no layout row changed by Build itself.
  A second Build replaces only its own objects: an object the author made, a mesh and an empty, is still there.
  Build refuses, naming a rule and no input, a hull source that breaks its language's or brief's rule, and changes nothing.
  Build refuses a hull source that redefines or shadows its faction's language (reassigned variable, second echo, a
  language file beside the hull), and changes nothing.
  Build refuses, and changes nothing, a collection that holds a render mesh it did not make, a copy of a generated zone,
  or a generated zone or mount that was edited in place.
  Package accepts the built ship (the C# validator), writes ship.glb with a node per anchor, and leaves hull.scad; a
  Build after Package still works.
  The author replaces the zone meshes with her art and keeps Build's anchors and collider: Package retires Build's
  records, and deletes hull.scad if it is still there (the second hull has had it removed by hand), in that one act and
  says so, and Build then refuses. The hull's state (plain, unbuilt, agent, art) is decided by one owner,
  _hull_state (map HS). Build's zones deleted
  with nothing in their place stay under the guard. Her art over a hull.scad that was never built ships and retires it.
  A hull collider alone is not her art. Package refuses a built hull whose maker record is gone, and an untouched
  Build zone beside her mesh with hull.scad gone by hand; an edit of every Build zone is her art.
  A plain hull with her art ships and retires nothing. A hull.scad with nothing built and none of her art keeps its
  hull.scad, and Package refuses it (no maker was judged).
  Package refuses an agent-built hull before it writes anything (ship.glb is not made) when the package holds no product,
  or a product by another maker than the language Build judged it against (hull-names-its-own-faction); with a product by
  that maker it accepts, with hull.scad in the package or not (the guard keys on the hull's state), and a refused Package
  changes nothing in the scene. Her art needs no product.
  Schematic lines: after Build and Package the ship.cc's first line is the silhouette and the kit's lines follow (the Pirate
  hull's kit draws none); a Grease Pencil object in the collection replaces them with its own; a second one is refused by
  Package and Capture Ship Lines, and the slot is as it was. The Nakovalnya's hull.scad, read with the real kit, has a
  silhouette whose box is the zones' top-view box and a fitting octagon at every flak mount anchor.
  Build and Package refuse copies that share an aetheria.id, selecting the original and the copies, and delete nothing.
  Package moves no row and turns no mount: every layout row is as authored after it, and a hand-turned mount keeps its yaw.
  The ship.cc HullData.HullType decides whether the language's proportions judge the hull: a body far wider than the
  language's ships is refused as Ship and built as Station, and a Station wider than the layout grid is refused.
"""

import dataclasses
import json
import math
import os
import shutil
import struct
import sys
import traceback
from pathlib import Path

import addon_utils
import bmesh
import bpy
import msgpack

REPO = Path(__file__).resolve().parents[3]
PACKAGES = os.environ.get("CULTLIB_PACKAGES") or sys.exit("Set CULTLIB_PACKAGES to CultLib's packages directory")
GAME = Path(os.environ.get("SMOKE_GAME_FOLDER") or sys.exit("Set SMOKE_GAME_FOLDER to a folder holding GameData/Aetheria.cc and ModTools/AetherDb"))
OPENSCAD = os.environ.get("SMOKE_OPENSCAD")
PID_LOG = os.environ.get("SMOKE_PID_LOG")
MODS = GAME / "GameData" / "Mods"
FIXTURES = REPO / "tools" / "blender" / "tests" / "fixtures" / "hulls"
THRUSTER, RADIATOR, BALLISTIC = "3", "6", "10"

# fixture folder, ship id, stand-in box (x, y) metres and its length in cells, the layout rows (mount id, kind, cell),
# the body zone the collider copies, the palette each zone must carry, the Faction name of the maker whose language the
# hull speaks (AetherDb resolves it to the product's Manufacturer), and a Faction that is not its maker
HULLS = (
    ("zh-ammo-tender", "smoke.hull.tender", (10.0, 23.4), 12,
     (("Gun.D", BALLISTIC, (2, 3)), ("Th.L", THRUSTER, (0, 0)), ("Th.R", THRUSTER, (1, 0)),
      ("Ra.L", RADIATOR, (2, 0)), ("Ra.R", RADIATOR, (3, 0))),
     "primary", {"primary": (0.47, 0.48, 0.50), "secondary": (0.21, 0.21, 0.23), "accent": (0.56, 0.20, 0.12), "recess": (0.05, 0.05, 0.06),
      "marking": (0.86, 0.80, 0.64)},
     "Zhestokost", "Pirates"),
    ("pi-fighter", "smoke.hull.fighter", (8.0, 15.0), 8,
     (("Gun.P", BALLISTIC, (3, 3)), ("Th.Main", THRUSTER, (0, 0)), ("Th.L", THRUSTER, (1, 0))),
     "rnd.primary", {"rnd.primary": (0.72, 0.74, 0.76), "primer": (0.48, 0.27, 0.22), "weld": (0.70, 0.68, 0.62),
      "accent": (0.10, 0.62, 0.60), "rnd.accent": (0.90, 0.89, 0.84)},
     "Pirates", "Zhestokost"),
)


# per fixture: the variable the hull's language is in, a value that redefines it (every rule loosened), the language file a
# copy of which beside hull.scad would shadow the include, and a colour in it. %MAKER% is the maker key the faction's own
# language file names, read by the production reader (language_maker), so a language edited alone cannot leave it stale
ATTACKS = {
    "zh-ammo-tender": ("ZH_LANG", '["zhestokost", ZH_ZONES, "primary", ZH_GLOW, ZH_RADIATOR, [[0, 9], [0, 9]], ["asymmetric", 0.0], "%MAKER%"]',
                       "zhestokost.scad", "[0.47, 0.48, 0.50]"),
    "pi-fighter": ("PI_LANG", '["pirate", PI_ZONES, "", PI_GLOW, PI_RADIATOR, [], ["bilateral", 0.0], "%MAKER%"]',
                   "pirate.scad", "[0.48, 0.27, 0.22]"),
}
EXECUTABLE = None  # the OpenSCAD the add-on preference names, set by main
hull_source = None  # aetheria_ships.hull_source, set by main


def language_maker(faction):
    """The maker key the faction's own language file names, as Build reads it."""
    return hull_source.read_languages(hull_source.language_library(EXECUTABLE)(faction))[0].maker


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
    """Build's objects, by the record Build keeps on the collection."""
    record = collection.get("aetheria.hull_built")
    return {obj.name: obj for obj in collection.all_objects if record is not None and obj.name in record}


def layout_rows(scene):
    return [(hp.mount_id, hp.kind, (hp.x, hp.y), hp.rotation, hp.armor, hp.firing_arc, hp.footprint)
            for hp in scene.aetheria_layout.hardpoints]


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


def one_hull(scene, number, fixture, ship_id, extent, length, rows, body, palette, maker, other):
    collection, box, package = make_ship(scene, fixture, ship_id, extent, length, rows)
    state = scene.aetheria_layout
    root = bpy.data.objects[next(o.name for o in collection.all_objects if o.get("aetheria.ship_root"))]
    rows_before = layout_rows(scene)
    source = package / "hull.scad"
    lang_var, lang_value, shadowed, shadow_colour = ATTACKS[fixture]
    lang_value = lang_value.replace("%MAKER%", language_maker(shadowed[:-len(".scad")]))
    runner = hull_source.openscad_runner(EXECUTABLE, source)
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
    require(layout_rows(scene) == rows_before, "Build changed the layout buffer")
    check_poses(collection, root, runner)
    if fixture == "zh-ammo-tender":
        original = source.read_text(encoding="utf-8")
        yawed_mount(scene, box, collection, root, package, source, original, runner)
        first = generated(collection)
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

    # Package refuses an agent-built hull whose package has no product, one by another maker, or one right-maker product
    # beside a wrong-maker one, and does so whether or not hull.scad is still in the package (the guard keys on Build's
    # record), before it frames, exports or writes anything; the maker of the language Build judged it against is accepted.
    ship_cc = str(package / "ship.cc")
    require(collection.get("aetheria.hull_maker") and not (package / "ship.glb").exists(), "Build recorded no maker, or Package ran")
    away = source.with_name("hull.scad.away")

    def add_product(faction, name):
        require(aetherdb(ship_cc, "product", ship_cc, faction, "--name", name) == 0, "AetherDb could not write a product")

    def scad_away():
        source.rename(away)

    def scad_back():
        away.rename(source)

    off_centre = root.location.x
    root.location.x = off_centre + 1.7  # _frame_ship would put it back: a refusal that frames first is caught
    bpy.context.view_layer.update()
    framed = {o.name: tuple(o.matrix_world.translation) for o in collection.all_objects}
    for label, expected, prepare, restore in (
            ("no product", "The package has no product", lambda: None, lambda: None),
            ("a product by another maker", "not by the maker of the hull's faction language",
             lambda: add_product(other, "x"), lambda: None),
            ("a product by another maker and hull.scad gone", "not by the maker of the hull's faction language",
             scad_away, scad_back),
            ("a right-maker and a wrong-maker product", "not by the maker of the hull's faction language",
             lambda: add_product(maker, "y"), lambda: None)):
        prepare()
        select(box)
        rows_now = layout_rows(scene)
        result, message = run(bpy.ops.aetheria.package_ship)
        restore()
        bpy.context.view_layer.update()
        report = scene.aetheria_package_report
        require(result == {"CANCELLED"} and expected in report, f"Package took a hull with {label}: {report!r} {message!r}")
        require(not (package / "ship.glb").exists(), f"Package wrote ship.glb for a hull with {label}")
        require(layout_rows(scene) == rows_now, f"A refused Package changed a row ({label})")
        require({o.name: tuple(o.matrix_world.translation) for o in collection.all_objects} == framed,
                f"A refused Package moved the scene ({label})")
        for echoed in (ship_id, str(GAME), maker, other, collection["aetheria.hull_maker"]):
            require(echoed not in report, f"The refusal echoes an input ({echoed})")
    root.location.x = off_centre
    require(aetherdb(ship_cc, "product", ship_cc, other, "--remove") == 0, "AetherDb could not remove the other maker's product")

    # Record keys compare exactly: a Manufacturer that differs from the language's maker only in case names no Faction.
    # AetherDb cannot write one, but a hand-edited ship.cc or a cultcache-py push can, and the guard alone refuses it.
    def manufacturer_as(change):
        ship_module = sys.modules["aetheria_ships.ship_cc"]
        store = ship_module.read(ship_cc, PACKAGES).store
        seen = []
        for envelope in store.pull_all():
            if envelope.type == ship_module.PRODUCT_SCHEMA:
                values = msgpack.unpackb(envelope.payload, raw=False)
                seen.append(values[ship_module.PRODUCT_MANUFACTURER_SLOT])
                values[ship_module.PRODUCT_MANUFACTURER_SLOT] = change(seen[-1])
                store.push(dataclasses.replace(envelope, payload=msgpack.packb(values, use_bin_type=True)))
        return seen

    written = manufacturer_as(str.upper)
    require(written and all(key.upper() != key for key in written), "The package holds no product with a cased key to rewrite")
    select(box)
    result, message = run(bpy.ops.aetheria.package_ship)
    report = scene.aetheria_package_report
    require(result == {"CANCELLED"} and "not by the maker of the hull's faction language" in report,
            f"Package took a product whose Manufacturer differs from the maker in case: {report!r} {message!r}")
    require(not (package / "ship.glb").exists(), "Package wrote ship.glb for a case-variant Manufacturer")
    for echoed in (ship_id, str(GAME), maker, collection["aetheria.hull_maker"], collection["aetheria.hull_maker"].upper()):
        require(echoed not in report, f"The refusal echoes an input ({echoed})")
    manufacturer_as(lambda _: written.pop(0))

    # Package: the validator accepts the built ship and the GLB holds a node per anchor.
    mount = bpy.data.objects[roles["weapon-mount"][0]]
    straight = mount.rotation_euler.z
    mount.rotation_euler.z = straight + 0.3  # hand-turned: Package turns no mount
    turned, rows_authored = mount.rotation_euler.z, layout_rows(scene)
    select(box)
    result, message = run(bpy.ops.aetheria.package_ship)
    report = scene.aetheria_package_report
    require(result == {"FINISHED"} and "validator accepted" in report, f"Package refused the built ship: {report!r} {message!r}")
    require(layout_rows(scene) == rows_authored, "Package changed a layout row")
    require(abs(mount.rotation_euler.z - turned) < 1e-6, "Package turned a mount")
    mount.rotation_euler.z = straight
    require(source.is_file(), "Package deleted a hull source whose hull is still built from it")
    nodes = {n.get("name"): n for n in glb_nodes(package / "ship.glb")}
    for role, ids in roles.items():
        for anchor_id in ids:
            require(anchor_id in nodes, f"The GLB has no node for {anchor_id}")
    for zone in palette:
        require(zone in nodes, f"The GLB has no mesh node for zone {zone}")
    saved = [(r[0], tuple(r[2])) for r in layout_rows(scene)]
    require(saved == [(mount_id, cell) for mount_id, _, cell in rows], f"A row is not as authored after Package: {saved}")
    print(f"PASS {number}.5 Package accepts the built ship and keeps hull.scad")

    # Schematic lines: Build's, else hers, and never two of hers.
    packaged = ship_lines(ship_cc)
    require(packaged and packaged[0][0] == "silhouette" and packaged[0][5] is True, f"The first line is not the silhouette: {packaged[:1]}")
    kit = [row for row in packaged if row[0] != "silhouette"]
    drawn = [row[0] for row in packaged]
    require(drawn[:len(packaged) - len(kit)] == ["silhouette"] * (len(packaged) - len(kit)), "The silhouette contours are not first")
    require(all(row[3] is None and row[4] is None and row[1] == "" for row in packaged),
            "A kit line row is not shaped as Package writes it")
    require(bool(kit) == (fixture == "zh-ammo-tender"), f"The kit drew {len(kit)} lines for {fixture}")
    require(all(approx(row[6], hull_source.LINE_STYLE[row[0]]) for row in packaged), "A line lost its style")
    shipped_z = {round(c, 3) for row in packaged for c in row[2][2::3]}
    require(len(shipped_z) >= 1, "The lines have no height")
    pencil = grease_pencil(collection, "her lines", [(0, 0, 0), (1, 0, 0), (1, 1, 0)])
    select(box)
    result, message = run(bpy.ops.aetheria.package_ship)
    require(result == {"FINISHED"}, f"Package refused a ship with one Grease Pencil: {message!r}")
    hers = ship_lines(ship_cc)
    require(len(hers) == 1 and hers[0][0] == "hers" and len(hers[0][2]) == 9, f"Her Grease Pencil did not replace the kit's lines: {hers}")
    second = grease_pencil(collection, "second lines", [(0, 0, 0), (2, 0, 0)])
    before = ship_lines(ship_cc)
    for operator in (bpy.ops.aetheria.package_ship, bpy.ops.aetheria.capture_ship_lines):
        select(box)
        result, message = run(operator)
        require(result == {"CANCELLED"} and "Keep one Grease Pencil object" in (message + scene.aetheria_package_report),
                f"Two Grease Pencil objects were not refused: {message!r}")
    require(ship_lines(ship_cc) == before, "A refused schematic changed the line slot")
    for obj in (pencil, second):
        bpy.data.objects.remove(obj)
    select(box)
    result, message = run(bpy.ops.aetheria.package_ship)
    require(result == {"FINISHED"} and len(ship_lines(ship_cc)) == len(packaged), f"Without Grease Pencil the kit's lines are back: {message!r}")
    print(f"PASS {number}.5b the kit's lines ship, her Grease Pencil wins, two are refused")

    # What Package did to the built objects is not an edit: Build still replaces them.
    select(box)
    result, message = run(bpy.ops.aetheria.build_hull_source)
    require(result == {"FINISHED"}, f"Build after Package was refused: {message!r}")
    print(f"PASS {number}.6 Build after Package replaces its own objects")

    # A hull source that redefines or shadows its faction's language: refused, nothing changes.
    attacks = []
    for label, change in (
            ("reassigns the language after the include", lambda text: text + f"\n{lang_var} = {lang_value};\n"),
            ("echoes a second language", lambda text: text + f'\nif (part == "lang") echo(lang = {lang_value});\n')):
        attacks.append((label, lambda change=change: source.write_text(change(original), encoding="utf-8"), lambda: None))
    shadow = package / shadowed
    shadow_text = (REPO / "tools" / "blender" / "aetheria_ships" / "factions" / shadowed).read_text(encoding="utf-8")
    require(shadow_text.count(shadow_colour) >= 1, "The shadow's colour is not in the language file")
    attacks.append(("shadows the language file beside itself",
                    lambda: shadow.write_text(shadow_text.replace(shadow_colour, "[1, 0, 1]"), encoding="utf-8"),
                    lambda: shadow.unlink()))
    for label, apply, undo in attacks:
        apply()
        names = sorted(o.name for o in collection.all_objects)
        select(box)
        result, message = run(bpy.ops.aetheria.build_hull_source)
        undo()
        source.write_text(original, encoding="utf-8")
        require(result == {"CANCELLED"} and "includes its faction's language files" in message,
                f"A hull that {label} was built: {message!r}")
        require(sorted(o.name for o in collection.all_objects) == names, "A refused Build changed objects")
        for echoed in (ship_id, str(GAME), fixture):
            require(echoed not in message, f"The refusal echoes an input ({echoed})")
    print(f"PASS {number}.7 Build refuses a hull that redefines or shadows its language")

    # An author's copy of a generated zone, and an edit of a generated zone or mount, are the author's: Build refuses
    # and leaves them as they are.
    zone = bpy.data.objects[body]
    piece = zone.copy()
    piece.data = zone.data.copy()
    piece.name = "my piece"
    collection.objects.link(piece)
    names = sorted(o.name for o in collection.all_objects)
    select(box)
    result, message = run(bpy.ops.aetheria.build_hull_source)
    require(result == {"CANCELLED"} and "did not make" in message, f"Build ignored a copy of a generated zone: {message!r}")
    require(sorted(o.name for o in collection.all_objects) == names and bpy.data.objects.get("my piece") is piece,
            "Build removed a copy of a generated zone")
    mesh = piece.data
    bpy.data.objects.remove(piece)
    bpy.data.meshes.remove(mesh)
    original_vertices = [tuple(v.co) for v in zone.data.vertices]
    zone.data.vertices[0].co.z += 5.0
    zone.data.update()
    names = sorted(o.name for o in collection.all_objects)
    select(box)
    result, message = run(bpy.ops.aetheria.build_hull_source)
    require(result == {"CANCELLED"} and "did not make" in message, f"Build ignored an edited zone: {message!r}")
    require(sorted(o.name for o in collection.all_objects) == names and abs(zone.data.vertices[0].co.z - original_vertices[0][2] - 5.0) < 1e-3,
            "Build discarded an edited zone")
    api = sys.modules["aetheria_ships"]
    require(api._retire_hull_source(collection) is False and source.is_file(),
            "One edited zone, with the others still Build's, retired hull.scad")
    for vertex, co in zip(zone.data.vertices, original_vertices):
        vertex.co = co
    zone.data.update()
    mount = bpy.data.objects[roles["weapon-mount"][0]]
    at = tuple(mount.location)
    mount.location.x += 1.0
    select(box)
    result, message = run(bpy.ops.aetheria.build_hull_source)
    require(result == {"CANCELLED"} and abs(mount.location.x - at[0] - 1.0) < 1e-6, f"Build discarded a moved mount: {message!r}")
    mount.location = at
    shared_ids_refused(scene, box, collection, ship_id, package, generated(collection), number)
    print(f"PASS {number}.8 Build refuses and keeps a copy and an edit of what it made")

    # The author replaces the zone meshes with her art and keeps Build's anchors and collider: Package ships the ship
    # and retires hull.scad in that one act; Build is gone with it.
    for name in list(generated(collection)):
        obj = generated(collection)[name]
        if obj.type == "MESH" and "aetheria.role" not in obj:
            bpy.data.objects.remove(obj)
    # Build's zones deleted with nothing in their place: the hull stays agent's and the maker guard refuses it (H8, H12).
    require(aetherdb(ship_cc, "product", ship_cc, maker, "--remove") == 0, "AetherDb could not remove the product")  # her art needs none
    if number % 2 == 0:  # the second hull: she (or git) removed hull.scad herself first; Build's record outlives it
        source.unlink()
        require(collection.get("aetheria.hull_maker"), "Build's maker record is gone before Package")
    glb_before = (package / "ship.glb").read_bytes()
    select(box)
    result, message = run(bpy.ops.aetheria.package_ship)
    report = scene.aetheria_package_report
    require(result == {"CANCELLED"} and "The package has no product" in report, f"Package took a hull with no zones and no art: {report!r} {message!r}")
    require((package / "ship.glb").read_bytes() == glb_before, "A refused Package changed ship.glb")
    require("aetheria.hull_maker" in collection and "aetheria.hull_built" in collection and source.is_file() == (number % 2 == 1),
            "A refused Package changed Build's records or hull.scad")
    print(f"PASS {number}.9a Build's zones deleted with nothing in their place stay under the guard")
    art_mesh = bpy.data.meshes.new("author hull")
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bm.to_mesh(art_mesh)
    bm.free()
    art = bpy.data.objects.new("author hull", art_mesh)
    collection.objects.link(art)
    art.parent = root
    require(any(o.get("aetheria.role") for o in generated(collection).values()), "The scene kept no anchor of Build's")
    select(box)
    result, message = run(bpy.ops.aetheria.package_ship)
    report = scene.aetheria_package_report
    require(result == {"FINISHED"} and "retired the agent hull source" in report, f"Package did not retire the hull source: {report!r} {message!r}")
    require(not source.exists(), "hull.scad is still there after the author's art was packaged")
    require("aetheria.hull_built" not in collection and "aetheria.hull_maker" not in collection,
            "The record of Build's objects or its maker outlived hull.scad")
    require(all(echoed not in report for echoed in (ship_id, str(GAME))), "The report echoes an input")
    select(box)
    result, message = run(bpy.ops.aetheria.build_hull_source)
    require(result == {"CANCELLED"} and bpy.data.objects.get("author hull") is art, f"Build ran after takeover: {message!r}")
    print(f"PASS {number}.9 her art with Build's anchors kept retires Build's records and any hull.scad, with a report; Build then refuses")
    return collection


def shared_ids_refused(scene, box, collection, ship_id, package, objects, number):
    """A copy of the collider, an emitter or radiator mesh or a weapon mount shares its original's aetheria.id: Build and
    Package refuse by selecting the original and the copy, name a count and no input, and delete nothing."""
    glb = package / "ship.glb"
    glb_before = glb.read_bytes()
    seen = set()
    for obj in list(objects.values()):
        role = obj.get("aetheria.role")
        if role not in ("hull-collider", "thruster-emitter", "radiator-mesh", "weapon-mount") or role in seen:
            continue
        seen.add(role)
        twin = obj.copy()
        if obj.data is not None:
            twin.data = obj.data.copy()
        collection.objects.link(twin)
        twin.parent = obj.parent
        require(twin.get("aetheria.id") == obj.get("aetheria.id"), "The copy does not share its original's aetheria.id")
        names = sorted(o.name for o in collection.all_objects)
        for operator in (bpy.ops.aetheria.build_hull_source, bpy.ops.aetheria.package_ship):
            for other in bpy.context.view_layer.objects:
                other.select_set(False)
            select(box)
            result, message = run(operator)
            message = message or scene.aetheria_package_report
            require(result == {"CANCELLED"} and "2 objects share an aetheria.id" in message,
                    f"A copy of a {role} was not refused by name of the shared id: {message!r}")
            for echoed in (ship_id, str(GAME), obj.name, twin.name, obj["aetheria.id"]):
                require(echoed not in message, f"The refusal echoes an input ({echoed})")
            require(obj.select_get() and twin.select_get() and not box.select_get(),
                    f"The refusal did not select the original and the copy of a {role}")
            require(sorted(o.name for o in collection.all_objects) == names, "A refused operator deleted or added an object")
            require(glb.read_bytes() == glb_before, "A refused Package wrote ship.glb")
        mesh = twin.data
        bpy.data.objects.remove(twin)
        if mesh is not None and mesh.users == 0:
            bpy.data.meshes.remove(mesh)
    require(len(seen) >= 3, f"The copies covered too few roles: {sorted(seen)}")
    print(f"PASS {number}.8b copies of {sorted(seen)} are refused by Build and Package, selected, nothing deleted")


def check_poses(collection, root, runner):
    """Every built anchor stands where the hull source says in Ship Root's frame, turned as it says, and the emitter and
    radiator meshes are the source's meshes in that frame."""
    inverse = root.matrix_world.inverted()
    for anchor in hull_source.read_anchors(runner("anchors")[1]):
        obj = bpy.data.objects[anchor.id]
        local = inverse @ obj.matrix_world
        require(approx(tuple(local.translation), anchor.position), f"{anchor.id} stands at {tuple(local.translation)}, not {anchor.position}")
        if anchor.role != "weapon-muzzle":
            turn = local.to_euler().z
            want = math.radians(anchor.yaw)
            require(approx((math.cos(turn), math.sin(turn)), (math.cos(want), math.sin(want))),
                    f"{anchor.id} is turned {math.degrees(turn):.1f} degrees, not {anchor.yaw}")
        if anchor.role in hull_source.MESH_ROLES:
            made = sorted(tuple(round(c, 3) for c in (inverse @ obj.matrix_world @ v.co)) for v in obj.data.vertices)
            wanted = sorted(tuple(round(c, 3) for c in v) for v in hull_source.parse_off(runner(hull_source.MESH_ROLES[anchor.role] + anchor.id)[0]).vertices)
            require(len(made) == len(wanted) and all(approx(a, b, 2e-3) for a, b in zip(made, wanted)),
                    f"The mesh of {anchor.id} is not the source's mesh in Ship Root's frame")


def yawed_mount(scene, box, collection, root, package, source, original, runner):
    """A mount the source turns 90 degrees stands, turned, where it says, and its muzzle follows it."""
    changed = original.replace('[0, 0, 0.33]), 0, ""]', '[0, 0, 0.33]), 90, ""]')
    require(changed != original, "The tender's mount is not where the pose test turns it")
    source.write_text(changed, encoding="utf-8")
    select(box)
    result, message = run(bpy.ops.aetheria.build_hull_source)
    require(result == {"FINISHED"}, f"Build refused a turned mount: {message!r}")
    check_poses(collection, root, runner)
    require(abs(bpy.data.objects["Gun.D"].rotation_euler.z - math.pi / 2) < 1e-4, "The mount is not turned 90 degrees")
    source.write_text(original, encoding="utf-8")
    select(box)
    result, message = run(bpy.ops.aetheria.build_hull_source)
    require(result == {"FINISHED"}, f"Build refused the restored source: {message!r}")
    print("PASS 1.10 a turned mount and its muzzle stand where the source puts them")


def clear(collection):
    """Removes a finished ship's objects: Blender names objects once per file, and anchors are found by name."""
    for obj in list(collection.all_objects if collection else ()):
        bpy.data.objects.remove(obj)


def ship_lines(ship_path):
    """The ShipAuthoring.SchematicLines rows of a ship.cc, as Package wrote them."""
    return sys.modules["aetheria_ships"].read(ship_path, PACKAGES).ship.body[sys.modules["aetheria_ships"].ship_cc.SCHEMATIC_LINES_SLOT]


def grease_pencil(collection, name, points):
    """A Grease Pencil object in the collection holding one stroke, layer named 'hers'."""
    data = bpy.data.grease_pencils.new(name)
    material = bpy.data.materials.new(name)
    bpy.data.materials.create_gpencil_data(material)
    data.materials.append(material)
    layer = data.layers.new("hers")
    drawing = layer.frames.new(1).drawing
    drawing.add_strokes([len(points)])
    for point, position in zip(drawing.strokes[0].points, points):
        point.position = position
    obj = bpy.data.objects.new(name, data)
    collection.objects.link(obj)
    return obj


def nakovalnya_frames(executable):
    """The Nakovalnya's hull.scad with the real kit: the silhouette is the zones' top view and a fitting octagon sits at
    every flak mount (the frame the kit placed the barrel in is the frame the anchor is in)."""
    source = REPO / "GameData" / "Mods" / "aetheria.nakovalnya" / "hull.scad"
    built = hull_source.build(hull_source.openscad_runner(executable, source), hull_source.language_library(executable),
                              "Ship", hull_source.openscad_projector(executable))
    require(not built.failed(), f"The Nakovalnya breaks a check: {[c.name for c in built.failed()]}")
    silhouette = [row for row in built.lines if row.layer == "silhouette"]
    require(built.lines[0].layer == "silhouette" and len(silhouette) == 1, "The Nakovalnya silhouette is not one first contour")
    xs = [v[0] for mesh in built.zones.values() for v in mesh.vertices]
    ys = [v[1] for mesh in built.zones.values() for v in mesh.vertices]
    box = (min(xs), max(xs), min(ys), max(ys))
    flat = silhouette[0].points
    got = (min(p[0] for p in flat), max(p[0] for p in flat), min(p[1] for p in flat), max(p[1] for p in flat))
    require(approx(box, got, 1e-3), f"The silhouette box {got} is not the zones' top-view box {box}")
    octagons = [row.points for row in built.lines if row.layer == "fitting" and row.cyclic and len(row.points) == 8]
    centres = [(sum(p[0] for p in o) / 8, sum(p[1] for p in o) / 8) for o in octagons]
    mounts = [a for a in built.anchors if a.id.startswith("Gun.F") and a.role == "weapon-mount"]
    require(len(mounts) == 10, f"The Nakovalnya has {len(mounts)} flak mounts")
    for mount in mounts:
        near = min(math.hypot(mount.position[0] - c[0], mount.position[1] - c[1]) for c in centres)
        require(near <= 0.3, f"{mount.id} is {near:.2f} m from the nearest fitting octagon")
    # Parts that face sideways (the belt collars and turning blocks, the wall hatches) draw nothing: no top view sees them.
    require(len(octagons) == 20, f"{len(octagons)} octagons: ten flak drums and ten flak rings, and no belt collar's")
    require(sum(row.layer != "silhouette" for row in built.lines) == 102, "The Nakovalnya kit's line count changed")
    kinds = {row.layer for row in built.lines}
    require({"silhouette", "outline", "step", "seam", "fitting"} <= kinds, f"The Nakovalnya lacks a layer: {kinds}")
    print(f"PASS frames the Nakovalnya's {len(built.lines)} lines are in the frame of its geometry")


def aetherdb(ship_path, *args):
    """Runs one AetherDb ship-authoring command through the add-on's own runner; returns its exit code."""
    return sys.modules["aetheria_ships"]._aetherdb(bpy.context, *args, ship_cc=ship_path)[0]


def never_built(scene):
    """A hull.scad whose hull was never built has no art in the collection: the deletion rule keeps it."""
    ship_id = "smoke.hull.unbuilt"
    collection, box, package = make_ship(scene, "pi-fighter", ship_id, (8.0, 15.0), 8, ())
    api = sys.modules["aetheria_ships"]
    require(api._retire_hull_source(collection) is False and (package / "hull.scad").is_file(),
            "A hull that was never built lost its hull.scad")
    select(box)
    result, message = run(bpy.ops.aetheria.package_ship)
    report = scene.aetheria_package_report
    unbuilt_text = ("Build From Hull Source first, or put your own meshes under Ship Root: Package needs the maker "
                    "Build judged the hull against")
    require(result == {"CANCELLED"} and unbuilt_text in report and not (package / "ship.glb").exists(),
            f"Package took a hull that was never built, or its refusal names no exit: {report!r}")
    print("PASS 3 a hull that was never built keeps its hull.scad, and Package refuses it naming the exit")
    # Her hull collider alone is not her art: it carries a role, so the hull is still unbuilt and keeps its hull.scad.
    root = api._ship_root(collection)
    collider_mesh = bpy.data.meshes.new("author collider")
    collider_mesh.from_pydata([(0, 0, 0), (1, 0, 0), (0, 1, 0)], [], [(0, 1, 2)])
    lone = bpy.data.objects.new("Hull Collider", collider_mesh)
    lone["aetheria.role"], lone["aetheria.id"] = "hull-collider", "collider"
    collection.objects.link(lone)
    lone.parent = root
    select(box)
    result, message = run(bpy.ops.aetheria.package_ship)
    report = scene.aetheria_package_report
    require(result == {"CANCELLED"} and unbuilt_text in report and (package / "hull.scad").is_file()
            and not (package / "ship.glb").exists(),
            f"A hull collider alone was taken for her art: {report!r}")
    bpy.data.objects.remove(lone)
    bpy.data.meshes.remove(collider_mesh)
    print("PASS 3.0 a hull collider alone is not her art: Package still refuses and keeps hull.scad")
    # Her art over a hull.scad Build never ran is hers: it ships and the hull source is retired (H4).
    art_mesh = bpy.data.meshes.new("author unbuilt hull")
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bm.to_mesh(art_mesh)
    bm.free()
    art = bpy.data.objects.new("author unbuilt hull", art_mesh)
    collection.objects.link(art)
    art.parent = api._ship_root(collection)
    collider = bpy.data.objects.new("Hull Collider", art_mesh)  # the validator wants exactly one hull-collider anchor
    collider["aetheria.role"], collider["aetheria.id"] = "hull-collider", "collider"
    collection.objects.link(collider)
    collider.parent = art.parent
    select(box)
    result, message = run(bpy.ops.aetheria.package_ship)
    report = scene.aetheria_package_report
    require(result == {"FINISHED"} and "retired the agent hull source" in report, f"Package refused her art over an unbuilt hull.scad: {report!r} {message!r}")
    require(not (package / "hull.scad").exists() and "aetheria.hull_built" not in collection and "aetheria.hull_maker" not in collection,
            "Her art over an unbuilt hull.scad left Build's source or records")
    select(box)
    result, message = run(bpy.ops.aetheria.build_hull_source)
    require(result == {"CANCELLED"}, f"Build ran over her art: {message!r}")
    print("PASS 3.1 her art over a hull.scad Build never ran ships and retires it")
    select(box)
    result, message = run(bpy.ops.aetheria.package_ship)
    report = scene.aetheria_package_report
    require(result == {"FINISHED"} and "retired" not in report and not (package / "hull.scad").exists(), f"A plain hull with her art did not ship alone: {report!r}")
    print("PASS 3.2 a plain hull with her art ships and retires nothing")


def agent_guards(scene):
    """Rows of the state table (map HS) that only a fresh built hull with no product can reach: the guard needs the
    maker record, a hull.scad gone by hand does not lift it (H10), and her edit of every Build zone is her art (H6)."""
    fixture, _, extent, length, rows, *_ = HULLS[1]
    ship_id = "smoke.hull.guards"
    collection, box, package = make_ship(scene, fixture, ship_id, extent, length, rows)
    api = sys.modules["aetheria_ships"]
    root = api._ship_root(collection)
    source = package / "hull.scad"
    select(box)
    result, message = run(bpy.ops.aetheria.build_hull_source)
    require(result == {"FINISHED"}, f"Build was refused: {message!r}")

    def refused(text, label):
        select(box)
        result, message = run(bpy.ops.aetheria.package_ship)
        report = scene.aetheria_package_report
        require(result == {"CANCELLED"} and text in report and not (package / "ship.glb").exists(),
                f"Package took {label}: {report!r} {message!r}")
        require("aetheria.hull_built" in collection and "aetheria.hull_maker" in collection,
                f"A refused Package changed Build's records ({label})")

    # Build's zones intact but the maker record gone: nothing to compare the products with, so Package refuses.
    maker = collection["aetheria.hull_maker"]
    del collection["aetheria.hull_maker"]
    select(box)
    result, message = run(bpy.ops.aetheria.package_ship)
    report = scene.aetheria_package_report
    require(result == {"CANCELLED"} and "Build From Hull Source first: Package needs the maker it judged the hull against" in report
            and source.is_file() and "aetheria.hull_built" in collection and not (package / "ship.glb").exists(),
            f"Package took a built hull whose maker record is gone: {report!r} {message!r}")
    collection["aetheria.hull_maker"] = maker
    print("PASS 4.1 Build's zones with no maker record: Package refuses and keeps everything")

    # H10: one of Build's zones untouched beside her mesh, hull.scad gone by hand: still the agent's, still guarded.
    art_mesh = bpy.data.meshes.new("author hull")
    art_mesh.from_pydata([(0, 0, 0), (1, 0, 0), (0, 1, 0)], [], [(0, 1, 2)])
    art = bpy.data.objects.new("author hull", art_mesh)
    collection.objects.link(art)
    art.parent = root
    away = source.with_name("hull.scad.away")
    source.rename(away)
    refused("The package has no product: add one for the hull's maker before packaging",
            "a hull with an untouched Build zone, her mesh and no hull.scad")
    away.rename(source)
    bpy.data.objects.remove(art)
    bpy.data.meshes.remove(art_mesh)
    require(source.is_file(), "hull.scad was not put back")
    print("PASS 4.2 an untouched Build zone beside her mesh stays guarded with hull.scad gone")

    # H6: she edited every Build zone and made nothing else: the zones are her art, Package ships with no product and
    # retires Build's records and hull.scad.
    zones = [o for o in generated(collection).values() if o.type == "MESH" and "aetheria.role" not in o]
    require(len(zones) >= 2, "The hull has too few zones to edit them all")
    for zone in zones:
        zone.data.vertices[0].co.z += 5.0
        zone.data.update()
    select(box)
    result, message = run(bpy.ops.aetheria.package_ship)
    report = scene.aetheria_package_report
    require(result == {"FINISHED"} and "retired the agent hull source" in report, f"Package refused a hull whose every zone she edited: {report!r} {message!r}")
    require(not source.exists() and "aetheria.hull_built" not in collection and "aetheria.hull_maker" not in collection,
            "Her edit of every zone left Build's source or records")
    print("PASS 4.3 an edit of every Build zone is her art: Package ships it with no product and retires Build's records")
    return collection


def hull_type_gate(scene):
    """The hull's HullData.HullType in ship.cc decides whether the language's proportions judge it; every hull type must
    fit the layout grid. A body far wider than a Zhestokost ship is refused as Ship, built as Station, and a Station
    wider than 32 cells is refused on the grid."""
    fixture, _, extent, length, rows, *_ = HULLS[0]
    ship_id = "smoke.hull.role"
    collection, box, package = make_ship(scene, fixture, ship_id, extent, length, rows)
    ship_module = sys.modules["aetheria_ships.ship_cc"]
    path = str(package / "ship.cc")
    source = package / "hull.scad"
    original = source.read_text()
    slab = '\nif (part == "primary") translate([-%d, -2, 0]) cube([%d, 4, 1]);\n'
    source.write_text(original + slab % (15, 30))
    judged = []
    real = hull_source.build

    def recorded(*args):
        judged.append(real(*args))
        return judged[-1]

    sys.modules["aetheria_ships"].hull_source.build = recorded
    try:
        require(ship_module.read_hull_type(path, PACKAGES) == "Ship", "New Ship did not write a Ship hull")
        select(box)
        result, message = run(bpy.ops.aetheria.build_hull_source)
        require(result == {"CANCELLED"} and "the body's proportions lie inside the language's ranges" in message,
                f"A Ship far wider than its language allows was built: {message!r}")
        print("PASS 5.1 a body wider than the language's ships is refused as Ship, on proportion")

        store = ship_module.read(path, PACKAGES).store
        for envelope in store.pull_all():
            if envelope.type == ship_module.HULL_SCHEMA:
                values = msgpack.unpackb(envelope.payload, raw=False)
                values[ship_module.HULL_TYPE_SLOT] = 1
                store.push(dataclasses.replace(envelope, payload=msgpack.packb(values, use_bin_type=True)))
        require(ship_module.read_hull_type(path, PACKAGES) == "Station", "The pushed HullType did not read back as Station")
        select(box)
        result, message = run(bpy.ops.aetheria.build_hull_source)
        names = [c.name for c in judged[-1].checks]
        require(result == {"FINISHED"} and judged[-1].hull_type == "Station"
                and "the body's proportions lie inside the language's ranges" not in names
                and "the top view fits the layout grid" in names,
                f"The same body as Station was refused or judged on proportion: {message!r} {names}")
        print("PASS 5.2 the same body as Station is built, with no proportion check and a grid check")

        built = set(generated(collection))
        source.write_text(original + slab % (35, 70))
        select(box)
        result, message = run(bpy.ops.aetheria.build_hull_source)
        require(result == {"CANCELLED"} and "the top view fits the layout grid" in message
                and "the body's proportions lie inside the language's ranges" not in message
                and set(generated(collection)) == built,
                f"A Station 70 m wide was not refused on the layout grid, or the refusal changed the scene: {message!r}")
        print("PASS 5.3 a Station 70 m wide is refused on the layout grid, not on proportion")
    finally:
        sys.modules["aetheria_ships"].hull_source.build = real
    print("HULL_TYPE_GATE_OK", flush=True)
    return collection


def main():
    require(bpy.app.version[:2] == (5, 2), f"This smoke is for Blender 5.2; this is {bpy.app.version_string}")
    bpy.data.objects.remove(bpy.data.objects["Cube"], do_unlink=True)
    sys.path.insert(0, str(REPO / "tools" / "blender"))
    addon_utils.enable("aetheria_ships", default_set=True, handle_error=None)
    import aetheria_ships
    global hull_source, EXECUTABLE
    hull_source = aetheria_ships.hull_source
    aetheria_ships._brokkr_cultlib = lambda context: PACKAGES
    preferences = bpy.context.preferences.addons["aetheria_ships"].preferences
    preferences.game_folder = str(GAME)
    if OPENSCAD:
        preferences.openscad = OPENSCAD
    EXECUTABLE = preferences.openscad
    real = aetheria_ships.hull_source.openscad_runner

    def logged(executable, source, **kwargs):
        kwargs.pop("on_start", None)

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
    for number, (fixture, ship_id, extent, length, rows, body, palette, maker, other) in enumerate(HULLS, start=1):
        clear(previous)
        previous = one_hull(scene, number, fixture, ship_id, extent, length, rows, body, palette, maker, other)
    clear(previous)
    clear(agent_guards(scene))
    clear(hull_type_gate(scene))
    never_built(scene)
    nakovalnya_frames(EXECUTABLE)


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
    for _, ship_id, *_ in HULLS + ((None, "smoke.hull.unbuilt"), (None, "smoke.hull.guards"), (None, "smoke.hull.role")):
        shutil.rmtree(MODS / ship_id, ignore_errors=True)
    if created:
        shutil.rmtree(MODS, ignore_errors=True)
sys.stdout.flush()
os._exit(code)
