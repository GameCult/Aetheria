// Pirate Coalition station "Seized Refinery" on a stolen Zhestokost donor: a cast-iron pot-bellied stove, the furnace, with
// Pirate additions welded on one side. The firebox is an octagonal cast box, not a drum: flat faces keep the station's interior roomy. An agent-built hull source: the donor body and its fittings come from the Zhestokost
// language (zones keyed zhestokost:<zone>), every addition from the Pirate language. No bench prompt names a refinery; this
// is a first proposal of the form, not a ruling on it.
// part = lang | brief | anchors | lines | marks | zhestokost:<zone> | primer | weld | accent | recess | rad:<id>
// Frame: Ship Root, metres, nose -Y, starboard -X, up +Z. The docking door faces the nose; the stovepipe is the control tower.
include <zhestokost.scad>
include <pirate.scad>
part = "zhestokost:primary";

HULL_FACTION = "pirate";
HULL_DONOR = "zhestokost";
HULL_ROLE = "refinery";
HULL_LENGTH = [14, 26];
HULL_TRIANGLES = [800, 9000];
HULL_READS = ["the stolen Zhestokost body is still a cast-iron pot-bellied stove: one squat octagonal firebox belly, a flat cooktop, a tall stovepipe that is the control tower",
              "a square firebox door on the nose with a docking collar in it is the ore intake, an ash-pan drawer below it",
              "bolted primer-red salvage plates cover a breach in the belly, an exposed cable run climbs from a stolen reactor saddle on the cooktop to a second docking collar welded on the starboard flank",
              "one oversized stolen gun on an adapted saddle, out of proportion to the stove",
              "Zhestokost stencils on the belly are crossed out in the crew's own colour",
              "no thrusters, a refinery does not move; asymmetric"];
if (part == "brief") echo(brief = [HULL_FACTION, HULL_DONOR, HULL_ROLE, HULL_LENGTH, HULL_TRIANGLES, HULL_READS]);

A = 10.2;                         // belly apothem (centre to the middle of a flat face)
TOP_A = 7.6;                      // cooktop apothem
TOP = 9.0;                        // cooktop height
PIPE = [0, 1.0];                  // stovepipe centre
PIER_X = [A - 0.8, A + 3.0];      // the welded pier on the starboard flank: x from the belly out (mirrored to -X)
PIER_Y = -1.5;
PIER_W = 9.0;                     // the pier's width along Y
RAD_W = 2.6; RAD_L = 3.6;
TROUGH = [[4.1, 1.0], [-4.1, 1.0]];
RADS = [["Ra.1", 0, 1], ["Ra.2", 1, -1]];     // [id, trough index, side of the trough its vanes stand in]

// A round mass from z0 to z1, r0 at the base and r1 at the top.
module cone(z0, z1, r0, r1, fn=28) translate([0, 0, z0]) cylinder(r1=r0, r2=r1, h=z1 - z0, $fn=fn);
// An octagonal mass, flat faces on the axes, from z0 to z1; a0 and a1 are apothems.
module oct(z0, z1, a0, a1) rotate([0, 0, 22.5]) cone(z0, z1, a0/cos(22.5), a1/cos(22.5), 8);
function oct_ring(a, z) = [for (k = [0:7]) let(t = 22.5 + 45*k) [a/cos(22.5)*cos(t), a/cos(22.5)*sin(t), z]];
// A frame on a flat face of the belly at azimuth a (a multiple of 45), height z: local +Z points out of the face.
module on_wall(a, z, out=0) lp_at(t=[(A + out)*cos(a), (A + out)*sin(a), z], r=[90, 0, a + 90]) children();

// ---- the donor: the stove ----
// firebox belly (body): a chamfered foot, the cast belly, a battered shoulder up to the cooktop
zh_z("primary") {
  oct(0.4, 1.4, A - 1.0, A);
  oct(1.4, 6.0, A, A);
  oct(6.0, TOP - 0.4, A, TOP_A - 0.2);
}
// the cooktop, two cast belts, the stovepipe's flared foot and its cap
zh_z("secondary") {
  oct(TOP - 0.6, TOP, TOP_A, TOP_A - 0.2);
  oct(3.0, 3.6, A + 0.15, A + 0.15);
  oct(5.0, 5.5, A + 0.15, A + 0.15);
  translate([PIPE[0], PIPE[1], 0]) { cone(TOP - 0.05, TOP + 1.3, 2.4, 1.5, 12); cone(TOP + 6.9, TOP + 7.5, 1.85, 1.85, 12); }
}
// the stovepipe: the control tower, ringed by narrow observation slits
zh_z("primary") translate([PIPE[0], PIPE[1], 0]) cone(TOP + 1.2, TOP + 7.0, 1.5, 1.3, 12);
zh_z("recess") translate([PIPE[0], PIPE[1], 0]) for (k = [0:9]) rotate([0, 0, k*36 + 18]) translate([1.3, 0, TOP + 5.4]) cube([0.3, 0.55, 0.5], center=true);
// the firebox door on the nose face: the ore intake, with the docking collar in it; the ash-pan drawer below
lp_at(t=[0, -A, 3.2], r=[90, 0, 0]) zh_hatch(4.4, 3.6, dogs=false);
lp_at(t=[0, -A - 0.2, 3.2], r=[90, 0, 0]) zh_collar(1.6, 0.6);
zh_z("secondary") translate([0, -A - 0.5, 0.95]) cbox([5.0, 2.6, 1.5], 0.2);
zh_z("accent") translate([0, -A - 1.85, 0.95]) cbox([2.6, 0.4, 0.4], 0.08);
// two radiator troughs on the cooktop, either side of the pipe
for (t = TROUGH) lp_at(t=[t[0], t[1], TOP]) zh_rad_trough(RAD_W, RAD_L, 0.45);
// Zhestokost stencils on the two faces flanking the door
for (a = [225, 315]) on_wall(a, 3.2) zh_serial(0.9, 3);
// silhouette lines: the belly's cast belt and the cooktop edge
lp_line("outline", oct_ring(A + 0.15, 3.6), true);
lp_line("step", oct_ring(TOP_A, TOP), true);

