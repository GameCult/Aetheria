"""Agent-built hulls: reads a hull.scad through OpenSCAD and judges it against its faction's form language.

No bpy import. A hull source is a GameData/Mods/<id>/hull.scad that includes its faction's language file (and, for a
Pirate hull, its donor's) from factions/. Everything the add-on needs is echoed by one part of that file. The faction's language is owned by its own file in
factions/: the checks read it from there, by the faction the brief names, and the hull's own `lang` echo is only compared
with it (a hull that redefines or shadows an included language fails a check):

    brief    ECHO brief = [faction, donor, role, [length_min, length_max], [tris_min, tris_max], reads]
    lang     ECHO lang  = [faction, [[zone, [r, g, b]], ...], body zone, glow, radiator, proportion, symmetry, maker]
             (maker: the faction's Faction record key in GameData/Aetheria.cc, the eighth element)
             (every included language file echoes its own, and so does factions/<faction>.scad run alone; a Pirate
             language has no body zone and no proportion)
    anchors  ECHO anchor = [id, role, [x, y, z], yaw, parent]      one line per anchor, yaw 0 nose, 90 port
    <zone>   the zone's geometry, OFF; a donor's zone is named "<donor>:<zone>"
    emit:<id>, rad:<id>   the mesh of a thruster-emitter or radiator-mesh anchor

Frame: Ship Root, metres, nose -Y, starboard -X, up +Z. build() returns a HullBuild; the caller refuses on any failed
check. Messages and check names are fixed text: nothing a hull source says is echoed back.
"""

import hashlib
import json
import math
import os
import re
import subprocess
import tempfile
from dataclasses import dataclass, field
from pathlib import Path
from typing import Callable, Optional

FACTIONS_DIR = Path(__file__).with_name("factions")
HULL_FILE = "hull.scad"
OPENSCAD_TIMEOUT = 180  # seconds, per part
SILHOUETTE_CELL = 0.25  # metres, the top-view raster the symmetry check compares
ANCHOR_ROLES = ("weapon-mount", "weapon-muzzle", "thruster-emitter", "radiator-mesh")
MESH_ROLES = {"thruster-emitter": "emit:", "radiator-mesh": "rad:"}
ECHO_PARTS = ("brief", "lang", "anchors")  # parts that make no geometry; their ECHO lines are the answer
LANGUAGE_NAME = re.compile(r"[a-z][a-z0-9_]*")
ECHO_LINE = re.compile(r"^ECHO: (\w+) = (.*)$", re.MULTILINE)

# A runner answers one part of the hull source with (OFF text, ECHO text). OFF text is "" when the part made no geometry.
Runner = Callable[[str], "tuple[str, str]"]


class HullSourceError(ValueError):
    """The hull source could not be read. The message is fixed text."""


@dataclass(frozen=True)
class Mesh:
    vertices: tuple
    triangles: tuple

    def bounds(self):
        """((min x, y, z), (max x, y, z))."""
        columns = list(zip(*self.vertices))
        return tuple(min(c) for c in columns), tuple(max(c) for c in columns)


@dataclass(frozen=True)
class Language:
    faction: str
    zones: tuple  # ((name, (r, g, b)), ...)
    body: str  # the zone whose proportions the language judges; "" for a language with no body of its own
    glow: tuple
    radiator: tuple
    proportion: Optional[tuple]  # ((h/w min, max), (w/l min, max)); None for a language that takes its donor's
    symmetry: tuple  # ("bilateral", minimum mirror overlap) or ("asymmetric", maximum mirror overlap)
    maker: str  # the Faction record key a product of this language's hulls names as its Manufacturer

    def zone_names(self):
        return [name for name, _ in self.zones]

    def colour(self, zone):
        return dict(self.zones)[zone]


@dataclass(frozen=True)
class Brief:
    faction: str
    donor: str
    role: str
    length: tuple
    triangles: tuple
    reads: tuple


@dataclass(frozen=True)
class Anchor:
    id: str
    role: str
    position: tuple
    yaw: float
    parent: str


@dataclass(frozen=True)
class Check:
    name: str
    ok: bool
    detail: str = ""


@dataclass
class HullBuild:
    brief: Brief
    languages: dict  # faction -> Language
    zones: dict  # zone key ("primary", "rnd:primary") -> Mesh
    colours: dict  # zone key -> (r, g, b)
    anchors: list
    anchor_meshes: dict  # anchor id -> Mesh
    glow: tuple
    radiator: tuple
    body_zone: str  # the zone key the collider is made from
    checks: list = field(default_factory=list)

    def failed(self):
        return [check for check in self.checks if not check.ok]


