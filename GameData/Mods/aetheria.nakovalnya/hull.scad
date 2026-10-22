// Zhestokost fortress (boss) "Nakovalnya" (anvil). Built from the Zhestokost kit.
// part = lang | brief | anchors | lines | primary | secondary | accent | recess | marking | emit:<id> | rad:<id>
// Frame: Ship Root, metres, nose -Y, starboard -X, up +Z. Anchor yaw: 0 nose, 90 port, 180 aft, 270 starboard.
include <zhestokost.scad>
part = "primary";

HULL_FACTION = "zhestokost";
HULL_DONOR = "";
HULL_ROLE = "fortress";
HULL_LENGTH = [45, 60];
HULL_TRIANGLES = [6000, 12000];
HULL_READS = ["three long thick heavy barrels from fixed armoured casemates built into the stepped prow, all pointing straight forward",
              "a ring of ten small squat flak turrets on raised rings evenly spaced around the upper edge",
              "huge low broad stepped mass, wider at the bow, tapering to a squared stern; narrow cockpit slit high on the prow",
              "round feed collar with guide lugs and an oxide ammunition door on each flank, magazine hatches in rows on the dorsal plating; six turning blocks and two broad stern drives"];
if (part == "brief") echo(brief = [HULL_FACTION, HULL_DONOR, HULL_ROLE, HULL_LENGTH, HULL_TRIANGLES, HULL_READS]);

// ---- masses: three steps (hull, mid-deck, spine) with the casemates set into the prow's lowest step -----------------
// M: the hull, broadest at the bow, tapering aft to a squared stern with a cast lip. Its bow face is set back so the
// casemates sit inside the bow line.
M = [zh_s(-16, 32, 5.0, -2.25), zh_s(-15, 34, 5.5, -2.5), zh_s(5, 33, 5.5, -2.5), zh_s(14, 27, 5.5, -2.5),
     zh_s(19.6, 23, 5.5, -2.5), zh_s(20, 22.4, 5.2, -2.35)];
DECK = 3.0;
// T1: the mid-deck, ~60% of the hull's width, 3 m up; the outer band is the flak ring's gallery.
T1 = zh_tier([zh_sec(M, -15), zh_sec(M, 5), zh_sec(M, 11)], 5.4, 3.0, 4.0);
// T2: the spine, 2 m above that, carrying the cab and its slit at the prow's top step.
T2 = zh_tier(T1, 4.0, 2.0, 2.5);
// the spine runs on aft at the same height to the stern shoulders
SPINE = zh_block(12.0, 12, 8, 5.05, DECK - 0.05, 0.8, 0, 0.5);
// the belly steps down too: a keel block under the centre
KEEL = zh_block(0.5, 30, 18, 1.6, -4.0, 0, 0.6, 0.8);
// The prow's lowest step: a glacis apron, and three casemates whose tops are flush with the hull deck.
APRON = zh_block(-18.5, 8, 26, 2.6, -2.7, 0.6, 0.5, 0.4);
CX = [-9.6, 0, 9.6];
CAS = zh_block(-18.0, 10, 7.2, 5.7, -2.7, 1.3, 0.9, 0.6);
GY = -23.0;                        // casemate face
GZ = 0.1;                          // barrel axis height
BL = 11.0; BR = 0.9;               // barrel length, radius
// stern shoulders: radiator troughs either side of the spine
RAD_Y = 12.4; RAD_U = 0.55; RAD_W = 3.0; RAD_L = 2.4;
// stern drives
DX = 6.0; DRV = zh_block(20.6, 4, 8, 4.6, -2.3, 0.9, 0.7, 0.4);
DRIVE_Y = 22.6; DZ = -2.3 + 2.3;

zh_mass(M);
zh_mass(T1);
zh_mass(T2);
zh_mass(APRON);
for (x = CX) lp_at(t=[x, 0, 0]) zh_mass(CAS);
zh_mass(SPINE);
zh_mass(KEEL, lines=false);                       // under the hull: no top view sees it
for (s = [-1, 1]) lp_at(t=[s*DX, 0, 0]) zh_mass(DRV, "secondary");

