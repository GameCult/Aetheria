"""Live proof, in GUI Blender 5.3 with Brokkr installed as an extension and aetheria_ships in the scripts folder, that the
add-on's brokkr_actions.json gives agents Open and Push and nothing else: Open is one undo step, Push writes nothing and
returns AetherDb's change lines as hostResult (also in Edit Mode), and the operators that write game data stay human-only.

  BROKKR_SCRATCH=<scratch dir: the blend file is saved here>
  BROKKR_CULTLIB=<CultLib packages dir>
  SMOKE_GAME_FOLDER=<a game folder holding GameData/Aetheria.cc and ModTools/AetherDb.exe (hull_packager/build_aetherdb.ps1)>
  BROKKR_ADDON_MODULE=<default bl_ext.user_default.brokkr_bridge, the extension install>
  LIVE_SHOT=<optional png path: the 3D view with the Brokkr sidebar, saved from this window when the checks end>
    blender --enable-event-simulate --python live_authoring_surface.py -- <out>

Run it in Blender with private BLENDER_USER_CONFIG, BLENDER_USER_EXTENSIONS and BLENDER_USER_SCRIPTS directories (Blender
5.3 ignores BLENDER_USER_RESOURCE_PATH). The script refuses to run when they are unset or are not the folders Blender
uses. It copies this repo's aetheria_ships into BLENDER_USER_SCRIPTS/addons and enables it there, makes the ship
live.surface under <game folder>/GameData/Mods and removes it at the end. Every intent is put into the node database as
brokkr-ctl does and answered by the add-on's own 1 s drain. wm.undo_stack is the counter. Proves:

  * after the human's Reload Host Actions the snapshot lists exactly aetheria.open_ship and aetheria.push_ship (host
    aetheria_ships) and no hostActionErrors;
  * open is accepted as exactly one new undo step with the buffer filled from the .cc, and one undo restores the buffer;
  * push after a buffer edit is accepted with no new step, hostResult equal to wm().aetheria_push_result and holding the
    'armor: a -> b' line, ship.cc byte-identical; push is also accepted while an object is in Edit Mode, where open is denied;
  * intents naming aetheria.apply_ship, package_ship, capture_ship_lines and new_ship are denied Unsupported, no step,
    ship.cc byte-identical; the driver's own bpy.ops.aetheria.apply_ship, outside a turn, lands the reviewed push.

Writes <out>.result.json, then quits Blender.
"""
import hashlib
import importlib
import json
import os
import re
import shutil
import sys
import time
import traceback
from pathlib import Path

import bpy

for _name in ("BLENDER_USER_CONFIG", "BLENDER_USER_EXTENSIONS", "BLENDER_USER_SCRIPTS"):
    if not os.environ.get(_name):  # this Blender ignores BLENDER_USER_RESOURCE_PATH: without these, her own folders are used
        raise SystemExit(f"{_name} is not set: run this in private BLENDER_USER_CONFIG, BLENDER_USER_EXTENSIONS and BLENDER_USER_SCRIPTS directories")
for _name, _kind in (("BLENDER_USER_EXTENSIONS", "EXTENSIONS"), ("BLENDER_USER_SCRIPTS", "SCRIPTS")):
    if os.path.normcase(os.path.abspath(bpy.utils.user_resource(_kind))) != os.path.normcase(os.path.abspath(os.environ[_name])):
        raise SystemExit(f"Blender's {_kind.lower()} folder is not {_name}; refusing to touch it")

SCRATCH = Path(os.environ["BROKKR_SCRATCH"]).resolve()
SCRATCH.mkdir(parents=True, exist_ok=True)
GAME = Path(os.environ["SMOKE_GAME_FOLDER"]).resolve()
OUT = sys.argv[sys.argv.index("--") + 1]
SHOT = os.environ.get("LIVE_SHOT")
MODULE = os.environ.get("BROKKR_ADDON_MODULE", "bl_ext.user_default.brokkr_bridge")
HOST = "aetheria_ships"
SHIP_ID, COLLECTION = "live.surface", "Live Surface"
SHIP_FOLDER = GAME / "GameData" / "Mods" / SHIP_ID
SHIP_CC = SHIP_FOLDER / "ship.cc"
SOURCE = Path(__file__).resolve().parents[1] / HOST
R = {"blender": bpy.app.version_string, "module": MODULE, "checks": []}

