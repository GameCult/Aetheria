// Zhestokost form language: the one owner of this faction's part kit, proportions, symmetry rule and palette.
// Operator ruling zhestokost-language-lore-text: "The core design pillars were simply industrial and brutalist ...
// Zhestokost is the dream of the eternal Soviet, a society that has achieved total and unassailable permaculture via a
// carefully organized and brutally practical (self-policing) state." Lore (Faction Flavor > Zhestokost > Visual
// Identity): low broad stepped masses, thick armour round recessed machinery, apparent weight from deep seams and
// substantial supports rather than clutter; charcoal, iron grey, dull oxide red, small cream serials and safety marks.
//
// Brutalist is not "boxes of uniform weight". The vocabulary here is poured and cast concrete-and-iron:
//   * MASSES are battered (walls slope in to the deck and in to the belly from a vertical cast belt at the waist), so
//     every mass reads as poured, with a lit deck, a lit upper wall, a dark belt line and a shadowed belly.
//   * HIERARCHY: one ship = a narrow CORE plus a few heavy repeated MODULES wider than the core, so the plan is
//     castellated (wide-narrow-wide), never a plain rectangle. Modules step up in TIERS.
//   * SHADOW GAPS: modules stand apart across deep reveals; the reveal floor is the recess zone, so the gap reads black
//     from above even at 48 px.
//   * Fittings are few, thick and functional: hatches with heavy hinge bars, collars ringed with lugs, a tow eye.
//
// How a Zhestokost hull is built (a hull composes these modules; nothing here places itself):
//   1. A MASS is a list of sections nose (-Y) to stern (+Y): [y, width, height, base z, deck inset, belly inset]
//      (zh_s). Make lists with zh_plan (a core or hull), zh_block (a module), zh_tier (a stepped tier on any mass);
//      draw them with zh_mass. Keep the lists: every other part mates to them.
//   2. Every other part is placed ON a mass by a typed mate: zh_on_top, zh_on_wall, zh_on_belt, zh_on_belly,
//      zh_on_stern, zh_on_nose. In the mate frame local +Z is the outward normal and the part is sunk ZH_SINK into
//      the skin, so nothing floats. Top/belly frames keep local +Y aft; wall/belt frames keep +Y aft with local +X
//      pointing down the wall on either side (starboard mates are mirrors of port); stern/nose frames have local +Y down.
//   3. Zone routing: every zh_ module draws only the zone the global `part` names, so a hull writes its assembly once.
//      A hull on a Zhestokost donor (HULL_DONOR == "zhestokost") names a donor zone 'zhestokost:<zone>'.
//      Anchor meshes (zh_rad_vanes, zh_emitter) are not routed: call them inside the hull's rad:/emit: parts under
//      the same mate that placed their housing.
// Scale: chamfers are cast-heavy (ZH_CH); fittings have standard sizes (ZH_HATCH, ZH_COLLAR_R, ZH_LUG) shared by
// every hull so the family reads.
include <lowpoly.scad>

ZH_WL = 0.30;          // cast belt: from this fraction of section height ...
ZH_WU = 0.50;          // ... to this one; above it the wall batters in to the deck, below it in to the belly
ZH_CH = 0.20;          // edge chamfer for fittings
ZH_SINK = 0.05;        // mates sink parts this far into the skin
ZH_HATCH = 2.6;        // standard magazine hatch (m, square)
ZH_COLLAR_R = 1.05;    // standard transfer collar radius
ZH_LUG = 1.0;          // standard tow lug scale

function zh_key(z) = (!is_undef(HULL_DONOR) && HULL_DONOR == "zhestokost") ? str("zhestokost:", z) : z;
module zh_z(z) { if (part == zh_key(z)) children(); }

// ==== geometry core: sections, surface points, mates =================================================================
// A section. bt: deck inset (how far the upper wall batters in), bb: belly inset. Defaults keep the faction's batter.
function zh_s(y, w, h, z0=0, bt=undef, bb=undef) =
  [y, w, h, z0, is_undef(bt) ? h*0.26 : bt, is_undef(bb) ? h*0.22 : bb];
