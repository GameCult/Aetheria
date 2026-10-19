// Rossum & Douglas form language: "the flying wedge". Source: Faction Flavor > Rossum & Douglas > Visual Identity.
// The one owner of this faction's shape kit, proportions, symmetry rule and palette. It also serves as a donor language:
// a Pirate hull builds on a stolen R&D wedge (Bench 3 "Jumble Sale").
include <lowpoly.scad>
// Broad shallow trapezoid narrowing to a blunt nose (-Y), wide rear face (+Y), flat dorsal plane,
// bevelled perimeter, thicker centre section. One idea, no flourishes.
module rd_wedge(L, w_rear, w_nose, h_edge, h_centre) {
  hull() {
    translate([0,  L/2-0.3, 0]) cube([w_rear, 0.6, h_edge], center=true);          // rear face
    translate([0, -L/2+0.3, 0]) cube([w_nose, 0.6, h_edge*0.8], center=true);      // blunt nose
    translate([0,  L*0.05, 0]) cube([w_rear*0.45, L*0.7, h_centre], center=true);    // thick centre
  }
}
// Flush rear engine bank: shallow rectangular recesses across the rear face.
module rd_engine_bank(w, h, n=3) {
  for (i=[0:n-1]) translate([(i-(n-1)/2)*(w/n), 0, 0]) cube([w/n*0.7, 0.5, h], center=true);
}
// Recessed window strip and identical hatches: barely interrupt the planes.
module rd_window(w=1.2) { cube([w, 0.35, 0.18], center=true); }
module rd_hatch() { cube([0.9, 1.1, 0.06], center=true); }


RD_ZONES = [["primary", [0.72, 0.74, 0.76]],     // the wedge
            ["secondary", [0.40, 0.47, 0.56]],   // hatches, panels
            ["accent", [0.90, 0.89, 0.84]],      // stripes
            ["recess", [0.10, 0.12, 0.18]]];     // windows, engine bank
RD_GLOW = [1.0, 0.75, 0.3];
RD_RADIATOR = [0.40, 0.47, 0.56];
RD_LANG = ["rnd", RD_ZONES, "primary", RD_GLOW, RD_RADIATOR, [[0.12, 0.35], [0.55, 1.1]], ["bilateral", 0.97]];
if (part == "lang") echo(lang = RD_LANG);