addons_dir = Path(os.environ["BLENDER_USER_SCRIPTS"]) / "addons"
addons_dir.mkdir(parents=True, exist_ok=True)
if (addons_dir / HOST).exists():
    shutil.rmtree(addons_dir / HOST)
shutil.copytree(SOURCE, addons_dir / HOST, ignore=shutil.ignore_patterns("__pycache__"))
bpy.utils.refresh_script_paths()

bpy.ops.preferences.addon_enable(module=MODULE)
bpy.ops.preferences.addon_enable(module=HOST)
addon = importlib.import_module(MODULE)
bt = importlib.import_module(MODULE + ".blender_target")
ships = importlib.import_module(HOST)
prefs = bpy.context.preferences.addons[MODULE].preferences
bpy.context.preferences.addons[HOST].preferences.game_folder = str(GAME)
bpy.ops.wm.save_as_mainfile(filepath=str(SCRATCH / "live.blend"))
CACHE = str(SCRATCH / "queue.ccmp")
prefs.cultmesh_cache_path = CACHE
prefs.debug_mirror_root = ""
prefs.auto_drain_commands = True  # the sink
prefs.cultlib_py_src = os.environ.get("BROKKR_CULTLIB", prefs.cultlib_py_src)
adapter = addon.target()
bpy.ops.brokkr.reload_host_actions()  # the human's button: the host file is read here



def scene():  # read fresh every time: an undo step replaces the data-blocks
    return bpy.context.scene


def wm():
    return bpy.context.window_manager


def state():
    return scene().aetheria_layout


def check(name, ok, detail=None):
    R["checks"].append({"name": name, "ok": bool(ok), "detail": detail})


def win():
    return bpy.context.temp_override(window=wm().windows[0])


def steps_now():
    return [s.name for s in wm().undo_stack.steps]


def undo():
    with win():
        bpy.ops.ed.undo()


def redo():
    with win():
        bpy.ops.ed.redo()


def digest():
    return hashlib.sha256(SHIP_CC.read_bytes()).hexdigest()


def armor():
    return state().hardpoints[0].armor


def node():
    return adapter._open_node(CACHE, prefs.cultlib_py_src)


N = [0]
RUN = "%x" % int(time.time())  # the queue file outlives a run: an id is single-use, so a rerun needs ids of its own


def queue(**cmd):
    N[0] += 1
    cid = "surface-%s-%04d" % (RUN, N[0])
    n, docs = node()
    cmd.update(commandId=cid, schema=bt.COMMAND_INTENT_SCHEMA, requestedBy="nibu/hands")
    n.database.put(docs["command_intent"], f"blender/commands/{cid}", cmd)
    return cid


def receipt(cid):
    n, _ = node()
    return n.database.snapshot().get(bt.COMMAND_RECEIPT_SCHEMA, {}).get(f"blender/receipts/{cid}")


S = {}
WAIT = {"ticks": 0}


def until(pred, limit=60):
    def step():
        WAIT["ticks"] += 1
        if pred():
            WAIT["ticks"] = 0
            return True
        if WAIT["ticks"] > limit:
            raise TimeoutError("the drain did not receipt in time")
        return False
    return step


def once(fn):
    def step():
        fn()
        return True
    return step


def send(key, **fields):
    S[key + ":before"] = {"steps": steps_now(), "digest": digest()}
    S[key] = queue(**fields)


def done(key):
    return lambda: receipt(S[key]) is not None