function zh_poly(s) = let(w = s[1]/2, h = s[2], z = s[3], t = s[4], b = s[5])
  [[-(w-b), z], [w-b, z], [w, z+h*ZH_WL], [w, z+h*ZH_WU], [w-t, z+h], [-(w-t), z+h], [-w, z+h*ZH_WU], [-w, z+h*ZH_WL]];
module _zh_slice(s) translate([0, s[0], 0]) rotate([90, 0, 0]) linear_extrude(0.002, center=true) polygon(zh_poly(s));

function _zh_i(secs, y) = let(ix = [for (k = [0:len(secs)-2]) if (secs[k][0] <= y) k]) len(ix) ? ix[len(ix)-1] : 0;
// the mass's section at station y (linear between stations, as the mesh is)
function zh_sec(secs, y) = let(i = _zh_i(secs, y), a = secs[i], b = secs[i+1],
  f = max(0, min(1, (y - a[0]) / (b[0] - a[0])))) a + (b - a) * f;
function zh_y0(secs) = secs[0][0];
function zh_y1(secs) = secs[len(secs)-1][0];

// Surface points. u: -1 starboard .. 1 port across the deck or belly; v: 0..1 up a wall or belt; side: 1 port, -1 stbd.
function zh_top(secs, y, u=0) = let(s = zh_sec(secs, y)) [u*(s[1]/2 - s[4]), y, s[3] + s[2]];
function zh_belly(secs, y, u=0) = let(s = zh_sec(secs, y)) [u*(s[1]/2 - s[5]), y, s[3]];
function zh_belt(secs, y, side=1, v=0.5) = let(s = zh_sec(secs, y)) [side*s[1]/2, y, s[3] + s[2]*(ZH_WL + (ZH_WU-ZH_WL)*v)];
function zh_wall(secs, y, side=1, v=0.5) = let(s = zh_sec(secs, y)) [side*(s[1]/2 - s[4]*v), y, s[3] + s[2]*(ZH_WU + (1-ZH_WU)*v)];
function zh_wall_tilt(secs, y) = let(s = zh_sec(secs, y)) atan2(s[4], s[2]*(1-ZH_WU));   // degrees off vertical
function zh_stern_pt(secs, x=0, v=0.5) = let(s = secs[len(secs)-1]) [x, s[0], s[3] + s[2]*v];
function zh_nose_pt(secs, x=0, v=0.5) = let(s = secs[0]) [x, s[0], s[3] + s[2]*v];

module zh_on_top(secs, y, u=0, sink=ZH_SINK) translate(zh_top(secs, y, u) - [0, 0, sink]) children();
module zh_on_belly(secs, y, u=0, sink=ZH_SINK) translate(zh_belly(secs, y, u) + [0, 0, sink]) rotate([0, 180, 0]) children();
module _zh_port(side) { if (side < 0) mirror([1, 0, 0]) children(); else children(); }
module zh_on_belt(secs, y, side=1, v=0.5, sink=ZH_SINK)
  _zh_port(side) translate(zh_belt(secs, y, 1, v) - [sink, 0, 0]) rotate([0, 90, 0]) children();
module zh_on_wall(secs, y, side=1, v=0.5, sink=ZH_SINK) {
  t = zh_wall_tilt(secs, y);
  _zh_port(side) translate(zh_wall(secs, y, 1, v) - [cos(t), 0, sin(t)]*sink) rotate([0, 90 - t, 0]) children();
}
module zh_on_stern(secs, x=0, v=0.5, sink=ZH_SINK) translate(zh_stern_pt(secs, x, v) - [0, sink, 0]) rotate([-90, 0, 0]) children();
module zh_on_nose(secs, x=0, v=0.5, sink=ZH_SINK) translate(zh_nose_pt(secs, x, v) + [0, sink, 0]) rotate([90, 0, 0]) rotate([0, 0, 180]) children();
// world point of a local offset under a mate (for anchors): top, belly, stern frames
function zh_top_at(secs, y, u, o, sink=ZH_SINK) = zh_top(secs, y, u) - [0, 0, sink] + o;
function zh_belly_at(secs, y, u, o, sink=ZH_SINK) = zh_belly(secs, y, u) + [0, 0, sink] + [-o[0], o[1], -o[2]];
function zh_stern_at(secs, x, v, o, sink=ZH_SINK) = zh_stern_pt(secs, x, v) - [0, sink, 0] + [o[0], o[2], -o[1]];

