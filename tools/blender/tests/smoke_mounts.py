"""Headless Blender smoke for the add-on's weapon mounts, the gunboat path: Add Weapon, Add Muzzle, Mirror Mount, Remove
Mount, and that Package moves nothing of them.

    blender --background --factory-startup --python-use-system-env --python tools/blender/tests/smoke_mounts.py

Environment as smoke_package.py's header says: CULTLIB_PACKAGES and SMOKE_GAME_FOLDER (the published AetherDb under its
ModTools, no dotnet), and msgpack importable. The ship is created under <game folder>/GameData/Mods/smoke.mounts, which
this smoke removes when it ends.

The schematic and the model are separate spaces (ruling schematic-and-model-are-separate-spaces): a row's Position is
authored in the layout, a mount's pose in the model, and the two are linked by name alone. The ship is smoke_frame's
sphere, centred on the origin, through New Ship (6 x 10 cells, cell x = 0 at +X). Pass 1 adds a Ballistic gun on the
starboard flank as Clockwise: its row is made at HardpointData's default Position (the model point maps to no cell), the
smoke sets the Position as the panel would, and its muzzle sits 0.5 m toward -X of it. Mirror Mount makes a
CounterClockwise row at the footprint's reflection in the layout and the object's reflection in the model, with its
muzzle toward +X, and refuses a mount on the model's centreline and one whose footprint is its own reflection. Pass 2
packages: the Clockwise muzzle node in the GLB turns glTF +Z to glTF -X, and the mirrored one to +X. Pass 3 moves a
weapon in the model, hand-sets its yaw and changes another's Rotation: Package leaves every row's Position, in the buffer
and in the .cc, and every mount's yaw as authored. Pass 4 removes a mount with two muzzles: its row, the mount and both
muzzles go, and the other rows stay.
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
from mathutils import Vector

REPO = Path(__file__).resolve().parents[3]
PACKAGES = os.environ.get("CULTLIB_PACKAGES") or sys.exit("Set CULTLIB_PACKAGES to CultLib's packages directory")
SHIP_ID = "smoke.mounts"
ODD_ID = "smoke.mounts.odd"
# (ship id, length in cells, the source sphere's Y scale, wide, grid width parity) of passes 6 to 8; the id's mod directory
# is removed at the end.
WIDTH_PASSES = ((ODD_ID, 7, 0.6, False, 1), (ODD_ID + ".wide-even", 14, 1.0, True, 0), (ODD_ID + ".wide-odd", 15, 1.0, True, 1))
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


def set_footprint(scene, mount_id, footprint):
    next(hp for hp in scene.aetheria_layout.hardpoints if hp.mount_id == mount_id).footprint = footprint


def set_position(scene, mount_id, position):
    """The panel's edit: a row's Position is authored in the layout."""
    hp = next(h for h in scene.aetheria_layout.hardpoints if h.mount_id == mount_id)
    hp.x, hp.y = position


def root_local(root, obj):
    """obj's origin in Ship Root's frame."""
    return tuple(root.matrix_world.inverted() @ Vector(world(obj)))


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


def new_gun(scene, root, location, rotation=CLOCKWISE):
    """Add Weapon with the 3D cursor at location in Ship Root's frame; returns the new row's mount id."""
    before = {r[0] for r in rows(scene)}
    select(root)  # the active object names the ship collection
    scene.cursor.location = root.matrix_world @ Vector(location)
    result, message = run(lambda: bpy.ops.aetheria.add_ship_weapon(kind=BALLISTIC, rotation=str(rotation)))
    require(result == {"FINISHED"}, f"Add Weapon was refused: {message}")
    (mount_id,) = {r[0] for r in rows(scene)} - before
    return mount_id


def remove_mount(mount_id):
    select(bpy.data.objects[mount_id])
    require(run(bpy.ops.aetheria.remove_ship_mount)[0] == {"FINISHED"}, "Remove Mount was refused")


def mirror_matrix(scene, root, sphere):
    """Mirror Mount, each space within itself, for every footprint width 1 to 4 at every authored Position of the grid:
    a footprint on cells [a, a + w) of a W-wide grid has its twin row on [W - a - w, W - a), whatever the grid's parity,
    and the twin object is the model reflection of the source (-x, y, z), wherever the author put the row. A footprint
    that is its own reflection has no twin. The expected cells are computed here from the footprint, never read back
    from the twin."""
    state = scene.aetheria_layout
    grid_w = state.width
    y_cell = 1
    model_point = (1.5, -1.0, 0.2)  # off the centreline; it names no cell
    mirrored = refused_count = 0
    baseline = rows(scene)
    for w in (1, 2, 3, 4):
        for a in range(grid_w - w + 1):
            mount_id = new_gun(scene, root, model_point)
            set_footprint(scene, mount_id, "#" * w)
            set_position(scene, mount_id, (a, y_cell))
            select(bpy.data.objects[mount_id])
            before = rows(scene)
            expected = (grid_w - a - w, y_cell)
            if expected == (a, y_cell):
                refused(bpy.ops.aetheria.mirror_ship_mount, "no mirror image", "Mirror Mount of a footprint that is its own reflection")
                require(rows(scene) == before, "A refused Mirror Mount changed a row")
                refused_count += 1
            else:
                require(run(bpy.ops.aetheria.mirror_ship_mount)[0] == {"FINISHED"}, f"Mirror Mount was refused (width {w}, cell {a})")
                (twin_id,) = {r[0] for r in rows(scene)} - {r[0] for r in before}
                twin = row(scene, twin_id)
                require(twin[2] == expected, f"A {w}-wide footprint at cell {a} of {grid_w} mirrored to {twin[2]}, not {expected}")
                require(twin[4] == "#" * w, "The mirrored footprint is not the reflection of the footprint")
                reflected = (-model_point[0], model_point[1], model_point[2])
                require(approx(root_local(root, bpy.data.objects[twin_id]), reflected),
                        f"The twin object is not the model reflection: {root_local(root, bpy.data.objects[twin_id])}")
                mirrored += 1
                remove_mount(twin_id)
            remove_mount(mount_id)
    require(mirrored >= 6 and refused_count >= 1, f"The matrix ran too little ({mirrored}, {refused_count})")
    require(rows(scene) == baseline, "The matrix left rows behind")

    # The twin carries the author's armour and arc, rotation mirrored, and the muzzle's reflection.
    mount_id = new_gun(scene, root, model_point)
    set_position(scene, mount_id, (0, y_cell))
    hp = next(h for h in state.hardpoints if h.mount_id == mount_id)
    hp.armor, hp.firing_arc = 3.0, 45.0  # the author's numbers
    muzzle = bpy.data.objects[f"{mount_id}.muzzle.1"]
    muzzle.location.x = 0.3
    select(bpy.data.objects[mount_id])
    before_ids = {r[0] for r in rows(scene)}
    require(run(bpy.ops.aetheria.mirror_ship_mount)[0] == {"FINISHED"}, "Mirror Mount of a muzzled mount was refused")
    (twin_id,) = {r[0] for r in rows(scene)} - before_ids
    twin = row(scene, twin_id)
    require(twin[2] == (grid_w - 1, y_cell), f"The twin is at {twin[2]}")
    require(twin[3] == COUNTER_CLOCKWISE and twin[5:] == (3.0, 45.0), f"The twin's rotation or armour and arc are wrong: {twin}")
    gun_at, twin_at = world(bpy.data.objects[mount_id]), world(bpy.data.objects[twin_id])
    source_offset = tuple(m - g for m, g in zip(world(muzzle), gun_at))
    twin_offset = tuple(m - g for m, g in zip(world(bpy.data.objects[f"{twin_id}.muzzle.1"]), twin_at))
    require(approx(twin_offset, (-source_offset[0], source_offset[1], source_offset[2])),
            f"The mirrored muzzle is not the reflection of the source muzzle: {source_offset} vs {twin_offset}")
    remove_mount(twin_id)
    remove_mount(mount_id)

    # The model's centreline has no mirror image, whatever the row says; a millimetre is on it, two are not.
    for x, ok in ((0.0, False), (5e-4, False), (2e-3, True)):
        mount_id = new_gun(scene, root, (x, model_point[1], 0.0))
        set_position(scene, mount_id, (0, y_cell))
        select(bpy.data.objects[mount_id])
        before = rows(scene)
        if ok:
            require(run(bpy.ops.aetheria.mirror_ship_mount)[0] == {"FINISHED"}, "Mirror Mount just off the centreline was refused")
            (twin_id,) = {r[0] for r in rows(scene)} - {r[0] for r in before}
            remove_mount(twin_id)
        else:
            refused(bpy.ops.aetheria.mirror_ship_mount, "no mirror image", f"Mirror Mount of a mount {x} m off the centreline")
            require(rows(scene) == before, "A refused Mirror Mount changed a row")
        remove_mount(mount_id)


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
    require(gun_row == ("weapon.1", 10, (0, 0), CLOCKWISE, "#", 0.0, 0.0),
            f"The gun's row is not the default Position (the model point maps to no cell): {gun_row}")
    set_position(scene, "weapon.1", (4, 5))  # authored in the layout, as the panel would
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
    scene.cursor.location = (0.0, 0.0, 0.0)
    select(sphere)
    require(run(lambda: bpy.ops.aetheria.add_ship_weapon(kind=BALLISTIC, rotation="0"))[0] == {"FINISHED"},
            "Add Weapon on the centreline was refused")
    centre = bpy.data.objects["weapon.3"]
    select(centre)
    before = rows(scene)
    refused(bpy.ops.aetheria.mirror_ship_mount, "no mirror image", "Mirror Mount of a mount on the model's centreline")
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

    mirror_matrix(scene, root, sphere)
    print("PASS 5 mirror matrix ok")

    # A mirrored pair of two-cell footprints stays where Mirror Mount put it through Package and the validator.
    pair = new_gun(scene, root, (-1.0, 1.0, 0.0))
    set_footprint(scene, pair, "##")
    set_position(scene, pair, (3, 4))
    select(bpy.data.objects[pair])
    require(run(bpy.ops.aetheria.mirror_ship_mount)[0] == {"FINISHED"}, "Mirror Mount of the pair was refused")
    pair_rows = [r for r in rows(scene) if r[0] not in ("weapon.1", "weapon.2") and r[0].startswith("weapon.")]
    require(sorted(r[2] for r in pair_rows) == [(1, 4), (3, 4)], f"The pair is not a reflection: {pair_rows}")

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
    require([r for r in rows(scene) if r in pair_rows] == pair_rows, "Package moved a mirrored pair")
    print("PASS 2 package and glb ok")

    # Pass 3: Package moves nothing. A mount moved in the model, a hand-set yaw and a changed Rotation stay as authored.
    gun, twin = bpy.data.objects["weapon.1"], bpy.data.objects["weapon.2"]
    gun.location.y += 4.0  # two cells' worth aft in the model; the row is the author's
    gun.rotation_euler.z = 0.3  # a hand-set facing
    twin_yaw = twin.rotation_euler.z
    for hp in scene.aetheria_layout.hardpoints:
        if hp.mount_id == "weapon.2":
            hp.rotation = str(REVERSED)
    select(sphere)
    require(run(bpy.ops.aetheria.save_ship_layout)[0] == {"FINISHED"}, "Save before the second Package was refused")
    authored = rows(scene)
    result, message = run(bpy.ops.aetheria.package_ship)
    report = scene.aetheria_package_report
    require(result == {"FINISHED"}, f"Package refused the moved ship: {report}")
    require(rows(scene) == authored, "Package changed a row in the buffer")
    require(row(scene, "weapon.1")[2] == (4, 5) and row(scene, "weapon.2")[2] == (1, 5), "Package moved a weapon's Position")
    require(row(scene, "weapon.2")[3] == REVERSED and row(scene, "reactor.a") == reactor_before, "Package changed a Rotation or an internal row")
    require(abs(gun.rotation_euler.z - 0.3) < 1e-6, f"Package turned a hand-set yaw: {gun.rotation_euler.z}")
    require(abs(twin.rotation_euler.z - twin_yaw) < 1e-6, f"Package turned a mount to its row's Rotation: {twin.rotation_euler.z}")
    saved = aetheria_ships.ship_cc.read_layout(path, PACKAGES)[1]
    by_mount = {fields["Transform"]: fields for fields in map(aetheria_ships.ship_cc.decode_hardpoint, saved)}
    require(tuple(by_mount["weapon.1"]["Position"]) == (4, 5) and tuple(by_mount["weapon.2"]["Position"]) == (1, 5)
            and by_mount["weapon.2"]["Rotation"] == REVERSED and tuple(by_mount["reactor.a"]["Position"]) == (2, 2),
            "The .cc does not hold what the author left")
    require([r for r in rows(scene) if r in pair_rows] == pair_rows, "The second Package moved a mirrored pair")
    print("PASS 3 package moves nothing ok")

    # Passes 6 to 8: the same mirror matrix on ships whose grids are odd, wide and even, and wide and odd (10 or more
    # cells across, where real hulls live). Each pass clears the previous ship's objects, which hold the names the matrix
    # looks mounts up by.
    previous = collection
    for number, (ship_id, length, y_scale, wide, parity) in enumerate(WIDTH_PASSES, start=6):
        previous = matrix_pass(scene, previous, ship_id, length, y_scale, wide, parity)
        print(f"PASS {number} mirror matrix on a {scene.aetheria_layout.width}-wide grid ok")


def matrix_pass(scene, previous, ship_id, length, y_scale, wide, parity):
    for obj in list(previous.all_objects):
        bpy.data.objects.remove(obj)
    ship = bpy.data.collections.new(f"{COLLECTION} {ship_id}")
    scene.collection.children.link(ship)
    mesh = bpy.data.meshes.new(f"{ship_id} sphere")
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=48, v_segments=24, radius=0.5)
    bm.to_mesh(mesh)
    bm.free()
    sphere = new_object(ship, f"{ship_id} Source Sphere", mesh)
    sphere.scale = (1, y_scale, 0.4)
    select(sphere)
    scene.aetheria_new_ship_id, scene.aetheria_new_ship_name = ship_id, ship_id
    scene.aetheria_new_ship_like, scene.aetheria_new_ship_length = "Djinni", length
    result, message = run(bpy.ops.aetheria.new_ship)
    require(result == {"FINISHED"}, f"New Ship ({ship_id}) was refused: {message}")
    result, message = run(bpy.ops.aetheria.save_ship_layout)
    require(result == {"FINISHED"}, f"Save ({ship_id}) was refused: {message}")
    width = scene.aetheria_layout.width
    require(width % 2 == parity and (width >= 10 or not wide), f"The {ship_id} grid is {width} wide")
    mirror_matrix(scene, sys.modules["aetheria_ships"]._ship_root(ship), sphere)
    return ship



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
    if not existed:
        for pass_id, *_ in WIDTH_PASSES:
            shutil.rmtree(MODS / pass_id, ignore_errors=True)
    if created_mods and not existed:
        shutil.rmtree(MODS, ignore_errors=True)
sys.stdout.flush()
os._exit(code)
