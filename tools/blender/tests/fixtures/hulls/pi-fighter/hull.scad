// Pirate Coalition fighter "Markdown" on a stolen Rossum & Douglas wedge. An agent-built hull source: the donor body
// comes from the Rossum & Douglas language, every addition from the Pirate language.
// part = lang | brief | anchors | rnd:<zone> | primer | weld | accent | recess | emit:<id>
include <rnd.scad>
include <pirate.scad>
part = "rnd:primary";

HULL_FACTION = "pirate";
HULL_DONOR = "rnd";
HULL_ROLE = "fighter";
HULL_LENGTH = [9, 16];
HULL_TRIANGLES = [300, 6000];
HULL_READS = ["the stolen R&D wedge is still recognisable (flat dorsal plane, blunt nose, flush engine face)",
              "one oversized gun out of proportion to the hull, on an adapted mount",
              "a replacement drive that does not match", "bolted salvage plates, an exposed cable run, one stolen sensor pod",
              "asymmetric"];
if (part == "brief") echo(brief = [HULL_FACTION, HULL_DONOR, HULL_ROLE, HULL_LENGTH, HULL_TRIANGLES, HULL_READS]);

L = 11; WR = 8; WN = 2.4;
module cut() { translate([-WR/2+0.4, L/2-0.6, 0]) rotate([0,0,35]) cube([3.2, 2.6, 3], center=true); }
module wedge() { difference() { rd_wedge(L, WR, WN, 0.55, 1.5); cut(); translate([0, L/2, 0]) rd_engine_bank(5.4, 0.4, 3); translate([0, -L/2+2.2, 0.75]) rd_window(1.4); } }
module stripe() { translate([1.1, 0.5, 0.75]) cube([0.9, L*0.55, 0.04], center=true); }
module donor_hatches() { for (y=[-1.2, 1.4]) translate([-1.4, y, 0.75]) rd_hatch(); }
module gun() { translate([2.3, -0.8, 0.45]) { pi_saddle(1.2, 2.4, 0.5); translate([0, 0, 0.9]) pi_big_gun(9.0, 0.18); } }
module drive() { translate([-2.5, L/2-0.2, 0.15]) { pi_drive(1.05, 2.6);
    for (a=[[0.9,-1.4,0.2],[-0.9,-1.4,0.2],[0,-1.4,0.9]]) strut([0,0,0], a, 0.07); } }
module patch() { translate([-WR/2+1.25, L/2-1.25, 0.2]) rotate([0,0,35]) rotate([0,90,0]) pi_plate(0.7, 2.8, 0.12, 0); }
module plates() { translate([1.0, -L/2+1.9, 0.62]) pi_plate(1.4, 1.2, 0.12, 6); translate([-0.6, 2.4, 0.8]) pi_plate(1.8, 1.4, 0.12, -5); }
module welded_engine() { translate([1.8, L/2+0.05, 0]) cube([1.6, 0.12, 0.4], center=true); }
module cables() { pi_cable([[2.0, 0.6, 1.0], [1.0, 1.6, 0.9], [-0.6, 2.8, 0.95], [-1.6, 3.6, 0.85], [-2.3, 4.0, 0.9]]); }
module pod() { translate([-1.4, -2.6, 1.3]) pi_sensor_pod(1.5, 0.32); strut([-1.4, -2.6, 0.7], [-1.4, -2.6, 1.1], 0.07); }
module crew_slash() { translate([-0.2, -1.6, 0.78]) rotate([0,0,-38]) cube([0.55, 4.4, 0.04], center=true); }

if (part == "rnd:primary") wedge();
if (part == "rnd:secondary") donor_hatches();
if (part == "rnd:accent") stripe();
if (part == "rnd:recess") { translate([0, -L/2+2.2, 0.72]) rd_window(1.3); translate([0, L/2-0.25, 0]) cube([1.6, 0.05, 0.36], center=true); }
if (part == "primer") { patch(); plates(); welded_engine(); }
if (part == "weld") { gun(); drive(); cables(); }
if (part == "accent") { crew_slash(); pod(); }
if (part == "recess") translate([-2.5, L/2-0.2, 0.15]) pi_collar(1.15, 0.15);
if (part == "emit:Th.Main") translate([-2.5, L/2-0.2+1.17, 0.15]) rotate([90,0,0]) cylinder(r=0.95, h=0.05, center=true, $fn=8);
if (part == "emit:Th.L")    translate([0, L/2-0.22, 0]) cube([1.2, 0.05, 0.32], center=true);
// Ids are HullData mount ids: the gameplay layout addresses these by name only.
if (part == "anchors") {
  echo(anchor=["Gun.P", "weapon-mount", [2.3, -0.8, 1.35], 0, ""]);
  echo(anchor=["Gun.P.muzzle", "weapon-muzzle", [2.3, -0.8-0.17*9.0-0.65*9.0-0.4, 1.4], 0, "Gun.P"]);
  echo(anchor=["Th.Main", "thruster-emitter", [-2.5, L/2-0.2+1.17, 0.15], 180, ""]);
  echo(anchor=["Th.L", "thruster-emitter", [0, L/2-0.22, 0], 180, ""]);
}
