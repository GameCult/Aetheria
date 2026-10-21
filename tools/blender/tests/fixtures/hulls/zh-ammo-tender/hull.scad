// Zhestokost ammunition tender "Oboz" (baggage train): smoke fixture, from the hull bench Oboz A. Built only from the
// Zhestokost kit.
// part = lang | brief | anchors | primary | secondary | accent | recess | marking | emit:<id> | rad:<id>
// Frame: Ship Root, metres, nose -Y, starboard -X, up +Z. Anchor yaw: 0 nose, 90 port, 180 aft, 270 starboard.
include <zhestokost.scad>
part = "primary";

HULL_FACTION = "zhestokost";
HULL_DONOR = "";
HULL_ROLE = "tender";
HULL_LENGTH = [20, 28];
HULL_TRIANGLES = [600, 7000];
HULL_READS = ["the hull is the magazine: three huge stepped magazine blocks in a row on the upper half, oxide hatches with heavy hinge bars",
              "two thick round transfer collars set flush in each flank at hull height, ringed by guide lugs",
              "heavy towing lug jutting from the stern plate; short row of open radiator vanes on the spine behind the cab",
              "low, broad, heavy; narrow cockpit slit in a low armoured block at the bow; four squat drive pods low at the belly corners",
              "one defensive ballistic mount on the last magazine deck, not a warship battery"];
if (part == "brief") echo(brief = [HULL_FACTION, HULL_DONOR, HULL_ROLE, HULL_LENGTH, HULL_TRIANGLES, HULL_READS]);

// Tow block (stern frame: local +Z aft, local +Y down): the kit lug's cast base and oxide eye with no neck, so the eye
// is one heavy block jutting straight off the stern plate, pin hole vertical.
module oz_tow_block(w=2.6, h=1.5, d=1.5) {
  zh_z("secondary") translate([0, 0, 0.25]) cbox([w + 0.8, h + 0.6, 0.5 + ZH_SINK], 0.12);
  zh_z("accent") translate([0, 0, 0.5 + d/2 - 0.05]) difference() {
    cbox([w, h, d], 0.25); rotate([90, 0, 0]) cylinder(r=0.42, h=h*2, center=true, $fn=8); }
}

// ---- masses: one full-width charcoal belt the whole length; three iron magazine blocks seated on it, stepping up ----
// bow to stern across deep seams. The belt carries the collars, pods, cab and tow lug; the blocks carry the hatches.
C = zh_plan(23.6, 12.4, 3.0, z0=-1.8, y0=0.8, bow=0.1, bowW=0.62, bowH=0.75, bt=0.35, bb=0.7);
MY = [-3.9, 2.7, 9.3];                       // magazine stations; 0.6 m seams between them
MH = [3.0, 3.6, 4.2];                        // the stepped magazine train: each block taller than the one ahead
MZ = 1.0;                                    // block base, sunk 0.2 m into the belt deck
B = [for (i = [0:2]) zh_block(MY[i], 6.0, 11.8, MH[i], MZ, MH[i]*0.32, 0.3, 0.5)];
T = [for (b = B) zh_tier(b, 0.4, 0.7, 0.6)];
SEAMS = [(MY[0] + MY[1]) / 2, (MY[1] + MY[2]) / 2];
CAB_Y = -9.5; RAD_Y = -7.9; RAD_W = 3.4; RAD_L = 0.9;
POD = [2.6, 3.4, 1.6]; POD_U = 0.82;
PODS = [MY[0] - 1.4, MY[2] + 1.5];           // fore pair under the first block; aft pair mouths at the stern
COLLAR_R = 1.35;
GUN_Y = 11.0; GUN = [1, 0.2, 2.4, 1.3];      // defensive mount: T[2] deck centreline, aft of its hatches (they end at 10.55)

zh_mass(C, "secondary");
for (b = B) zh_mass(b);
for (t = T) zh_mass(t);

