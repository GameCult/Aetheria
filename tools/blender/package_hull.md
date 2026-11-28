# package_hull.py: the hull packager

One bench hull plus one manifest becomes one ship package (`GameData/Mods/<id>/ship.cc`, `ship.glb`, `hull.scad`) that the
validator accepts, with no Unity and no clicking. The header of `package_hull.py` gives the command line, the environment
(`GAME_FOLDER`, `CULTLIB_PACKAGES`, `OPENSCAD`) and the order of steps. This file is the manifest schema and the way to check the
packager still matches the committed first-party package.

## Contract

- The manifest is creation input only. Once `GameData/Mods/<id>` exists, its `ship.cc` owns rows, stats and product; the manifest
  is never read again. Without `--replace` an existing package is refused untouched. With `--replace` it is re-derived and edits
  made since are discarded, but only on success: the old package is moved aside during the run, and any failure removes what the
  run built and puts the old one back exactly as it was.
- The last output line is `PACKAGE_OK` (exit 0) or `PACKAGE_FAILED` (exit 1, with the reason). There is no third state: a row whose
  cell is off the raster, outside the hull shape or overlapping an earlier row fails the run; nothing is moved for you.
- A manifest is checked before Blender or the game folder is touched: bad JSON, an unknown or missing key, an unknown `kind`, a
  malformed footprint, a duplicate row id and a missing hull file are each refused by name.

## Manifest (JSON; no key may be missing or unknown)

| key | meaning |
| --- | --- |
| `id` | the stable ship id and the folder name under `GameData/Mods`; `ship_cc.valid_ship_id` decides what is valid |
| `name` | the hull's display name |
| `like` | the name of a shipped catalog hull of type Ship or Station whose stats the new hull starts from (AetherDb `--like`); a Station's kind is not copied |
| `length` | whole cells, at least 1: the New Ship length field of the add-on |
| `extent` | `[x, y, z]` metres of the stand-in box the model is made from; with the add-on's `CELL_SIZE` of 2 m (`aetheria_ships/hull_grid.py`) this fixes the raster, so rows written for one extent do not carry to another |
| `hull` | a file beside the manifest, copied to the package as `hull.scad` (OpenSCAD source with the kit's faction modules) |
| `maker` | the name of a faction the shipped catalog holds; the product record is made for it |
| `description` | the product's description |
| `offered` | `true` or `false`: whether the maker sells it |
| `price` | at least 0 (AetherDb `price`) |
| `stats` | `mass`, `durability`, `armor`, `drag`, each at least 0, and optionally `top_speed` (a hull without it keeps `like`'s) |
| `rows` | a non-empty list of hardpoint rows, below |
| `kind` | optional: a station kind id (such as `depot`), written by AetherDb `ship-authoring kind` after stats; required when `like` is a Station, because `validate` refuses a station package that names no kind the shipped catalog holds |

A row has all of `id`, `kind`, `cell`, `footprint`, `rotation`, `armor`, `arc`:

- `id` is the mount id the model's anchor binds to (`aetheria.id`); unique in the manifest.
- `kind` is one of `ship_cc.HARDPOINT_TYPE_NAMES` (Hull, Tool, Thermal, Thruster, WarpDrive, Reactor, Radiator, Shield, Sensors,
  Energy, Ballistic, Launcher, ControlModule, Facility). The packager keeps no list of its own. A `Facility` row is internal (no
  anchor, nothing on the model) and only a Station hull takes one.
- `cell` is `[x, y]`, whole numbers of at least 0: the footprint's lowest-x, lowest-y cell on the raster. `x` runs across, `y` from
  the stern (0) toward the bow, as the layout panel shows it. Cells are in schematic space, not model space.
- `footprint` is rows of `#` (the mount's cells) and `.` separated by `/`, all one width, at least one `#`. The first row is the top
  (highest y); the last row is `y` itself. `##/#.` is a 2x2 box missing its lower-right cell.
- `rotation` is `0` None, `1` CounterClockwise, `2` Reversed, `3` Clockwise (`ship_cc.ROTATION_NAMES`).
- `armor` is at least 0; `arc` is the firing arc in degrees, 0 to 360.

The committed example is `tests/fixtures/hulls/zh-ammo-tender/package.json`; its rows fit the raster that its `hull.scad` and
`extent` produce.

## Reproducers (`hull_packager/`)

- `build_aetherdb.ps1` publishes `tools/AetherDb` into `<game>/ModTools`: it needs CultLib and CultMath worktrees at the revisions
  `Directory.Build.props` names and passes `ErrorOnDuplicatePublishOutputFiles=false` (the NETSDK1152 workaround).
- `manifests/arsenal-depot/` is the committed manifest of the Arsenal Depot (its `hull.scad` is a copy of the package's; a test holds both equal to the shipped package, `Fa.1` included): a station's manifest is written by hand, and `gen_manifest.py` refuses a station with a message.
- `gen_manifest.py <ship.cc> <hull.scad> <out.json> --like ... --extent X Y Z --maker ...` turns an existing package into a manifest.
- `compare.py <committed ship.cc> <derived ship.cc>` prints `COMPARE_EQUAL` when shape, rows, stats, price and product match.
- `job.ps1` runs the regression on a workstation with Blender and OpenSCAD: the fixture manifest; the committed
  `aetheria.nakovalnya` regenerated from its own manifest and compared; a refused second run and a failed `--replace`, each
  required to leave the package byte-identical; then compose. Launch it detached; its header gives the log and status files.

`python -m unittest discover -s tools/blender/tests -p test_package_hull.py` runs the Blender-free tests (manifest refusals, row
placement, the all-or-nothing swap).
