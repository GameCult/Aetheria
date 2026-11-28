// Pirate Coalition station "Seized Refinery" on a stolen Zhestokost donor: a cast-iron pot-bellied stove, the furnace, with
// Pirate additions welded on one side. An agent-built hull source: the donor body and its fittings come from the Zhestokost
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
HULL_READS = ["the stolen Zhestokost body is still a cast-iron pot-bellied stove: one squat round firebox belly, a flat cooktop, a tall stovepipe that is the control tower",
              "a square firebox door on the nose with a docking collar in it is the ore intake, an ash-pan drawer below it",
              "bolted primer-red salvage plates cover a breach in the belly, an exposed cable run climbs from a stolen reactor saddle on the cooktop to a second docking collar welded on the starboard flank",
              "one oversized stolen gun on an adapted saddle, out of proportion to the stove",
              "Zhestokost stencils on the belly are crossed out in the crew's own colour",
              "no thrusters, a refinery does not move; asymmetric"];
if (part == "brief") echo(brief = [HULL_FACTION, HULL_DONOR, HULL_ROLE, HULL_LENGTH, HULL_TRIANGLES, HULL_READS]);

R = 10.0;                         // belly wall radius
TOP_R = 7.4;                      // cooktop radius
TOP = 9.0;                        // cooktop height
PIPE = [0, 1.0];                  // stovepipe centre
PIER_X = [R - 0.8, R + 3.8];      // the welded pier on the starboard flank: x from the belly out (mirrored to -X)
PIER_Y = -1.5;
RAD_W = 2.6; RAD_L = 3.6;
TROUGH = [[4.1, 1.0], [-4.1, 1.0]];
RADS = [["Ra.1", 0, 1], ["Ra.2", 1, -1]];     // [id, trough index, side of the trough its vanes stand in]

// A round mass from z0 to z1, r0 at the base and r1 at the top.
module cone(z0, z1, r0, r1, fn=28) translate([0, 0, z0]) cylinder(r1=r0, r2=r1, h=z1 - z0, $fn=fn);
// A frame on the belly wall at azimuth a (deg), height z: local +Z points out of the wall.
module on_wall(a, z, out=0) lp_at(t=[(R + out)*cos(a), (R + out)*sin(a), z], r=[90, 0, a + 90]) children();

// ---- the donor: the stove ----
// firebox belly (body): a chamfered foot, the cast belly, a battered shoulder up to the cooktop
zh_z("primary") {
  cone(0.4, 1.4, R - 1.0, R);
  cone(1.4, 6.0, R, R);
  cone(6.0, TOP - 0.4, R, TOP_R - 0.2);
}
// the cooktop, a cast waist belt, the stovepipe's flared foot and its cap
zh_z("secondary") {
  cone(TOP - 0.6, TOP, TOP_R, TOP_R - 0.2);
  cone(3.0, 3.6, R + 0.15, R + 0.15);
  cone(5.0, 5.5, R + 0.15, R + 0.15);
  translate([PIPE[0], PIPE[1], 0]) { cone(TOP - 0.05, TOP + 1.3, 2.4, 1.5, 12); cone(TOP + 6.9, TOP + 7.5, 1.85, 1.85, 12); }
}
// the stovepipe: the control tower, ringed by narrow observation slits
zh_z("primary") translate([PIPE[0], PIPE[1], 0]) cone(TOP + 1.2, TOP + 7.0, 1.5, 1.3, 12);
zh_z("recess") translate([PIPE[0], PIPE[1], 0]) for (k = [0:9]) rotate([0, 0, k*36 + 18]) translate([1.3, 0, TOP + 5.4]) cube([0.3, 0.55, 0.5], center=true);
// the firebox door on the nose: the ore intake, with the docking collar in it; the ash-pan drawer below
lp_at(t=[0, -R, 3.2], r=[90, 0, 0]) zh_hatch(4.4, 3.6, dogs=false);
lp_at(t=[0, -R - 0.2, 3.2], r=[90, 0, 0]) zh_collar(1.6, 0.6);
zh_z("secondary") translate([0, -R - 0.5, 0.95]) cbox([5.0, 2.6, 1.5], 0.2);
zh_z("accent") translate([0, -R - 1.85, 0.95]) cbox([2.6, 0.4, 0.4], 0.08);
// two radiator troughs on the cooktop, either side of the pipe
for (t = TROUGH) lp_at(t=[t[0], t[1], TOP]) zh_rad_trough(RAD_W, RAD_L, 0.45);
// Zhestokost stencils on the belly flanking the door
for (a = [245, 295]) on_wall(a, 3.2) zh_serial(0.9, 3);
// silhouette lines: the belly's cast belt and the cooktop edge
lp_line("outline", [for (a = [0:15:345]) [(R + 0.15)*cos(a), (R + 0.15)*sin(a), 3.6]], true);
lp_line("step", [for (a = [0:15:345]) [TOP_R*cos(a), TOP_R*sin(a), TOP]], true);