def make_ship():
    """The ship collection, applied through the human's buttons: a pending ship, then one hardpoint."""
    if SHIP_FOLDER.exists():
        shutil.rmtree(SHIP_FOLDER)
    collection = bpy.data.collections.new(COLLECTION)
    scene().collection.children.link(collection)
    mesh = bpy.data.meshes.new("Live Hull")
    corners = [(sx * 1.0, sy * 3.0, sz * 0.5) for sx in (-1, 1) for sy in (-1, 1) for sz in (-1, 1)]
    mesh.from_pydata(corners, [], [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)])
    hull = bpy.data.objects.new("Live Hull", mesh)
    collection.objects.link(hull)
    bpy.context.view_layer.objects.active = hull
    hull.select_set(True)
    scene().aetheria_new_ship_id, scene().aetheria_new_ship_name = SHIP_ID, "Live Surface"
    scene().aetheria_new_ship_like, scene().aetheria_new_ship_length = "Djinni", 10
    bpy.ops.aetheria.new_ship()
    bpy.ops.aetheria.push_ship()
    bpy.ops.aetheria.apply_ship()
    bpy.ops.aetheria.add_ship_hardpoint()
    hp = state().hardpoints[0]
    hp.mount_id, hp.kind, hp.x, hp.y, hp.footprint, hp.armor, hp.firing_arc = "mount.live", "5", 1, 2, "#", 5.0, 90.0
    bpy.ops.aetheria.push_ship()
    bpy.ops.aetheria.apply_ship()


make_ship()


def snapshot_check():
    R["windows"] = len(wm().windows)
    R["undo_steps_at_start"] = steps_now()
    snapshot = adapter.capture_snapshot(bpy.context, True)
    rows = snapshot["hostActions"]
    expect = sorted(f"{HOST[:-6] if False else 'aetheria'}.{n}" for n in ("open_ship", "push_ship"))
    check("the snapshot lists exactly aetheria.open_ship and aetheria.push_ship, host aetheria_ships",
          sorted(r["name"] for r in rows) == expect and {r["host"] for r in rows} == {HOST}, [r["name"] for r in rows])
    check("the snapshot carries no hostActionErrors", snapshot["hostActionErrors"] == [], snapshot["hostActionErrors"])


def edit_armor():
    S["armor:old"] = armor()
    state().hardpoints[0].armor = 9.0
    S["edited"] = json.dumps(ships._layout_section(state()), sort_keys=True)
    with win():
        bpy.ops.ed.undo_push(message="Her armor edit")  # the step the UI pushes for her own edit: what one undo returns to


def open_check():
    r = receipt(S["open"])
    before = S["open:before"]
    after = steps_now()
    check("open is accepted", r["status"] == "accepted", r)
    check("open is exactly one new undo step carrying the receipt's label",
          len(after) == len(before["steps"]) + 1 and after[-1] == r["undoLabel"] and r["undoLabel"].startswith("Brokkr nibu/hands: aetheria.open_ship "),
          {"before": before["steps"][-2:], "after": after[-2:], "label": r["undoLabel"]})
    check("open filled the buffer from the .cc: the edited armor is gone", abs(armor() - S["armor:old"]) < 1e-6, armor())
    check("open wrote nothing", digest() == before["digest"])
    undo()
    check("one undo restores the buffer before the open", abs(armor() - 9.0) < 1e-6, armor())


def push_check():
    r = receipt(S["push"])
    before = S["push:before"]
    check("push is accepted", r["status"] == "accepted", r)
    check("push pushed no undo step", steps_now() == before["steps"] and r["undoLabel"] == "", [steps_now()[-2:], before["steps"][-2:]])
    check("hostResult equals wm.aetheria_push_result", r["hostResult"] == wm().aetheria_push_result and r["hostResult"] != "", r["hostResult"])
    check("hostResult holds the armor change line and the dry-run summary",
          re.search(r"armor: 5 -> 9", r["hostResult"]) and r["hostResult"].splitlines()[-1].startswith("dry-run"), r["hostResult"])
    check("push left ship.cc byte-identical and the buffer as edited",
          digest() == before["digest"] and json.dumps(ships._layout_section(state()), sort_keys=True) == S["edited"])