// ==== PRIMARY: masses that make the silhouette ========================================================================
// Any section list as one faceted poured mass.
module zh_mass(secs, zone="primary") zh_z(zone) for (i = [0:len(secs)-2]) hull() { _zh_slice(secs[i]); _zh_slice(secs[i+1]); }

// A core or whole hull: bow chamfered in plan and dropped (bow, as a fraction of L), stern a flat face with a cast lip.
// bowW / bowH: nose width and height as fractions of W and H.
function zh_plan(L, W, H, z0=0, y0=0, bow=0.2, bowW=0.55, bowH=0.7, bt=undef, bb=undef) = let(n = y0 - L/2, s = y0 + L/2)
  [zh_s(n, W*bowW, H*bowH, z0 + H*(1-bowH)*0.55, bt, bb), zh_s(n + L*bow, W, H, z0, bt, bb),
   zh_s(s - 0.35, W, H, z0, bt, bb), zh_s(s, W - 0.5, H - 0.3, z0 + 0.15, bt, bb)];

// A heavy repeated module (magazine, reactor block, barracks): chamfered in plan at both ends by ch.
function zh_block(y, l, w, h, z0=0, bt=undef, bb=undef, ch=0.5) =
  let(t = is_undef(bt) ? h*0.24 : bt, b = is_undef(bb) ? h*0.2 : bb)
  [zh_s(y - l/2, w - 2*ch, h - ch, z0 + ch*0.5, max(0.05, t - ch*0.5), max(0.05, b - ch*0.5)),
   zh_s(y - l/2 + ch, w, h, z0, t, b), zh_s(y + l/2 - ch, w, h, z0, t, b),
   zh_s(y + l/2, w - 2*ch, h - ch, z0 + ch*0.5, max(0.05, t - ch*0.5), max(0.05, b - ch*0.5))];

// A stepped tier poured on top of any mass: inset from the deck edge, ends pulled in by yin, h tall.
function zh_tier(secs, inset=0.6, h=0.7, yin=0.6, bt=undef) = let(n = len(secs))
  [for (i = [0:n-1]) let(s = secs[i], y = s[0] + (i == 0 ? yin : i == n-1 ? -yin : 0), d = zh_sec(secs, y))
    zh_s(y, d[1] - 2*d[4] - 2*inset, h, d[3] + d[2] - 0.02, is_undef(bt) ? h*0.4 : bt, 0)];

// ==== SECONDARY: forms that give a hull its individuality =============================================================
// Low armoured cab (top frame): battered block, a deep cast brow over a black vision slit. Nose is -Y.
module zh_cab(w=3.6, l=3.0, h=1.5, zone="secondary") {
  zh_mass(zh_block(0, l, w, h, 0, h*0.32, 0, 0.4), zone);
  zh_z(zone) translate([0, -l/2 + 0.05, h*0.74]) cbox([w*0.84, 0.9, 0.34], 0.08);         // brow
  zh_z("recess") translate([0, -l/2 + 0.32, h*0.5]) cube([w*0.66, 0.6, 0.26], center=true); // slit, in the brow's shadow
}

// Squat drive pod (belly frame: local +Z is down, away from the hull): battered block, nozzle mouth on its aft face.
module zh_pod(w=2.0, l=3.2, h=1.4, zone="secondary") {
  zh_mass(zh_block(0, l, w, h, 0, h*0.18, h*0.22, 0.35), zone);
  zh_z(zone) translate([0, l/2 + 0.08, h*0.5]) difference() {                                 // cast nozzle frame
    cbox([w*0.86, 0.36, h*0.78], 0.08); cube([w*0.56, 1, h*0.5], center=true); }
  zh_z("recess") translate([0, l/2 - 0.02, h*0.5]) cube([w*0.58, 0.3, h*0.52], center=true);
}
function zh_pod_mouth(w=2.0, l=3.2, h=1.4) = [0, l/2 + 0.14, h*0.5];
// thruster-emitter mesh at the pod's (or any) nozzle mouth: call under the same mate, translated to zh_pod_mouth
module zh_emitter(w, h) cube([w, 0.05, h], center=true);