def parse_off(text):
    """(vertices, triangles) of an OFF file, faces fanned into triangles. An empty or absent text is an empty mesh."""
    lines = [line.split() for line in text.splitlines() if line.strip() and not line.lstrip().startswith("#")]
    if not lines or lines[0][0] != "OFF":
        return Mesh((), ())
    try:
        header = lines[1] if len(lines[0]) == 1 else lines[0][1:]
        vertex_count, face_count = int(header[0]), int(header[1])
        first = 2 if len(lines[0]) == 1 else 1
        vertices = tuple(tuple(float(v) for v in lines[first + i][:3]) for i in range(vertex_count))
        triangles = []
        for row in lines[first + vertex_count:first + vertex_count + face_count]:
            ring = [int(v) for v in row[1:1 + int(row[0])]]
            triangles += [(ring[0], ring[i], ring[i + 1]) for i in range(1, len(ring) - 1)]
        if any(not 0 <= v < vertex_count for tri in triangles for v in tri):
            raise ValueError
    except (ValueError, IndexError):
        raise HullSourceError("The hull source made geometry that is not a readable OFF file") from None
    return Mesh(vertices, tuple(triangles))


def parse_echoes(text, name):
    """The values of every 'ECHO: <name> = <value>' line. OpenSCAD prints JSON-compatible values."""
    values = []
    for key, raw in ECHO_LINE.findall(text):
        if key != name:
            continue
        try:
            values.append(json.loads(raw))
        except ValueError:
            raise HullSourceError(f"The hull source echoed a {name} that is not plain numbers, strings and lists") from None
    return values


def fingerprint(*values):
    """A digest of numbers, strings and the lists of them, floats to five decimals: equal exactly when the values are."""
    digest = hashlib.sha256()

    def add(value):
        if isinstance(value, (list, tuple)):
            digest.update(b"[")
            for item in value:
                add(item)
            digest.update(b"]")
        else:
            digest.update((f"{round(value, 5) + 0.0:.5f};" if isinstance(value, float) else f"{value!r};").encode())

    add(values)
    return digest.hexdigest()


def _vector(value, size, numbers=True):
    if not isinstance(value, list) or len(value) != size or (numbers and not all(_number(v) for v in value)):
        raise HullSourceError("The hull source echoed a vector of the wrong shape")
    return tuple(value)


def _number(value):
    return isinstance(value, (int, float)) and not isinstance(value, bool)


def _text(value):
    if not isinstance(value, str):
        raise HullSourceError("The hull source echoed a name that is not a string")
    return value


def read_brief(text):
    (raw,) = _exactly_one(parse_echoes(text, "brief"), "brief")
    if not isinstance(raw, list) or len(raw) != 6 or not isinstance(raw[5], list):
        raise HullSourceError("The hull source's brief has the wrong shape")
    return Brief(_text(raw[0]), _text(raw[1]), _text(raw[2]), _vector(raw[3], 2), _vector(raw[4], 2),
                 tuple(_text(r) for r in raw[5]))


def _proportion(raw):
    if raw == []:
        return None
    if not isinstance(raw, list) or len(raw) != 2:
        raise HullSourceError("A faction language has the wrong shape")
    return _vector(raw[0], 2), _vector(raw[1], 2)


def read_languages(text):
    """Every language echoed in text, in echo order."""
    languages = []
    for raw in parse_echoes(text, "lang"):
        if not isinstance(raw, list) or len(raw) != 8 or not isinstance(raw[1], list):
            raise HullSourceError("A faction language has the wrong shape")
        zones = tuple((_text(z[0]), _vector(z[1], 3)) for z in raw[1] if isinstance(z, list) and len(z) == 2)
        if len(zones) != len(raw[1]):
            raise HullSourceError("A faction language has the wrong shape")
        proportion = _proportion(raw[5])
        symmetry = _vector(raw[6], 2, numbers=False)
        if symmetry[0] not in ("bilateral", "asymmetric") or not _number(symmetry[1]):
            raise HullSourceError("A faction language names a symmetry rule it does not have")
        maker = _text(raw[7])
        if not maker:
            raise HullSourceError("A faction language names no maker")
        language = Language(_text(raw[0]), zones, _text(raw[2]), _vector(raw[3], 3), _vector(raw[4], 3), proportion,
                            symmetry, maker)
        if language.body and language.body not in language.zone_names():
            raise HullSourceError("A faction language's body zone is not one of its zones")
        languages.append(language)
    return languages


