"""Headless Blender smoke for the add-on's weapon mounts, the gunboat path: Add Weapon, Add Muzzle, Mirror Mount, Remove
Mount, and what Package derives from them.

    blender --background --factory-startup --python-use-system-env --python tools/blender/tests/smoke_mounts.py

Environment as smoke_package.py's header says: CULTLIB_PACKAGES and SMOKE_GAME_FOLDER (the published AetherDb under its
ModTools, no dotnet), and msgpack importable. The ship is created under <game folder>/GameData/Mods/smoke.mounts, which
this smoke removes when it ends.

The ship is smoke_frame's sphere, centred on the origin, through New Ship (6 x 10 cells, cell x = 0 at +X). Pass 1 adds a
Ballistic gun on the starboard flank as Clockwise: its row's Rotation is 3, its Position is the cell under the mount, and
its muzzle sits 0.5 m toward -X of it. Mirror Mount makes a CounterClockwise row at the mirrored cell with its muzzle
toward +X, and refuses a mount on the centreline. Pass 2 packages: the Clockwise muzzle node in the GLB turns glTF +Z to
glTF -X, and the mirrored one to +X. Pass 3 moves a weapon two cells by script and sets another's Rotation to Reversed in
the panel state: Package moves the first row's Position by two cells, leaves the internal Reactor row alone, yaws the
second mount to 180 degrees and the first back to its Rotation's yaw, and the validator accepts the ship. Pass 4 removes a
mount with two muzzles: its row, the mount and both muzzles go, and the other rows stay.
"""

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

REPO = Path(__file__).resolve().parents[3]
PACKAGES = os.environ.get("CULTLIB_PACKAGES") or sys.exit("Set CULTLIB_PACKAGES to CultLib's packages directory")
SHIP_ID = "smoke.mounts"
GAME = Path(os.environ.get("SMOKE_GAME_FOLDER") or sys.exit("Set SMOKE_GAME_FOLDER to a folder holding GameData/Aetheria.cc and ModTools/AetherDb"))
MODS = GAME / "GameData" / "Mods"
COLLECTION = "Smoke Mounts"
NAMES_NOT_ECHOED = (SHIP_ID, COLLECTION, "weapon.", str(GAME), GAME.name)
CLOCKWISE, COUNTER_CLOCKWISE, REVERSED = 3, 1, 2
BALLISTIC = "10"


def new_object(collection, name, mesh, location=(0, 0, 0), parent=None):
    obj = bpy.data.objects.new(name, mesh)
    obj.location = location
    obj.parent = parent
    collection.objects.link(obj)
    return obj


def tag(obj, role, anchor_id):
    obj["aetheria.role"] = role
    obj["aetheria.id"] = anchor_id
    return obj


def quad(name, size):
    mesh = bpy.data.meshes.new(name)
    half = size / 2
    mesh.from_pydata([(-half, -half, 0), (half, -half, 0), (half, half, 0), (-half, half, 0)], [], [(0, 1, 2, 3)])
    return mesh


def tetrahedron(name):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([(0, 0, 0), (1, 0, 0), (0, 1, 0), (0, 0, 1)], [], [(0, 2, 1), (0, 1, 3), (0, 3, 2), (1, 2, 3)])
    return mesh


def run(operator):
    try:
        return operator(), ""
    except RuntimeError as exc:
        print(f"OPERATOR error: {exc}")
        return {"CANCELLED"}, str(exc)


def require(condition, message):
    if not condition:
        raise SystemExit(message)


def refused(operator, expected, what):
    result, message = run(operator)
    require(result == {"CANCELLED"}, f"{what} was not refused")
    require(expected in message, f"{what}: the report is not the fixed text ({message!r})")
    for name in NAMES_NOT_ECHOED:
        require(name not in message, f"{what}: the report echoes an input ({name})")


def select(obj):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)


def approx(a, b, tolerance=1e-3):
    return all(abs(x - y) <= tolerance for x, y in zip(a, b))


def rows(scene):
    return [(hp.mount_id, int(hp.kind), (hp.x, hp.y), int(hp.rotation), hp.footprint, hp.armor, hp.firing_arc)
            for hp in scene.aetheria_layout.hardpoints]