// ---- the casemate guns: lighter cast-steel barrels out of charcoal mantlets -----------------------------------------
MANTLET = [4.0, 1.2, 3.4];
module casemate_gun() {
  // mantlet frame: its fitting outline is drawn in the frame the mantlet is
  lp_at(t=[0, GY - 0.3, GZ]) {
    zh_z("secondary") cbox(MANTLET, 0.2);
    lp_line("fitting", zh_rect(MANTLET.x, MANTLET.y, MANTLET.z/2), true);
  }
  // barrel frame: local +Y runs forward from the collar; the barrel's top line is drawn in the frame the barrel is
  lp_at(t=[0, GY - 0.7, GZ], r=[0, 0, 180]) {
    zh_z("secondary") tube(BR*1.55, 1.8, 8);                                    // barrel collar
    zh_z("primary") tube(BR, BL, 8);
    zh_z("secondary") translate([0, BL, 0]) cbox([BR*2.7, 1.6, BR*2.7], 0.12);  // muzzle block
    zh_z("recess") translate([0, BL + 0.81, 0]) cube([BR*1.2, 0.05, BR*1.2], center=true);
    lp_line("fitting", [[0, 0, BR], [0, BL, BR]]);
  }
}
for (x = CX) lp_at(t=[x, 0, 0]) casemate_gun();
for (x = CX) lp_at(t=[x, 0, 0]) zh_on_top(CAS, -21.9, 0) zh_stripes(3.6, 0.6, 4);
// black reveals between the casemates, on the apron
for (u = [-0.33, 0.33]) zh_on_top(APRON, -19.5, u) zh_reveal(2.3, 7.0);

// ---- the flak ring: ten turrets on raised drums, equal arc-length around the hull's deck rim ----------------------
// [x, y, outward yaw]; F1 at the bow-port quarter, numbered clockwise seen from above; equal-arc-length stations, baked.
FLAK = [[5.43, -13.40, 21], [-5.43, -13.40, 339], [-13.77, -10.93, 311], [-13.50, -0.06, 274], [-11.53, 10.51, 230],
        [-5.43, 17.20, 199], [5.43, 17.20, 161], [11.53, 10.51, 130], [13.50, -0.06, 86], [13.77, -10.93, 49]];
FR = 2.2; FH = 1.7;                // raised drum
FG = [2, 0.27, 3.0, 3.2];          // zh_gun n, cal, len, base
module flak(yaw) {
  zh_z("secondary") cylinder(r=FR, h=FH + ZH_SINK, $fn=8);
  zh_z("accent") translate([0, 0, FH*0.35]) cylinder(r=FR + 0.08, h=0.35, $fn=8);   // oxide band round the drum
  zh_z("recess") translate([0, 0, FH - 0.06]) cylinder(r=FR*0.84, h=0.1, $fn=8);
  lp_line("fitting", zh_oct(FR, FH), true);
  lp_at(t=[0, 0, FH - 0.02], r=[0, 0, yaw]) zh_gun(FG[0], FG[1], FG[2], FG[3], "primary");
}
for (f = FLAK) lp_at(t=[f[0], f[1], DECK - ZH_SINK]) flak(f[2]);
function rz(a, p) = [p[0]*cos(a) - p[1]*sin(a), p[0]*sin(a) + p[1]*cos(a), p[2]];

// ---- bow: cab with the slit high on the spine's front step ---------------------------------------------------------
zh_on_top(T2, -6.4, 0) zh_cab(6.0, 3.4, 3.0, "primary");
zh_on_top(T2, -3.6, 0) zh_serial(0.7, 4);

// ---- dorsal magazine hatches: two rows of four on the mid-deck, clear of the rim -----------------------------------
for (y = [-8.0, -3.4, 1.2, 5.6], u = [-0.78, 0.78]) zh_on_top(T1, y, u) zh_hatch(2.8, 3.0, dogs=false);
PLATE_SEAM = [8.4, 0.5, 0.2];
for (y = [0.5, 4.5]) zh_on_top(T2, y, 0) lp_at(t=[0, 0, 0.06]) {                                          // plate seams
  zh_z("secondary") cube(PLATE_SEAM, center=true);
  lp_line("seam", [[-PLATE_SEAM.x/2, 0, PLATE_SEAM.z/2], [PLATE_SEAM.x/2, 0, PLATE_SEAM.z/2]]);
}

// Feed collar for an Oboz: a lighter cast-steel flange ring standing proud of the flank round the kit collar.
module feed_collar() {
  zh_z("primary") difference() {
    cylinder(r=3.3, h=0.9 + ZH_SINK, $fn=8); translate([0, 0, -0.1]) cylinder(r=2.1, h=2, $fn=8); }
  zh_collar(2.2, 1.5, 4);
}
// ---- flanks: feed collar, ammunition door, unit serial -------------------------------------------------------------
for (s = [-1, 1]) {
  zh_on_belt(M, 1.5, s, 0.5) lp_at(r=[0, 0, 22.5]) feed_collar();
  zh_on_wall(M, 7.0, s, 0.45) zh_hatch(2.6, 3.0);
  zh_on_wall(M, -9.0, s, 0.5) zh_serial(0.8, 5);
}