def read_anchors(text):
    anchors = []
    for raw in parse_echoes(text, "anchor"):
        if not isinstance(raw, list) or len(raw) != 5 or not _number(raw[3]):
            raise HullSourceError("An anchor has the wrong shape")
        anchors.append(Anchor(_text(raw[0]), _text(raw[1]), _vector(raw[2], 3), float(raw[3]), _text(raw[4])))
    return anchors


def _exactly_one(values, what):
    if len(values) != 1:
        raise HullSourceError(f"The hull source must echo exactly one {what}")
    return values


def mirror_overlap(triangles_xy, cell=SILHOUETTE_CELL):
    """Intersection over union of the top-view silhouette with its reflection across x = 0. The silhouette is every raster
    cell whose centre a triangle covers (a feature thinner than a cell may not show)."""
    cells = set()
    for (ax, ay), (bx, by), (cx, cy) in triangles_xy:
        det = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
        if abs(det) < 1e-12:
            continue
        for i in range(math.floor(min(ax, bx, cx) / cell), math.floor(max(ax, bx, cx) / cell) + 1):
            px = (i + 0.5) * cell
            for j in range(math.floor(min(ay, by, cy) / cell), math.floor(max(ay, by, cy) / cell) + 1):
                py = (j + 0.5) * cell
                u = ((by - cy) * (px - cx) + (cx - bx) * (py - cy)) / det
                v = ((cy - ay) * (px - cx) + (ax - cx) * (py - cy)) / det
                if u >= 0 and v >= 0 and u + v <= 1:
                    cells.add((i, j))
    mirrored = {(-i - 1, j) for i, j in cells}
    union = len(cells | mirrored)
    return len(cells & mirrored) / union if union else 1.0


def _triangles_xy(mesh):
    return [tuple(mesh.vertices[v][:2] for v in tri) for tri in mesh.triangles]


def _language(library, faction):
    """The language of factions/<faction>.scad, which owns it: exactly one echo, naming that faction."""
    (language,) = _exactly_one(read_languages(library(faction)), "language")
    if language.faction != faction:
        raise HullSourceError("A faction language file echoes the language of another faction")
    return language


def build(runner, library):
    """Reads the hull source through runner (see Runner) and judges it against the languages library(faction) answers with
    (the ECHO text of that faction's own language file). Raises HullSourceError when the source cannot be read at all;
    otherwise returns a HullBuild whose failed() lists the checks it breaks."""
    _, text = runner("brief")
    brief = read_brief(text)
    _, text = runner("lang")
    included = read_languages(text)
    own = _language(library, brief.faction)
    donor = _language(library, brief.donor) if brief.donor else None
    owned = [own] + ([donor] if donor else [])
    zone_keys = list(own.zone_names()) + [f"{donor.faction}:{z}" for z in (donor.zone_names() if donor else ())]
    colours = {z: own.colour(z) for z in own.zone_names()}
    colours.update({f"{donor.faction}:{z}": donor.colour(z) for z in (donor.zone_names() if donor else ())})
    zones = {key: parse_off(runner(key)[0]) for key in zone_keys}
    _, text = runner("anchors")
    anchors = read_anchors(text)
    ids = [a.id for a in anchors]
    if len(set(ids)) != len(ids) or any(a.role not in ANCHOR_ROLES for a in anchors):
        raise HullSourceError("The hull source's anchors repeat an id or name a role a hull source does not make")
    anchor_meshes = {a.id: parse_off(runner(MESH_ROLES[a.role] + a.id)[0]) for a in anchors if a.role in MESH_ROLES}
    judge = donor or own
    body_zone = f"{donor.faction}:{donor.body}" if donor else own.body
    result = HullBuild(brief, {language.faction: language for language in owned}, zones, colours, anchors, anchor_meshes,
                       own.glow, own.radiator, body_zone)
    by_faction = lambda language: language.faction
    result.checks = [Check("the hull includes its faction's language files and changes none of them",
                           sorted(included, key=by_faction) == sorted(owned, key=by_faction), "")]
    result.checks += _checks(result, own, judge)
    return result