// magazine hatches: two on each tier deck, one on each flank wall; unit serials on the block belts
for (i = [0:2]) {
  for (u = [-0.5, 0.5]) zh_on_top(T[i], MY[i], u) zh_hatch(2.5, 2.5);
  for (s = [-1, 1]) {
    zh_on_wall(B[i], MY[i] + 0.6, s, 0.4) zh_hatch(2.4, 2.4, dogs=false);
    zh_on_belt(B[i], MY[i] - 1.9, s, 0.5) zh_serial(0.5, 3);
  }
}
// seams between the blocks: black floors on the belt deck; a transfer collar in each belt flank under each seam
for (y = SEAMS) {
  zh_on_top(C, y, 0) zh_reveal(11.0, 0.6);
  for (s = [-1, 1]) zh_on_belt(C, y, s, 0.5) rotate([0, 0, 45]) zh_collar(COLLAR_R, 0.6);
}
// bow: low lit armoured cab with its slit, chevrons on the nose deck; spine radiator trough between cab and magazines
zh_on_top(C, CAB_Y, 0) zh_cab(6.4, 2.4, 1.2);
zh_on_top(C, -10.6, 0) zh_stripes(2.6, 0.4, 4);
zh_on_top(C, RAD_Y, 0) zh_rad_trough(RAD_W, RAD_L, 0.45);
// stern: iron armour plate and a heavy tow lug through it
zh_on_stern(C, 0, 0.55) zh_plate(6.4, 2.2, 0.3);
zh_on_stern(C, 0, 0.5, sink=-0.25) oz_tow_block();
// four squat drive pods at the belly corners of the belt
for (y = PODS, u = [-POD_U, POD_U]) zh_on_belly(C, y, u) zh_pod(POD[0], POD[1], POD[2]);
// one defensive ballistic mount on the last magazine deck, between and aft of its hatches, barrels to the nose
zh_on_top(T[2], GUN_Y, 0) zh_gun(GUN[0], GUN[1], GUN[2], GUN[3]);

// ---- anchor meshes ----
if (part == "rad:Ra.L") zh_on_top(C, RAD_Y, 0) zh_rad_vanes(RAD_W, RAD_L, 1, 4, 1.0);
if (part == "rad:Ra.R") zh_on_top(C, RAD_Y, 0) zh_rad_vanes(RAD_W, RAD_L, -1, 4, 1.0);
if (part == "emit:Th.L") zh_on_belly(C, PODS[1],  POD_U) translate(zh_pod_mouth(POD[0], POD[1], POD[2])) zh_emitter(1.1, 0.65);
if (part == "emit:Th.R") zh_on_belly(C, PODS[1], -POD_U) translate(zh_pod_mouth(POD[0], POD[1], POD[2])) zh_emitter(1.1, 0.65);
if (part == "anchors") {
  echo(anchor=["Gun.D", "weapon-mount", zh_top_at(T[2], GUN_Y, 0, [0, 0, 0.33]), 0, ""]);
  echo(anchor=["Gun.D.muzzle", "weapon-muzzle", zh_top_at(T[2], GUN_Y, 0, zh_gun_muzzle(GUN[0], GUN[1], GUN[2], GUN[3])), 0, "Gun.D"]);
  echo(anchor=["Th.L", "thruster-emitter", zh_belly_at(C, PODS[1],  POD_U, zh_pod_mouth(POD[0], POD[1], POD[2])), 180, ""]);
  echo(anchor=["Th.R", "thruster-emitter", zh_belly_at(C, PODS[1], -POD_U, zh_pod_mouth(POD[0], POD[1], POD[2])), 180, ""]);
  echo(anchor=["Ra.L", "radiator-mesh", zh_top_at(C, RAD_Y, 0, zh_rad_centre(RAD_W,  1, 1.0)), 0, ""]);
  echo(anchor=["Ra.R", "radiator-mesh", zh_top_at(C, RAD_Y, 0, zh_rad_centre(RAD_W, -1, 1.0)), 0, ""]);
}