// ---- the Pirate additions, one side and unmatched ----
// primer-red salvage plates bolted over a breach in the starboard rear face
module wall_plate(a, y, z, w, l, tilt) translate([A*cos(a), y, z]) rotate([0, 0, a + 90]) rotate([90, 0, 0]) pi_plate(w, l, 0.14, tilt);
if (part == "primer") {
  translate([-(A + 0.02)*cos(45), (A + 0.02)*sin(45), 0]) {}   // (faces at 135 degrees carry the plates below)
  for (p = [[2.6, 2.4, 3.2, 3.0, 4], [4.3, -0.8, 2.4, 2.0, -6], [1.9, 3.8, 1.8, 1.6, 7]])
    translate([A*cos(135) + 0.0, A*sin(135) + 0, 0]) rotate([0, 0, 135 + 90]) translate([p[1], 0, p[0]]) rotate([90, 0, 0]) pi_plate(p[2], p[3], 0.14, p[4]);
  translate([-4.6, 5.0, TOP + 0.1]) pi_plate(2.2, 1.8, 0.12, 5);
}
// the welded pier with its second docking collar, the stolen reactor on its saddle, the exposed cable run, the oversized gun
module cables() {
  pi_cable([[-3.0, -3.9, TOP + 1.0], [-5.4, -3.5, TOP + 0.6], [-(TOP_A + 0.8), -3.0, TOP - 1.2], [-(A + 0.1), -2.6, 5.0], [-(A + 1.2), -2.0, 4.6]]);
  pi_cable([[-3.4, -3.3, TOP + 0.8], [-5.6, -2.4, TOP + 0.5], [-(TOP_A + 0.9), -1.9, TOP - 1.3], [-(A + 0.1), -1.4, 5.0], [-(A + 1.4), -1.2, 4.6]], 0.05);
}
if (part == "weld") {
  translate([-(PIER_X[0] + PIER_X[1])/2, PIER_Y, 2.6]) cbox([PIER_X[1] - PIER_X[0], PIER_W, 3.6], 0.2);
  translate([-(PIER_X[0] + 1.6), PIER_Y, 5.1]) rotate([0, 0, 90]) pi_plate(1.6, 1.4, 0.12, 3);
  strut([-(PIER_X[0] + 0.4), PIER_Y - 1.4, 4.4], [-(A - 0.6), PIER_Y - 3.4, 6.4], 0.12);
  strut([-(PIER_X[0] + 0.4), PIER_Y + 1.4, 4.4], [-(A - 0.4), PIER_Y + 3.2, 6.0], 0.12);
  translate([-(PIER_X[1] + 0.05), PIER_Y, 2.6]) rotate([0, 90, 0]) pi_collar(1.8, 0.3);
  translate([-3.0, -3.8, TOP]) { pi_saddle(2.6, 3.2, 1.0); translate([0, 0, 1.0]) cylinder(r=0.85, h=1.3, $fn=10); }
  cables();
  translate([2.8, -3.2, TOP]) { pi_saddle(1.4, 2.6, 0.6); translate([0, 0, 1.0]) pi_big_gun(8.0, 0.2); }
}
if (part == "recess") translate([-(PIER_X[1] + 0.2), PIER_Y, 2.6]) rotate([0, 90, 0]) pi_collar(1.25, 0.2);
// the crew's own colour: one slash across a crossed-out stencil, one across the cooktop, a sensor pod on the stovepipe
if (part == "accent") {
  translate([(A + 0.06)*cos(315), (A + 0.06)*sin(315), 3.2]) rotate([0, 0, 315 + 90]) rotate([90, 0, 0]) rotate([0, 0, 40]) cube([3.4, 0.4, 0.12], center=true);
  translate([-0.5, -1.2, TOP + 0.04]) rotate([0, 0, -38]) cube([0.7, 7.0, 0.06], center=true);
  translate([2.5, PIPE[1] + 0.4, TOP + 4.4]) pi_sensor_pod(1.6, 0.34);
  strut([1.2, PIPE[1] + 0.2, TOP + 4.2], [2.5, PIPE[1] + 0.4, TOP + 4.4], 0.07);
}

// ---- anchor meshes: each radiator is one half of a trough; the other half was stripped ----
for (r = RADS) if (part == str("rad:", r[0]))
  lp_at(t=[TROUGH[r[1]][0], TROUGH[r[1]][1], TOP]) zh_rad_vanes(RAD_W, RAD_L, r[2], 4, 1.0);
if (part == "anchors") for (r = RADS)
  echo(anchor=[r[0], "radiator-mesh", [TROUGH[r[1]][0], TROUGH[r[1]][1], TOP] + zh_rad_centre(RAD_W, r[2], 1.0), 0, ""]);
