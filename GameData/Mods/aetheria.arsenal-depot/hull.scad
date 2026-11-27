// Zhestokost Arsenal Depot, a station: the hull bench's cast-iron Dutch oven with its lid on (AetheriaLore, Brainstorming/Ship
// Prompt Bench.md, Stations and Station Modules). One squat charcoal pot, a heavy iron-grey stepped lid with a round knob
// for its control tower, two thick lugs on opposite flanks that are the docking piers, four broad magazine bands round the
// pot wall with oxide hatches. Built only from the Zhestokost kit and lowpoly primitives.
// part = lang | brief | anchors | lines | primary | secondary | accent | recess | marking | rad:<id>
// Frame: Ship Root, metres, nose -Y, starboard -X, up +Z. The piers stand on the X axis; the rest of the outline is round.
include <zhestokost.scad>
part = "primary";

HULL_FACTION = "zhestokost";
HULL_DONOR = "";
HULL_ROLE = "depot";
HULL_LENGTH = [20, 26];
HULL_TRIANGLES = [800, 9000];
HULL_READS = ["the hull is a cast-iron Dutch oven with its lid on: one squat thick charcoal pot, a heavy stepped iron lid, a round knob on top",
              "the knob is the control tower, ringed by narrow observation slits",
              "two thick lugs on opposite flanks are the docking piers, each ending in one square-set docking collar with guide lugs",
              "four broad magazine bands ring the pot wall, oxide-red square hatches set in them",
              "two radiator troughs on the lid, either side of the knob; no thrusters and no guns, a depot does not move or fight"];
if (part == "brief") echo(brief = [HULL_FACTION, HULL_DONOR, HULL_ROLE, HULL_LENGTH, HULL_TRIANGLES, HULL_READS]);

POT_R = 11.7;                      // pot wall circumradius; its top-view raster must stay a disc of about 12 cells
POT_H = 7.2;
BAND_R = 11.85;                    // bands stand 0.15 m proud: the widest the top view gets (23.7 m, 12 cells)
BANDS = [[0.5, 2.0], [2.3, 3.8], [4.1, 5.6], [5.9, 7.1]];
LID = [[POT_H - 0.2, 11.85], [8.7, 10.0], [10.4, 8.0]];   // [z, radius]: lip, first step, deck
DECK_Z = 10.4;
TOWER_R = 2.4;
TOWER_TOP = 13.2;
LUG_X = [9.4, 13.1];               // piers: x from the pot into the flank; the collars end at 13.95 m
LUG_Y = 4.0;
LUG_H = 5.2;
HATCH_AT = [50, 130, 230, 310];    // azimuths, deg: clear of the piers on the X axis and of the y extremes
RAD_Y = 4.9; RAD_W = 5.2; RAD_L = 2.4;
RADS = [["Ra.N1", -1, 1], ["Ra.N2", -1, -1], ["Ra.S1", 1, 1], ["Ra.S2", 1, -1]];   // [id, trough y sign, trough side]

// A round mass from z0 to z1, r0 at the base and r1 at the top.
module cone(z0, z1, r0, r1, fn=24) translate([0, 0, z0]) cylinder(r1=r0, r2=r1, h=z1 - z0, $fn=fn);

// pot (charcoal) with its bottom chamfer
zh_z("secondary") {
  cone(0, 0.6, POT_R - 0.9, POT_R);
  cone(0.6, POT_H, POT_R, POT_R);
}
// four magazine bands, proud of the pot
for (b = BANDS) zh_z("secondary") cone(b[0], b[1], BAND_R, BAND_R);
// lid: a lip, then two steps up to a flat deck (iron grey)
zh_z("primary") {
  cone(LID[0][0], 7.7, LID[0][1], LID[0][1]);
  cone(7.7, LID[1][0], LID[0][1], LID[1][1]);
  cone(LID[1][0], LID[2][0] + 0.05, LID[1][1], LID[2][1]);
  // knob: the control tower
  cone(DECK_Z - 0.05, TOWER_TOP, TOWER_R + 0.5, TOWER_R, 12);
}
zh_z("secondary") cone(TOWER_TOP - 0.05, TOWER_TOP + 0.45, TOWER_R + 0.35, TOWER_R + 0.35, 12);
// observation slits ring the tower: black recesses sunk in its wall
zh_z("recess") for (k = [0:11]) rotate([0, 0, k*30 + 15]) translate([TOWER_R - 0.05, 0, 12.2]) cube([0.28, 0.5, 0.45], center=true);
// the piers: a thick lug on each flank, one docking collar on each outer face
for (s = [-1, 1]) {
  zh_z("secondary") translate([s*(LUG_X[0] + LUG_X[1])/2, 0, LUG_H/2]) cbox([LUG_X[1] - LUG_X[0], LUG_Y, LUG_H], 0.3);
  lp_at(t=[s*LUG_X[1], 0, LUG_H/2], r=[0, 90*s, 0]) zh_collar(1.5, 0.55);
  lp_at(t=[s*(LUG_X[0] + LUG_X[1])/2, 0, LUG_H], r=[0, 0, 90]) zh_stripes(2.4, 1.4, 3);
}
// magazine hatches on each band, at four azimuths; cream serials on the bands between them
for (b = BANDS, a = HATCH_AT)
  lp_at(t=[BAND_R*cos(a), BAND_R*sin(a), (b[0] + b[1])/2], r=[90, 0, a + 90]) zh_hatch(1.9, 1.0, dogs=false);
for (a = [80, 100, 260, 280])
  lp_at(t=[BAND_R*cos(a), BAND_R*sin(a), 3.05], r=[90, 0, a + 90]) zh_serial(0.5, 3);
// two radiator troughs on the deck, either side of the knob
for (y = [-RAD_Y, RAD_Y]) lp_at(t=[0, y, DECK_Z]) zh_rad_trough(RAD_W, RAD_L, 0.45);
// silhouette lines: the pot's cast belt and the lid deck edge
lp_line("outline", [for (a = [0:15:345]) [BAND_R*cos(a), BAND_R*sin(a), POT_H]], true);
lp_line("step", [for (a = [0:15:345]) [LID[2][1]*cos(a), LID[2][1]*sin(a), DECK_Z]], true);

// ---- anchor meshes: each radiator is one half of a trough ----
for (r = RADS) if (part == str("rad:", r[0])) lp_at(t=[0, r[1]*RAD_Y, DECK_Z]) zh_rad_vanes(RAD_W, RAD_L, r[2], 4, 1.0);
if (part == "anchors") for (r = RADS)
  echo(anchor=[r[0], "radiator-mesh", [0, r[1]*RAD_Y, DECK_Z] + zh_rad_centre(RAD_W, r[2], 1.0), 0, ""]);
