// Zhestokost form language: the one owner of this faction's shape kit, proportions, symmetry rule and palette.
// Canon. Operator ruling zhestokost-language-lore-text: Zhestokost is industrial and brutalist, "the dream of the
// eternal Soviet, a society that has achieved total and unassailable permaculture via a carefully organized and
// brutally practical (self-policing) state". Form follows the lore page ("Faction Flavor and Visual Identity" >
// Zhestokost): low broad stepped masses, deep seams, charcoal, iron grey, dull oxide red. The operator's faction sheet
// (Asset Sources/Zhestokost.png) lends the handling boom with hanging magazines as vocabulary, not its palette.
// A hull composes these modules; nothing here places itself.
include <lowpoly.scad>
ZH_CH = 0.22;                      // chamfer: heavy, square, one segment
// A stepped mass: tiers shrink upward and pull in from both ends. "Low, broad ... stepped rectangular masses."
module zh_stepped(L, W, H, tiers=3, pull=0.12) {
  for (i=[0:tiers-1]) let(k=i/tiers)
    translate([0, 0, H*k + H/tiers/2]) cbox([W*(1-pull*i*1.6), L*(1-pull*i), H/tiers + 0.02], ZH_CH);
}
// Armour plate standing proud of a face, leaving a deep seam (the seam is the gap to the next plate).
module zh_plate(w, l, t=0.3) { cbox([w, l, t], 0.08); }
// Recessed machinery: a dark box set into a mass (subtract the pocket in the hull, then place this inside).
module zh_recess(w, l, d) { cube([w, l, d], center=true); }
// Ammunition magazine: square cassette with ribbed loading face and a hanger lug on top.
module zh_cassette(w=1.2, l=1.6, h=2.0) {
  cbox([w, l, h], 0.12);
  for (z=[-h*0.3, 0, h*0.3]) translate([0, -l/2-0.04, z]) cube([w*0.8, 0.12, 0.18], center=true);
  translate([0, 0, h/2+0.2]) cube([0.3, 0.6, 0.4], center=true);
}
// Handling boom: square box beam with cross-members; magazines hang from it.
module zh_boom(len, w=0.9, h=0.7) {
  cbox([w, len, h], 0.1);
  for (y=[-len/2+0.6:1.6:len/2-0.4]) translate([0, y, -h/2-0.1]) cube([w+0.4, 0.25, 0.25], center=true);
}
// Towing point: a thick eye on a block.
module zh_tow_lug() { cbox([0.6, 0.6, 0.4], 0.08); translate([0,0,0.45]) rotate([0,90,0]) difference() { cylinder(r=0.38, h=0.25, center=true, $fn=8); cylinder(r=0.18, h=1, center=true, $fn=8); } }
// Ballistic mount: square base, slab mantlet, N thick barrels with a box muzzle brake. Barrels point -Y (nose).
module zh_gun(n=2, cal=0.22, len=3.0, base=1.6) {
  cbox([base, base, 0.5], 0.12);
  translate([0, 0, 0.55]) cbox([base*0.85, base*0.7, 0.7], 0.12);
  for (i=[0:n-1]) translate([(i-(n-1)/2)*cal*3, -base*0.3, 0.6]) {
    rotate([0,0,180]) tube(cal, len, 8);
    translate([0, -len, 0]) cbox([cal*3, cal*3.5, cal*3], 0.04);
  }
}
// Drive block: a flat rear face with square nozzles in a grid (recessed: emitters sit at the nozzle mouths).
module zh_drive_block(w, h, d, nx=2, nz=1, nozzle=1.0) {
  difference() {
    cbox([w, d, h], ZH_CH);
    for (i=[0:nx-1], j=[0:nz-1]) translate([(i-(nx-1)/2)*(w/nx), d/2, (j-(nz-1)/2)*(h/nz)]) cube([nozzle, 0.8, nozzle], center=true);
  }
}


// Palette zones. Value contrast carries the read at 48 px on dark space: the body is iron grey, the plates and seams
// are charcoal, the oxide red is the only hue. Zones become livery mask channels.
ZH_ZONES = [["primary", [0.42, 0.43, 0.45]],     // iron grey: the stepped mass
            ["secondary", [0.20, 0.20, 0.22]],   // charcoal: armour plates, booms, guns, command block
            ["accent", [0.55, 0.22, 0.14]],      // dull oxide red: magazines, markings
            ["recess", [0.08, 0.08, 0.09]]];     // recessed machinery
ZH_GLOW = [1.0, 0.62, 0.25];
ZH_RADIATOR = [0.50, 0.51, 0.52];
// [faction, zones, body zone, glow, radiator, [height/width range, width/length range], symmetry rule]
ZH_LANG = ["zhestokost", ZH_ZONES, "primary", ZH_GLOW, ZH_RADIATOR, [[0.25, 0.6], [0.35, 0.75]], ["bilateral", 0.97]];
if (part == "lang") echo(lang = ZH_LANG);