// Transfer collar (belt or wall frame): thick octagonal ring proud of the skin, black bore, guide lugs in oxide.
module zh_collar(r=ZH_COLLAR_R, proud=0.55, lugs=4) {
  zh_z("secondary") difference() {
    cylinder(r=r, h=proud + ZH_SINK, $fn=8); translate([0, 0, -0.1]) cylinder(r=r*0.6, h=proud + 1, $fn=8); }
  zh_z("recess") cylinder(r=r*0.62, h=0.08 + ZH_SINK, $fn=8);
  zh_z("accent") for (i = [0:lugs-1]) rotate(i*360/lugs) translate([r + 0.16, 0, (proud + 0.25)/2])
    cube([0.5, 0.6, proud + 0.25 + ZH_SINK], center=true);
}

// Magazine hatch (any frame, +Z out): oxide slab, two heavy hinge bars and four dogs in charcoal, cream stripe at aft edge.
// dogs=false for a small or secondary hatch (saves ~100 triangles).
module zh_hatch(w=ZH_HATCH, l=ZH_HATCH, t=0.22, dogs=true) {
  zh_z("accent") translate([0, 0, t/2]) cbox([w, l, t + ZH_SINK], 0.06);
  zh_z("secondary") {
    for (x = [-1, 1]) translate([x*w*0.27, 0, t + 0.12]) cbox([0.5, l + 0.6, 0.34], 0.07);
    if (dogs) for (sx = [-1, 1], sy = [-1, 1]) translate([sx*(w/2 + 0.12), sy*l*0.3, t*0.6]) cube([0.36, 0.44, 0.4], center=true);
  }
  translate([0, l/2 - 0.42, t]) zh_stripes(w*0.36, 0.5, dogs ? 3 : 2);
}

// Armour plate (any frame): replaceable slab with four bolt bosses; leaves a seam to its neighbour.
module zh_plate(w, l, t=0.3, zone="secondary") zh_z(zone) {
  translate([0, 0, t/2]) cbox([w, l, t + ZH_SINK], 0.07);
  for (sx = [-1, 1], sy = [-1, 1]) translate([sx*(w/2 - 0.25), sy*(l/2 - 0.25), t + 0.05]) cube([0.22, 0.22, 0.12], center=true);
}

// Radiator trough (top frame): cast frame round a black floor, split by a spine wall; vanes stand in it (zh_rad_vanes).
module zh_rad_trough(w=3.2, l=2.4, d=0.45, zone="secondary") {
  zh_z(zone) translate([0, 0, d/2]) difference() {
    cbox([w + 0.7, l + 0.7, d + ZH_SINK], 0.1); cube([w, l, d*3], center=true); }
  zh_z(zone) translate([0, 0, d*0.6]) cube([0.3, l, d*1.2 + ZH_SINK], center=true);
  zh_z("recess") translate([0, 0, 0.04]) cube([w, l, 0.08 + ZH_SINK], center=true);
}
// radiator-mesh for one half of a trough (side 1 port, -1 starboard): transverse vanes rising out of it
module zh_rad_vanes(w=3.2, l=2.4, side=1, n=5, h=1.0) {
  for (i = [0:n-1]) translate([side*(w/4 + 0.08), -l/2 + 0.2 + i*(l - 0.4)/(n-1), h/2]) cube([w/2 - 0.36, 0.14, h], center=true);
}
function zh_rad_centre(w=3.2, side=1, h=1.0) = [side*(w/4 + 0.08), 0, h/2];

// Tow lug (stern frame: local +Z aft, local +Y down): a cast block, a neck, a heavy oxide eye with a vertical pin hole.
module zh_tow_lug(s=ZH_LUG, zone="secondary") {
  zh_z(zone) { translate([0, 0, 0.4*s]) cbox([1.8*s, 1.3*s, 0.8*s + ZH_SINK], 0.16*s);
               translate([0, 0, 1.05*s]) cbox([1.0*s, 0.8*s, 0.6*s], 0.1*s); }
  zh_z("accent") translate([0, 0, 1.75*s]) rotate([90, 0, 0]) difference() {
    cylinder(r=0.72*s, h=0.55*s, center=true, $fn=8); cylinder(r=0.3*s, h=2*s, center=true, $fn=8); }
}