def row(scene, mount_id):
    found = [r for r in rows(scene) if r[0] == mount_id]
    require(len(found) == 1, f"Expected one row for a mount, found {len(found)}")
    return found[0]


def world(obj):
    bpy.context.view_layer.update()
    return tuple(obj.matrix_world.translation)


def glb_world_rotation_of(path, name):
    """The GLB node's world rotation applied to glTF +Z."""
    data = Path(path).read_bytes()
    length, kind = struct.unpack_from("<II", data, 12)
    require(kind == 0x4E4F534A, "The GLB's first chunk is not JSON")
    nodes = json.loads(data[20:20 + length])["nodes"]
    parents = {child: index for index, node in enumerate(nodes) for child in node.get("children", [])}
    index = next((i for i, node in enumerate(nodes) if node.get("name") == name), None)
    require(index is not None, "The GLB has no node for the muzzle")

    def multiply(a, b):
        ax, ay, az, aw = a
        bx, by, bz, bw = b
        return (aw * bx + ax * bw + ay * bz - az * by, aw * by - ax * bz + ay * bw + az * bx,
                aw * bz + ax * by - ay * bx + az * bw, aw * bw - ax * bx - ay * by - az * bz)

    chain = []
    while index is not None:
        chain.append(nodes[index].get("rotation", (0.0, 0.0, 0.0, 1.0)))
        index = parents.get(index)
    q = (0.0, 0.0, 0.0, 1.0)
    for rotation in reversed(chain):
        q = multiply(q, rotation)
    x, y, z, w = q
    # q v q^-1 for v = (0, 0, 1)
    return (2 * (x * z + w * y), 2 * (y * z - w * x), 1 - 2 * (x * x + y * y))


