// Pirate Coalition form language: the one owner of this faction's additions kit, symmetry rule and palette. Source: Faction Flavor > Pirate Coalition > Visual Identity: "An identifiable
// donor hull with asymmetrical additions, cut-down modules, adapted hardpoints, exposed cable routes, and bolted
// salvage plates. Each major addition has a plausible mounting and supply path."
// A Pirate hull = one donor-language body (another faction's language, named in the hull's brief and judged by its
// proportions) + these additions. This language draws no body and has no proportions of its own.
include <lowpoly.scad>
// Bolted salvage plate: a slightly tilted slab with proud bolt heads.
module pi_plate(w, l, t=0.12, tilt=4) {
  rotate([tilt, -tilt*0.6, 0]) { cube([w, l, t], center=true);
    for (x=[-w/2+0.15, w/2-0.15], y=[-l/2+0.15, l/2-0.15]) translate([x, y, t/2]) cylinder(r=0.07, h=0.06, $fn=6); }
}
// Exposed cable run: a chain of short segments sagging between clamp points.
module pi_cable(pts, r=0.06) { for (i=[0:len(pts)-2]) strut(pts[i], pts[i+1], r, 5); }
// Weld bead / adapter collar where an addition meets the donor.
module pi_collar(r, h=0.2) { cylinder(r=r, h=h, center=true, $fn=8); }
// Adapted hardpoint: a crude saddle (two plates + braces) that carries something the donor never had.
module pi_saddle(w, l, h) {
  translate([0,0,h/2]) cube([w, l, 0.15], center=true);
  for (x=[-w/2+0.1, w/2-0.1]) translate([x, 0, h/4]) cube([0.12, l*0.8, h/2], center=true);
  strut([-w/2, -l/2, 0], [w/2, l/2, h], 0.05); strut([w/2, -l/2, 0], [-w/2, l/2, h], 0.05);
}
// Replacement drive: an oversize bell on a braced cradle, not matching the donor's flush engines.
module pi_drive(r, l) {
  rotate([-90,0,0]) { cylinder(r1=r*0.55, r2=r, h=l*0.45, $fn=8); translate([0,0,-l*0.55]) cylinder(r=r*0.6, h=l*0.55, $fn=8); }
}
// Oversized industrial gun (a mass driver pulled off a mining rig): long box receiver, single heavy barrel.
module pi_big_gun(len, cal=0.2) {
  cbox([0.9, len*0.35, 0.7], 0.08);
  translate([0, -len*0.17, 0.05]) rotate([0,0,180]) tube(cal, len*0.65, 8);
  translate([0, -len*0.82, 0.05]) rotate([0,0,180]) tube(cal*1.6, 0.35, 8);
}
// Bolted-on sensor pod: one sleek piece from a better ship, on a strut.
module pi_sensor_pod(l=1.4, r=0.3) { scale([1, l/(2*r), 1]) sphere(r, $fn=10); }

PI_ZONES = [["primer", [0.48, 0.27, 0.22]],      // primer-red salvage plates
            ["weld", [0.70, 0.68, 0.62]],        // welded additions: guns, drives, cables
            ["accent", [0.10, 0.62, 0.60]],      // the crew's own colour
            ["recess", [0.08, 0.08, 0.09]]];
PI_GLOW = [0.55, 0.85, 1.0];
PI_RADIATOR = [0.70, 0.68, 0.62];
// No body zone and no proportions: the donor's. The additions must make the top view visibly lopsided.
// maker: the Pirates Faction record key in GameData/Aetheria.cc
PI_LANG = ["pirate", PI_ZONES, "", PI_GLOW, PI_RADIATOR, [], ["asymmetric", 0.9], "1d8a168def994a76a1ce7cb18f59c038"];
if (part == "lang") echo(lang = PI_LANG);