// Ballistic mount (top frame): octagonal ring, battered mantlet block, n thick barrels with box brakes, pointing -Y.
module zh_gun(n=2, cal=0.22, len=3.0, base=1.8, zone="secondary") {
  zh_z(zone) cylinder(r=base*0.56, h=0.35 + ZH_SINK, $fn=8);
  zh_mass([for (s = zh_block(0, base*0.95, base, base*0.5, 0.33, base*0.14, 0, 0.22)) s], zone);
  zh_z(zone) for (i = [0:n-1]) translate([(i - (n-1)/2)*cal*3.2, -base*0.4, 0.33 + base*0.25]) {
    rotate([0, 0, 180]) tube(cal, len, 8);
    translate([0, -len, 0]) cbox([cal*3, cal*3.5, cal*3], 0.04);
  }
}
function zh_gun_muzzle(n=2, cal=0.22, len=3.0, base=1.8) = [0, -base*0.4 - len - cal*1.75, 0.33 + base*0.25];

// ==== TERTIARY: detail ===============================================================================================
// Shadow-gap floor (top frame): the black bottom of a reveal between modules.
module zh_reveal(w, l) zh_z("recess") translate([0, 0, 0.03]) cube([w, l, 0.06 + ZH_SINK], center=true);
// Cream safety chevrons (any frame, +Z out): n diagonal bars clipped to w x l.
module zh_stripes(w, l, n=3) zh_z("marking") intersection() {
  translate([0, 0, 0.04]) cube([w, l, 0.1], center=true);
  for (i = [0:n-1]) translate([(i - (n-1)/2)*w/n, 0, 0.04]) rotate([0, 0, 45]) cube([w/(n*2.2), l*3, 0.12], center=true);
}
// Cream block serial (any frame, +Z out; reads along local Y): stencil bars of a unit number.
module zh_serial(h=0.7, n=4) zh_z("marking") for (i = [0:n-1])
  translate([0, (i - (n-1)/2)*h*0.75, 0.03]) cube([h*(i % 2 == 0 ? 1 : 0.6), h*0.42, 0.08], center=true);

// ==== palette ========================================================================================================
// Value carries the 48 px read on dark space, within the lore palette (operator: solved within the palette): iron grey
// masses are the LIGHT value, charcoal fittings the mid-dark, reveals near black, oxide red the one hue, and the lore's
// cream serials/safety marks are the brightest small value. Five zones become livery mask channels.
ZH_ZONES = [["primary", [0.47, 0.48, 0.50]],     // iron grey: poured masses (core, modules, tiers)
            ["secondary", [0.21, 0.21, 0.23]],   // charcoal: cab, pods, collars, hinge bars, troughs, plates, lugs
            ["accent", [0.56, 0.20, 0.12]],      // dull oxide red: hatches, collar guide lugs, tow eye
            ["recess", [0.05, 0.05, 0.06]],      // shadow gaps, bores, slits, nozzle mouths
            ["marking", [0.86, 0.80, 0.64]]];    // cream: serials and safety chevrons
ZH_GLOW = [1.0, 0.62, 0.25];
ZH_RADIATOR = [0.56, 0.57, 0.58];
// [faction, zones, body zone, glow, radiator, [height/width range, width/length range], symmetry rule, maker]
// maker: the Zhestokost Faction record key in GameData/Aetheria.cc, the Manufacturer of a product whose hull speaks this language
// Low and broad: a body never taller than ~0.7 of its width; wide-for-length, as a tracked vehicle rather than a dart.
ZH_LANG = ["zhestokost", ZH_ZONES, "primary", ZH_GLOW, ZH_RADIATOR, [[0.22, 0.7], [0.32, 0.8]], ["bilateral", 0.96],
           "16bad4f9-7fb5-41c5-8da0-dc76709eb7d3"];
if (part == "lang") echo(lang = ZH_LANG);
