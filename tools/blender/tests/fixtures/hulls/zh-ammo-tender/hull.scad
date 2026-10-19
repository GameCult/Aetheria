// Zhestokost ammunition tender "Podvoz". An agent-built hull source: composes the Zhestokost language only.
// part = lang | brief | anchors | primary | secondary | accent | recess | emit:<id> | rad:<id>
// Frame: Ship Root, metres, nose -Y, starboard -X, up +Z. Anchor yaw: 0 nose, 90 port, 180 aft, 270 starboard.
include <zhestokost.scad>
part = "primary";

HULL_FACTION = "zhestokost";
HULL_DONOR = "";
HULL_ROLE = "tender";
HULL_LENGTH = [18, 28];
HULL_TRIANGLES = [300, 6000];
HULL_READS = ["ammunition magazines carried where a rearming ship can see and reach them",
              "a handling boom that brings magazines forward", "towing points",
              "one defensive ballistic mount, not a warship battery", "low, broad, heavy, slow"];
if (part == "brief") echo(brief = [HULL_FACTION, HULL_DONOR, HULL_ROLE, HULL_LENGTH, HULL_TRIANGLES, HULL_READS]);

L = 15; W = 10; H = 4.2; Y0 = 3;            // main mass centred at y=+3 (nose is -Y)
module pockets() { for (s=[-1,1], y=[-1, 4]) translate([s*W/2, Y0+y, 1.3]) cube([1.2, 2.6, 1.4], center=true); }
module body() { difference() { translate([0, Y0, 0]) zh_stepped(L, W, H, 3, 0.10); pockets(); } }
module plates() { for (s=[-1,1], i=[0:3]) translate([s*(W/2+0.05), Y0-5.4+i*3.6, 0.6]) rotate([0,0,90]) rotate([90,0,0]) zh_plate(3.2, 1.0, 0.3); }
module band() { for (s=[-1,1], i=[0:3]) translate([s*(W/2+0.22), Y0-5.4+i*3.6, 0.6]) cube([0.04, 3.2, 0.3], center=true); }
module booms() { for (s=[-1,1]) translate([s*2.6, -5.0, H-0.3]) { zh_boom(13); translate([0, 5.6, -0.9]) cbox([1.4, 1.4, 1.6], 0.12); } }
module magazines() { for (s=[-1,1], i=[0:3]) translate([s*3.75, -10.4+i*1.9, H-1.7]) zh_cassette(1.2, 1.5, 2.0); }
module arms() { for (s=[-1,1], i=[0:3]) translate([s*3.2, -10.4+i*1.9, H-0.45]) cube([1.6, 0.3, 0.3], center=true); }
module bridge() { translate([0, Y0+5.6, H]) { cbox([4.4, 2.6, 1.4], ZH_CH); translate([0, 0.3, 1.2]) cbox([2.8, 1.8, 1.0], ZH_CH);
    translate([0, -1.32, 0.35]) cube([3.2, 0.06, 0.3], center=true); } }
module lugs() { for (s=[-1,1], y=[Y0-L/2+0.8, Y0+L/2-1.0]) translate([s*(W/2-1.6), y, H/3+0.2]) zh_tow_lug(); }
module drive() { translate([0, Y0+L/2+0.6, 1.3]) zh_drive_block(6.0, 2.2, 1.6, 2, 1, 1.3); }
module gun() { translate([0, Y0+3.2, H]) zh_gun(2, 0.2, 2.6, 1.6); }
module rad(s) { translate([s*(W/2+1.35), Y0+5.2, 2.0]) cube([2.5, 2.2, 0.12], center=true); }
module recess_fill() { for (s=[-1,1], y=[-1, 4]) translate([s*(W/2-0.55), Y0+y, 1.3]) cube([0.1, 2.5, 1.3], center=true); }

if (part == "primary") { body(); drive(); }
if (part == "secondary") { plates(); booms(); arms(); lugs(); gun(); bridge(); }
if (part == "accent") { magazines(); band(); }
if (part == "recess") recess_fill();
if (part == "rad:Ra.L") rad(1);
if (part == "rad:Ra.R") rad(-1);
if (part == "emit:Th.L") translate([ 1.5, Y0+L/2+1.05, 1.3]) cube([1.2, 0.05, 1.2], center=true);
if (part == "emit:Th.R") translate([-1.5, Y0+L/2+1.05, 1.3]) cube([1.2, 0.05, 1.2], center=true);
// Ids are HullData mount ids: the gameplay layout addresses these by name only.
if (part == "anchors") {
  echo(anchor=["Gun.D", "weapon-mount", [0, Y0+3.2, H+0.6], 0, ""]);
  echo(anchor=["Gun.D.muzzle", "weapon-muzzle", [0, Y0+3.2-0.48-2.6-0.3, H+1.15], 0, "Gun.D"]);
  echo(anchor=["Th.L", "thruster-emitter", [ 1.5, Y0+L/2+1.05, 1.3], 180, ""]);
  echo(anchor=["Th.R", "thruster-emitter", [-1.5, Y0+L/2+1.05, 1.3], 180, ""]);
  echo(anchor=["Ra.L", "radiator-mesh", [ W/2+1.35, Y0+5.2, 2.0], 0, ""]);
  echo(anchor=["Ra.R", "radiator-mesh", [-W/2-1.35, Y0+5.2, 2.0], 0, ""]);
}