def _checks(hull, own, judge):
    brief, checks = hull.brief, []
    empty = [key for key, mesh in hull.zones.items() if not mesh.triangles]
    checks.append(Check("every zone of the language has geometry", not empty, f"{len(empty)} empty"))
    missing = [a.id for a in hull.anchors if a.role in MESH_ROLES and not hull.anchor_meshes[a.id].triangles]
    checks.append(Check("every emitter and radiator anchor has a mesh", not missing, f"{len(missing)} without"))
    checks.append(Check("a donor is named exactly when the language has no proportions of its own",
                        bool(brief.donor) == (own.proportion is None), ""))
    body = hull.zones[hull.body_zone] if hull.body_zone in hull.zones else Mesh((), ())
    if body.triangles and judge.proportion:
        low, high = body.bounds()
        width, length, height = (high[0] - low[0]), (high[1] - low[1]), (high[2] - low[2])
        ratios = (height / width if width else float("inf"), width / length if length else float("inf"))
        (hw_lo, hw_hi), (wl_lo, wl_hi) = judge.proportion
        checks.append(Check("the body's proportions lie inside the language's ranges",
                            hw_lo <= ratios[0] <= hw_hi and wl_lo <= ratios[1] <= wl_hi,
                            f"h/w {ratios[0]:.2f}, w/l {ratios[1]:.2f}"))
    else:
        checks.append(Check("the body's proportions lie inside the language's ranges", False, "no body to judge"))
    every = [m for m in list(hull.zones.values()) + list(hull.anchor_meshes.values()) if m.triangles]
    if every:
        overlap = mirror_overlap([t for m in every for t in _triangles_xy(m)])
        rule, bound = own.symmetry
        checks.append(Check(f"the top view obeys the language's {rule} rule",
                            overlap >= bound if rule == "bilateral" else overlap <= bound, f"mirror overlap {overlap:.2f}"))
        ys = [v[1] for m in every for v in m.vertices]
        length = max(ys) - min(ys)
        checks.append(Check("the length lies inside the brief's range", brief.length[0] <= length <= brief.length[1],
                            f"{length:.1f} m"))
    else:
        checks.append(Check("the top view obeys the language's symmetry rule", False, "no geometry"))
        checks.append(Check("the length lies inside the brief's range", False, "no geometry"))
    triangles = sum(len(m.triangles) for m in every)
    checks.append(Check("the triangle count lies inside the brief's budget",
                        brief.triangles[0] <= triangles <= brief.triangles[1], f"{triangles}"))
    by_id = {a.id: a for a in hull.anchors}
    unmuzzled = [a.id for a in hull.anchors if a.role == "weapon-mount"
                 and not any(m.role == "weapon-muzzle" and m.parent == a.id for m in hull.anchors)]
    orphans = [m.id for m in hull.anchors if m.role == "weapon-muzzle"
               and (m.parent not in by_id or by_id[m.parent].role != "weapon-mount")]
    checks.append(Check("every weapon-mount has a muzzle and no muzzle is orphaned",
                        not (unmuzzled or orphans),
                        f"{len(unmuzzled)} without a muzzle, {len(orphans)} orphaned"))
    return checks


def openscad_runner(executable, source, library=FACTIONS_DIR, on_start=None):
    """A Runner that runs OpenSCAD (Manifold backend, full render) on source, one process at a time. on_start(pid) is
    called with each process's id. OPENSCADPATH is the language folder, so a hull source includes <zhestokost.scad>."""
    env = dict(os.environ, OPENSCADPATH=str(library))

    def run(part):
        with tempfile.TemporaryDirectory() as folder:
            target = Path(folder) / ("part.echo" if part in ECHO_PARTS else "part.off")
            command = [str(executable), "--backend", "Manifold", "--render", "all", "-D", f'part="{part}"',
                       "-o", str(target), str(source)]
            try:
                process = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, env=env)
            except OSError:
                raise HullSourceError("OpenSCAD could not be started") from None
            if on_start:
                on_start(process.pid)
            try:
                output, _ = process.communicate(timeout=OPENSCAD_TIMEOUT)
            except subprocess.TimeoutExpired:
                process.kill()
                process.communicate()
                raise HullSourceError("OpenSCAD did not finish a part in time") from None
            if process.returncode != 0:
                raise HullSourceError("OpenSCAD refused a part of the hull source")
            written = target.read_text(encoding="utf-8", errors="replace") if target.exists() else ""
            return ("", written + output) if part in ECHO_PARTS else (written, output)

    return run


def language_library(executable, library=FACTIONS_DIR, on_start=None):
    """The `library` build() reads languages from: faction -> the ECHO text of library/<faction>.scad run alone, so a hull
    source (or a file beside it) cannot stand in for the faction's own file."""
    def read(faction):
        file = library / f"{faction}.scad"
        if not LANGUAGE_NAME.fullmatch(faction) or not file.is_file():
            raise HullSourceError("The hull source names a faction that has no language file")
        return openscad_runner(executable, file, library=library, on_start=on_start)("lang")[1]

    return read