def enter_edit_mode():
    hull = bpy.data.objects["Live Hull"]
    bpy.context.view_layer.objects.active = hull
    with win():
        bpy.ops.object.mode_set(mode="EDIT")
    check("an object is in Edit Mode", bpy.data.objects["Live Hull"].mode == "EDIT", bpy.data.objects["Live Hull"].mode)


def leave_edit_mode():
    with win():
        bpy.ops.object.mode_set(mode="OBJECT")


def edit_push_check():
    r = receipt(S["epush"])
    check("push is accepted in Edit Mode", r["status"] == "accepted" and "armor: 5 -> 9" in r["hostResult"], r)
    check("and wrote nothing", steps_now() == S["epush:before"]["steps"] and digest() == S["epush:before"]["digest"])


def edit_open_check():
    r = receipt(S["eopen"])
    check("open is denied in Edit Mode", r["status"] == "denied", r)
    check("and changed nothing", steps_now() == S["eopen:before"]["steps"] and abs(armor() - 9.0) < 1e-6)


def denied_check(key, action):
    def run():
        r = receipt(S[key])
        before = S[key + ":before"]
        check(f"{action} is denied Unsupported", r["status"] == "denied" and "Unsupported Blender command action" in r["message"], r)
        check(f"{action} ran nothing: no step, ship.cc byte-identical", steps_now() == before["steps"] and digest() == before["digest"])
    return run


def human_apply():
    before = digest()
    bpy.ops.aetheria.push_ship()
    lines = wm().aetheria_push_result.splitlines()
    check("the human's own Push shows the armor change", any("armor: 5 -> 9" in line for line in lines), lines)
    S["human_before"] = before
    bpy.ops.aetheria.apply_ship()
    check("the human's Apply, outside a turn, lands the reviewed push", digest() != before, wm().aetheria_push_result)
    check("and the .cc holds the new armor", ships._open(bpy.context, str(SHIP_CC))["layout"]["hardpoints"][0]["armor"] == 9.0)


def view3d():
    area = next(a for a in bpy.context.screen.areas if a.type == "VIEW_3D")
    return area, next(r for r in area.regions if r.type == "UI"), next(r for r in area.regions if r.type == "WINDOW")


def stage_shot():
    """Leaves the panel as she would see it after an agent's push: an edit, then the dry run in the panel, the Brokkr tab
    open. The tab is chosen and the sidebar widened by simulated events (Blender run with --enable-event-simulate)."""
    state().hardpoints[0].armor = 12.0
    bpy.ops.aetheria.push_ship()
    area, ui, _ = view3d()
    area.spaces.active.show_region_ui = True
    area.tag_redraw()
    R["shot"] = {"category_before": ui.active_panel_category, "ui": [ui.x, ui.y, ui.width, ui.height]}


def simulate(**event):
    window = wm().windows[0]
    window.event_simulate(**event)


def tab_point():
    area, ui, _ = view3d()
    tabs = ui.x + ui.width - 15  # the tab strip is the sidebar's right edge; Brokkr is the last of five tabs
    return tabs, area.y + area.height - 295


def sim(type, value="NOTHING", **where):
    x, y = tab_point()
    wm().windows[0].event_simulate(type=type, value=value, x=where.get("x", x), y=where.get("y", y))


def widen(step):
    area, ui, _ = view3d()
    edge, y = ui.x + 1, ui.y + ui.height // 2
    if step == 0:
        sim("MOUSEMOVE", x=edge, y=y)
    elif step == 1:
        sim("LEFTMOUSE", "PRESS", x=edge, y=y)
    elif step == 2:
        sim("MOUSEMOVE", x=edge - 150, y=y)
    elif step == 3:
        sim("MOUSEMOVE", x=edge - 300, y=y)
    else:
        sim("LEFTMOUSE", "RELEASE", x=edge - 300, y=y)