def main():
    require(bpy.app.version[:2] == (5, 2), f"This smoke is for Blender 5.2; this is {bpy.app.version_string}")
    bpy.data.objects.remove(bpy.data.objects["Cube"], do_unlink=True)
    sys.path.insert(0, str(REPO / "tools" / "blender"))
    addon_utils.enable("aetheria_ships", default_set=True, handle_error=None)
    import aetheria_ships
    aetheria_ships._brokkr_cultlib = lambda context: PACKAGES
    bpy.context.preferences.addons["aetheria_ships"].preferences.game_folder = str(GAME)
    require(not (MODS / SHIP_ID).exists(), f"{MODS / SHIP_ID} exists; remove it first")
    path = str(MODS / SHIP_ID / "ship.cc")

    scene = bpy.context.scene
    collection = bpy.data.collections.new(COLLECTION)
    scene.collection.children.link(collection)
    sphere_mesh = bpy.data.meshes.new("Sphere")
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=48, v_segments=24, radius=0.5)
    bm.to_mesh(sphere_mesh)
    bm.free()
    sphere = new_object(collection, "Source Sphere", sphere_mesh)
    sphere.scale = (1, 0.6, 0.4)
    select(sphere)
    scene.aetheria_new_ship_id, scene.aetheria_new_ship_name = SHIP_ID, "Smoke Mounts"
    scene.aetheria_new_ship_like, scene.aetheria_new_ship_length = "Djinni", 10
    result, message = run(bpy.ops.aetheria.new_ship)
    require(result == {"FINISHED"}, f"New Ship was refused: {message}")
    root = bpy.data.objects["Ship Root"]
    state = scene.aetheria_layout
    require((state.width, state.height) == (6, 10), f"The grid is {state.width}x{state.height}")
    origin = collection["aetheria.grid_origin"]
    require(approx((origin[0], origin[1]), (5.0, 9.0), 0.2), f"The hull is not centred on X = 0: {tuple(origin)}")

    # The packaged ship needs the four anchors Package does not make, and a drive.
    tag(new_object(collection, "Map Icon", quad("Map Icon", 1.0), (0, 0, -1), root), "map-icon", "map")
    tag(new_object(collection, "Hull Collider", tetrahedron("Hull Collider"), parent=root), "hull-collider", "collider")
    tag(new_object(collection, "Shield", None, parent=root), "shield", "shield")
    tag(new_object(collection, "Tractor", None, (0, 2, 0), root), "tractor", "tractor")
    disc = new_object(collection, "Thruster", quad("Thruster", 0.4), (0, 8, 0), root)
    tag(disc, "thruster-emitter", "thruster")
    for mount, kind, x, y in (("thruster", "3", 2, 0), ("reactor.a", "5", 2, 2)):
        bpy.ops.aetheria.add_ship_hardpoint()
        hp = state.hardpoints[len(state.hardpoints) - 1]
        hp.mount_id, hp.kind, hp.x, hp.y, hp.footprint = mount, kind, x, y, "#"
    result, message = run(bpy.ops.aetheria.save_ship_layout)
    require(result == {"FINISHED"}, f"Save was refused: {message}")
    reactor_before = row(scene, "reactor.a")
    select(sphere)

    # Pass 1: Add Weapon on the starboard flank, Clockwise; Mirror Mount.
    scene.cursor.location = (-3.0, -1.0, 0.0)
    result, message = run(lambda: bpy.ops.aetheria.add_ship_weapon(kind=BALLISTIC, rotation=str(CLOCKWISE)))
    require(result == {"FINISHED"}, f"Add Weapon was refused: {message}")
    gun = bpy.data.objects["weapon.1"]
    require(gun.get("aetheria.role") == "weapon-mount" and gun.parent == root, "The mount is not a tagged child of Ship Root")
    gun_row = row(scene, "weapon.1")
    require(gun_row == ("weapon.1", 10, (4, 5), CLOCKWISE, "#", 0.0, 0.0), f"The gun's row is wrong: {gun_row}")
    muzzles = [o for o in bpy.data.objects if o.get("aetheria.role") == "weapon-muzzle"]
    require([m.get("aetheria.id") for m in muzzles] == ["weapon.1.muzzle.1"] and muzzles[0].parent == gun
            and muzzles[0].get("aetheria.order") == 1, "Add Weapon did not make its one ordered muzzle")
    gun_at, muzzle_at = world(gun), world(muzzles[0])
    require(approx(gun_at, (-3.0, -1.0, 0.0)), f"The mount is not at the cursor: {gun_at}")
    require(approx(tuple(m - g for m, g in zip(muzzle_at, gun_at)), (-0.5, 0.0, 0.0)),
            "The Clockwise muzzle is not 0.5 m toward -X of its mount")

    select(gun)
    result, message = run(bpy.ops.aetheria.mirror_ship_mount)
    require(result == {"FINISHED"}, f"Mirror Mount was refused: {message}")
    twin = bpy.data.objects["weapon.2"]
    twin_row = row(scene, "weapon.2")
    require(twin_row == ("weapon.2", 10, (1, 5), COUNTER_CLOCKWISE, "#", 0.0, 0.0), f"The mirrored row is wrong: {twin_row}")
    twin_muzzle = bpy.data.objects["weapon.2.muzzle.1"]
    twin_at = world(twin)
    require(approx(twin_at, (3.0, -1.0, 0.0)), f"The mirrored mount is not across the centreline: {twin_at}")
    require(approx(tuple(m - t for m, t in zip(world(twin_muzzle), twin_at)), (0.5, 0.0, 0.0)),
            "The mirrored muzzle does not point toward +X")
    scene.cursor.location = (0.2, 0.0, 0.0)
    select(sphere)
    require(run(lambda: bpy.ops.aetheria.add_ship_weapon(kind=BALLISTIC, rotation="0"))[0] == {"FINISHED"},
            "Add Weapon on the centreline was refused")
    centre = bpy.data.objects["weapon.3"]
    select(centre)
    before = rows(scene)
    refused(bpy.ops.aetheria.mirror_ship_mount, "no mirror image", "Mirror Mount of a centreline mount")
    require(rows(scene) == before and bpy.data.objects.get("weapon.4") is None, "A refused Mirror Mount changed something")
    print("PASS 1 add weapon and mirror ok")

    # Pass 4 first, on the centreline mount (it must not reach Package): Add Muzzle, then Remove Mount.
    select(centre)
    require(run(bpy.ops.aetheria.add_ship_muzzle)[0] == {"FINISHED"}, "Add Muzzle was refused")
    second = bpy.data.objects.get("weapon.3.muzzle.2")
    require(second is not None and second.parent == centre and second.get("aetheria.order") == 2,
            "Add Muzzle did not make the next ordered muzzle under the mount")
    others = [r for r in rows(scene) if r[0] != "weapon.3"]
    select(centre)
    require(run(bpy.ops.aetheria.remove_ship_mount)[0] == {"FINISHED"}, "Remove Mount was refused")
    require(bpy.data.objects.get("weapon.3") is None and bpy.data.objects.get("weapon.3.muzzle.1") is None
            and bpy.data.objects.get("weapon.3.muzzle.2") is None, "Remove Mount left the mount or a muzzle")
    require(rows(scene) == others, "Remove Mount changed a row besides its own")
    print("PASS 4 remove mount ok")

    # Pass 2: Package; the GLB's muzzle nodes face outboard.
    select(sphere)
    result, message = run(bpy.ops.aetheria.save_ship_layout)
    require(result == {"FINISHED"}, f"Save before Package was refused: {message}")
    result, message = run(bpy.ops.aetheria.package_ship)
    report = scene.aetheria_package_report
    require(result == {"FINISHED"}, f"Package refused the gunboat ship: {report}")
    glb = Path(path).with_name("ship.glb")
    starboard = glb_world_rotation_of(glb, "weapon.1.muzzle.1")
    port = glb_world_rotation_of(glb, "weapon.2.muzzle.1")
    require(approx(starboard, (-1.0, 0.0, 0.0)), f"The Clockwise muzzle node does not turn glTF +Z to -X: {starboard}")
    require(approx(port, (1.0, 0.0, 0.0)), f"The mirrored muzzle node does not turn glTF +Z to +X: {port}")
    print("PASS 2 package and glb ok")

    # Pass 3: a moved mount and a changed Rotation; Package derives Position and yaw from them.
    gun, twin = bpy.data.objects["weapon.1"], bpy.data.objects["weapon.2"]
    gun.location.y += 4.0  # two cells aft
    gun.rotation_euler.z = 0.3  # a hand-set facing: Package overwrites it from the row
    for hp in scene.aetheria_layout.hardpoints:
        if hp.mount_id == "weapon.2":
            hp.rotation = str(REVERSED)
    select(sphere)
    require(run(bpy.ops.aetheria.save_ship_layout)[0] == {"FINISHED"}, "Save before the second Package was refused")
    require(row(scene, "weapon.1")[2] == (4, 5), "The buffer's Position moved before Package")
    result, message = run(bpy.ops.aetheria.package_ship)
    report = scene.aetheria_package_report
    require(result == {"FINISHED"}, f"Package refused the moved ship: {report}")
    require(row(scene, "weapon.1")[2] == (4, 3), f"Package did not move the row two cells: {row(scene, 'weapon.1')[2]}")
    require(row(scene, "weapon.2")[2] == (1, 5), "Package moved a mount that did not move")
    require(row(scene, "reactor.a") == reactor_before, "Package changed an internal row")
    require(abs(twin.rotation_euler.z - math.pi) < 1e-6 or abs(twin.rotation_euler.z + math.pi) < 1e-6,
            f"The Reversed mount's yaw is not 180 degrees: {twin.rotation_euler.z}")
    require(abs(gun.rotation_euler.z + math.pi / 2) < 1e-6, f"Package kept a hand-set yaw: {gun.rotation_euler.z}")
    saved = aetheria_ships.ship_cc.read_layout(path, PACKAGES)[1]
    by_mount = {fields["Transform"]: fields for fields in map(aetheria_ships.ship_cc.decode_hardpoint, saved)}
    require(tuple(by_mount["weapon.1"]["Position"]) == (4, 3) and by_mount["weapon.2"]["Rotation"] == REVERSED
            and tuple(by_mount["reactor.a"]["Position"]) == (2, 2), "The .cc does not hold what Package derived")
    print("PASS 3 package derives ok")


created_mods = not MODS.exists()
existed = (MODS / SHIP_ID).exists()
try:
    main()
except SystemExit as stop:
    print(f"SMOKE_MOUNTS_FAILED: {stop}", flush=True)
    code = 1
except Exception:
    traceback.print_exc()
    print("SMOKE_MOUNTS_FAILED", flush=True)
    code = 1
else:
    print("SMOKE_MOUNTS_OK", flush=True)
    code = 0
finally:
    if not existed:
        shutil.rmtree(MODS / SHIP_ID, ignore_errors=True)
    if created_mods and not existed:
        shutil.rmtree(MODS, ignore_errors=True)
sys.stdout.flush()
os._exit(code)