// ---- six square turning blocks on the belt, a small exhaust slot in each -------------------------------------------
TB = [2.8, 2.6, 1.8];              // along Y, down the wall, outward
TY = [-11.5, 13.6];
module turn_block() {
  zh_mass(zh_block(0, TB[0], TB[1], TB[2], 0, 0.45, 0.45, 0.35), "secondary");
  zh_z("recess") translate([0, 0, TB[2] + 0.02]) cube([0.6, 1.4, 0.14], center=true);
}
for (y = TY, s = [-1, 1]) zh_on_belt(M, y, s, 0.5) turn_block();
// the forward pair face ahead from the bow corners, outboard of the casemates
NX = 14.6;
for (s = [-1, 1]) zh_on_nose(M, s*NX, 0.55) turn_block();
function nb_at(s) = zh_nose_pt(M, s*NX, 0.55) + [0, ZH_SINK - TB[2] - 0.1, 0];
function tb_at(y, s) = zh_belt(M, y, s, 0.5) + [s*(TB[2] + 0.1 - ZH_SINK), 0, 0];

// ---- stern: drives, radiator troughs on the shoulders, stern plate ------------------------------------------------
module drive_mouth() {
  zh_z("secondary") translate([0, DRIVE_Y + 0.12, 0]) difference() {
    cbox([7.0, 0.4, 3.6], 0.1); cube([5.4, 1, 2.4], center=true); }
  zh_z("recess") translate([0, DRIVE_Y - 0.02, 0]) cube([5.5, 0.3, 2.5], center=true);
}
for (s = [-1, 1]) translate([s*DX, 0, DZ]) drive_mouth();
for (s = [-1, 1]) zh_on_top(M, RAD_Y, s*RAD_U) zh_rad_trough(RAD_W, RAD_L, 0.45);
zh_on_stern(M, 0, 0.62) zh_plate(4.0, 1.4, 0.3);
zh_on_stern(M, 0, 0.62) translate([0, 0, 0.3]) zh_stripes(3.4, 1.0, 4);

// ---- anchor meshes ------------------------------------------------------------------------------------------------
for (s = [-1, 1]) if (part == (s > 0 ? "rad:Ra.L" : "rad:Ra.R"))
  zh_on_top(M, RAD_Y, s*RAD_U) for (h = [-1, 1]) zh_rad_vanes(RAD_W, RAD_L, h, 4, 1.0);
for (s = [-1, 1]) if (part == (s > 0 ? "emit:Th.L" : "emit:Th.R"))
  translate([s*DX, DRIVE_Y + 0.15, DZ]) cube([5.2, 0.05, 2.3], center=true);
TID = [["Th.T3", TY[0], 1], ["Th.T4", TY[0], -1], ["Th.T5", TY[1], 1], ["Th.T6", TY[1], -1]];
for (t = TID) if (part == str("emit:", t[0])) translate(tb_at(t[1], t[2])) cube([0.05, 1.2, 0.4], center=true);
for (s = [-1, 1]) if (part == (s > 0 ? "emit:Th.T1" : "emit:Th.T2")) translate(nb_at(s)) cube([1.2, 0.05, 0.4], center=true);

if (part == "anchors") {
  for (i = [0:2]) {
    id = str("Gun.H", i + 1);
    echo(anchor=[id, "weapon-mount", [CX[i], GY, GZ], 0, ""]);
    echo(anchor=[str(id, ".muzzle"), "weapon-muzzle", [CX[i], GY - 0.7 - BL - 0.85, GZ], 0, id]);
  }
  for (i = [0:9]) {
    f = FLAK[i]; id = str("Gun.F", i + 1); base = [f[0], f[1], DECK - ZH_SINK + FH - 0.02];
    echo(anchor=[id, "weapon-mount", base + [0, 0, 0.8], f[2], ""]);
    echo(anchor=[str(id, ".muzzle"), "weapon-muzzle", base + rz(f[2], zh_gun_muzzle(FG[0], FG[1], FG[2], FG[3])), f[2], id]);
  }
  echo(anchor=["Th.T1", "thruster-emitter", nb_at(1), 0, ""]);
  echo(anchor=["Th.T2", "thruster-emitter", nb_at(-1), 0, ""]);
  for (t = TID) echo(anchor=[t[0], "thruster-emitter", tb_at(t[1], t[2]), t[2] > 0 ? 90 : 270, ""]);
  echo(anchor=["Th.L", "thruster-emitter", [DX, DRIVE_Y + 0.15, DZ], 180, ""]);
  echo(anchor=["Th.R", "thruster-emitter", [-DX, DRIVE_Y + 0.15, DZ], 180, ""]);
  echo(anchor=["Ra.L", "radiator-mesh", zh_top_at(M, RAD_Y, RAD_U, [0, 0, 0.5]), 0, ""]);
  echo(anchor=["Ra.R", "radiator-mesh", zh_top_at(M, RAD_Y, -RAD_U, [0, 0, 0.5]), 0, ""]);
}