// ---- the Pirate additions, one side and unmatched ----
// primer-red salvage plates bolted over a breach in the starboard rear belly
module wall_plate(a, z, w, l, tilt) translate([R*cos(a), R*sin(a), z]) rotate([0, 0, a + 90]) rotate([90, 0, 0]) pi_plate(w, l, 0.14, tilt);
if (part == "primer") {
  wall_plate(148, 2.6, 3.2, 3.0, 4); wall_plate(162, 4.3, 2.4, 2.0, -6); wall_plate(136, 1.9, 1.8, 1.6, 7);
  translate([-4.6, 5.0, TOP + 0.1]) pi_plate(2.2, 1.8, 0.12, 5);
}
// the welded pier with its second docking collar, the stolen reactor on its saddle, the exposed cable run, the oversized gun
module cables() {
  pi_cable([[-3.0, -3.9, TOP + 1.0], [-5.4, -3.5, TOP + 0.6], [-7.8, -3.0, 7.9], [-10.4, -2.6, 5.0], [-11.4, -2.0, 4.6]]);
  pi_cable([[-3.4, -3.3, TOP + 0.8], [-5.6, -2.4, TOP + 0.5], [-7.9, -1.9, 7.8], [-10.5, -1.4, 5.0], [-11.6, -1.2, 4.6]], 0.05);
}
if (part == "weld") {
  translate([-(PIER_X[0] + PIER_X[1])/2, PIER_Y, 2.6]) cbox([PIER_X[1] - PIER_X[0], 6.2, 3.6], 0.2);
  translate([-(PIER_X[0] + 2.2), PIER_Y, 5.1]) rotate([0, 0, 90]) pi_plate(1.6, 1.4, 0.12, 3);
  strut([-(PIER_X[0] + 0.4), PIER_Y - 1.4, 4.4], [-(R - 0.6), PIER_Y - 3.4, 6.4], 0.12);
  strut([-(PIER_X[0] + 0.4), PIER_Y + 1.4, 4.4], [-(R - 0.4), PIER_Y + 3.2, 6.0], 0.12);
  translate([-(PIER_X[1] + 0.05), PIER_Y, 2.6]) rotate([0, 90, 0]) pi_collar(1.8, 0.3);
  translate([-3.0, -3.8, TOP]) { pi_saddle(2.6, 3.2, 1.0); translate([0, 0, 1.0]) cylinder(r=0.85, h=1.3, $fn=10); }
  cables();
  translate([2.8, -3.2, TOP]) { pi_saddle(1.4, 2.6, 0.6); translate([0, 0, 1.0]) pi_big_gun(8.0, 0.2); }
}
if (part == "recess") translate([-(PIER_X[1] + 0.2), PIER_Y, 2.6]) rotate([0, 90, 0]) pi_collar(1.25, 0.2);
// the crew's own colour: one slash across a crossed-out stencil, one across the cooktop, a sensor pod on the stovepipe
if (part == "accent") {
  translate([295 < 0 ? 0 : (R + 0.06)*cos(295), (R + 0.06)*sin(295), 3.2]) rotate([0, 0, 295 + 90]) rotate([90, 0, 0]) rotate([0, 0, 40]) cube([3.4, 0.4, 0.12], center=true);
  translate([-0.5, -1.2, TOP + 0.04]) rotate([0, 0, -38]) cube([0.7, 7.0, 0.06], center=true);
  translate([2.5, PIPE[1] + 0.4, TOP + 4.4]) pi_sensor_pod(1.6, 0.34);
  strut([1.2, PIPE[1] + 0.2, TOP + 4.2], [2.5, PIPE[1] + 0.4, TOP + 4.4], 0.07);
}

// ---- anchor meshes: each radiator is one half of a trough; the other half was stripped ----
for (r = RADS) if (part == str("rad:", r[0]))
  lp_at(t=[TROUGH[r[1]][0], TROUGH[r[1]][1], TOP]) zh_rad_vanes(RAD_W, RAD_L, r[2], 4, 1.0);
if (part == "anchors") for (r = RADS)
  echo(anchor=[r[0], "radiator-mesh", [TROUGH[r[1]][0], TROUGH[r[1]][1], TOP] + zh_rad_centre(RAD_W, r[2], 1.0), 0, ""]);