def collapse_and_scroll(step):
    """Collapses the Brokkr panel (a click on its header), then scrolls the sidebar to its end, where the push result is."""
    area, ui, _ = view3d()
    x, y = ui.x + 120, area.y + area.height - 67
    if step == 0:
        sim("MOUSEMOVE", x=x, y=y)
    elif step == 1:
        sim("LEFTMOUSE", "PRESS", x=x, y=y)
    elif step == 2:
        sim("LEFTMOUSE", "RELEASE", x=x, y=y)
    else:
        sim("MOUSEMOVE", x=ui.x + 300, y=ui.y + ui.height // 2)
        for _ in range(12):
            sim("WHEELDOWNMOUSE", "PRESS", x=ui.x + 300, y=ui.y + ui.height // 2)


def note_shot():
    area, ui, _ = view3d()
    R["shot"].update(category_after=ui.active_panel_category, ui_after=[ui.x, ui.y, ui.width, ui.height])


def shoot():
    if not SHOT:
        return
    area, _, region = view3d()
    with bpy.context.temp_override(window=wm().windows[0], area=area, region=region):
        bpy.ops.screen.screenshot_area(filepath=SHOT)
    check("the panel screenshot was written", Path(SHOT).is_file(), SHOT)


def cleanup():
    leave_edit_mode()
    shutil.rmtree(SHIP_FOLDER, ignore_errors=True)


steps = [
    once(snapshot_check),
    once(edit_armor),
    once(lambda: send("open", action="aetheria.open_ship", collection=COLLECTION)), until(done("open")), once(open_check),
    once(lambda: send("push", action="aetheria.push_ship", collection=COLLECTION)), until(done("push")), once(push_check),
    once(enter_edit_mode),
    once(lambda: send("epush", action="aetheria.push_ship", collection=COLLECTION)), until(done("epush")), once(edit_push_check),
    once(lambda: send("eopen", action="aetheria.open_ship", collection=COLLECTION)), until(done("eopen")), once(edit_open_check),
    once(leave_edit_mode),
    once(lambda: send("apply", action="aetheria.apply_ship", collection=COLLECTION)), until(done("apply")), once(denied_check("apply", "aetheria.apply_ship")),
    once(lambda: send("package", action="aetheria.package_ship", collection=COLLECTION)), until(done("package")), once(denied_check("package", "aetheria.package_ship")),
    once(lambda: send("capture", action="aetheria.capture_ship_lines", collection=COLLECTION)), until(done("capture")), once(denied_check("capture", "aetheria.capture_ship_lines")),
    once(lambda: send("new", action="aetheria.new_ship")), until(done("new")), once(denied_check("new", "aetheria.new_ship")),
    once(human_apply),
    once(stage_shot), until(lambda: True), once(lambda: sim('MOUSEMOVE')), until(lambda: True), once(lambda: sim('LEFTMOUSE', 'PRESS')), until(lambda: True), once(lambda: sim('LEFTMOUSE', 'RELEASE')), until(lambda: True),
    once(lambda: widen(0)), until(lambda: True), once(lambda: widen(1)), until(lambda: True), once(lambda: widen(2)), until(lambda: True), once(lambda: widen(3)), until(lambda: True), once(lambda: widen(4)), until(lambda: True),
    once(lambda: collapse_and_scroll(0)), until(lambda: True), once(lambda: collapse_and_scroll(1)), until(lambda: True), once(lambda: collapse_and_scroll(2)), until(lambda: True), once(lambda: collapse_and_scroll(3)), until(lambda: True), until(lambda: True), until(lambda: True),
    once(note_shot), once(shoot),
]
i = 0


def write():
    R["ok"] = bool(R["checks"]) and all(c["ok"] for c in R["checks"]) and not R.get("errors")
    with open(OUT + ".result.json", "w") as f:
        json.dump(R, f, indent=1, default=str)


def tick():
    global i
    if i < len(steps):
        try:
            if steps[i]():
                i += 1
        except Exception:
            R.setdefault("errors", []).append({"step": i, "error": traceback.format_exc()})
            i = len(steps)
        write()
        return 0.5
    try:
        cleanup()
    except Exception:
        R.setdefault("errors", []).append({"step": "cleanup", "error": traceback.format_exc()})
    write()
    bpy.ops.wm.quit_blender()
    return None


bpy.app.timers.register(tick, first_interval=3.0)
