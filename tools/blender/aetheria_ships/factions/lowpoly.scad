// Shared low-poly primitives (any faction). Frame: Blender Ship Root, nose -Y, starboard -X, up +Z.
// cbox: a box with one chamfer segment on every edge (the operator's one-segment-bevel bar, as geometry).
module cbox(s, c=0.2, center=true) {
  t = center ? [0,0,0] : s/2;
  translate(t) hull() {
    cube([s.x, s.y-2*c, s.z-2*c], center=true);
    cube([s.x-2*c, s.y, s.z-2*c], center=true);
    cube([s.x-2*c, s.y-2*c, s.z], center=true);
  }
}
// tube along +Y (nose is -Y, so a forward barrel uses rotate 180 or negative length)
module tube(r, l, fn=8) { rotate([-90,0,0]) cylinder(r=r, h=l, $fn=fn); }
// a straight strut between two points
module strut(a, b, r=0.08, fn=6) { hull() { translate(a) sphere(r, $fn=fn); translate(b) sphere(r, $fn=fn); } }

// ==== Frames and schematic lines =====================================================================================
// A kit part that draws schematic lines is placed with lp_at, never with raw translate/rotate/mirror: lp_at moves the
// geometry (multmatrix) and tracks the same frame in $lp_m, so lp_line, called anywhere beneath it, emits its points in
// the Ship Root frame. Placement has this one owner. A line is echoed (part "lines") only when the frame's local +Z
// points up, so fittings on the belly, the walls and the belts, which a top view cannot see, emit nothing.
$lp_m = [[1,0,0,0], [0,1,0,0], [0,0,1,0], [0,0,0,1]];
function lp_T(t) = [[1,0,0,t[0]], [0,1,0,t[1]], [0,0,1,t[2]], [0,0,0,1]];
function lp_R(r) =                                   // Rz * Ry * Rx, as rotate() composes
  [[cos(r[2]), -sin(r[2]), 0, 0], [sin(r[2]), cos(r[2]), 0, 0], [0,0,1,0], [0,0,0,1]]
  * [[cos(r[1]), 0, sin(r[1]), 0], [0,1,0,0], [-sin(r[1]), 0, cos(r[1]), 0], [0,0,0,1]]
  * [[1,0,0,0], [0, cos(r[0]), -sin(r[0]), 0], [0, sin(r[0]), cos(r[0]), 0], [0,0,0,1]];
lp_MX = [[-1,0,0,0], [0,1,0,0], [0,0,1,0], [0,0,0,1]];   // mirror across x = 0
function lp_ap(m, p) = let(v = m * [p[0], p[1], p[2], 1]) [v[0], v[1], v[2]];
// Mirror (when mx), then translate by t, then rotate by r (as nested mirror, translate, rotate would).
module lp_at(t=[0,0,0], r=[0,0,0], mx=false) {
  M = (mx ? lp_MX : [[1,0,0,0], [0,1,0,0], [0,0,1,0], [0,0,0,1]]) * lp_T(t) * lp_R(r);
  $lp_m = $lp_m * M;
  multmatrix(M) children();
}
// One schematic line: layer (outline, step, seam or fitting), points in the local frame, cyclic when it closes.
// Part "marks" draws a LP_MARK cube at the line's first point through the CTM, the frame the geometry is drawn in, where
// "lines" echoes the same point through $lp_m; hull_source compares the two, so a part placed by anything but lp_at
// (a raw translate, rotate or mirror) shows as a line away from its geometry.
LP_MARK = 0.002;
module lp_line(layer, pts, cyclic=false) {
  if (!is_undef(part) && ($lp_m * [0, 0, 1, 0])[2] > 0.5) {
    if (part == "lines") echo(line = [layer, cyclic, [for (p = pts) lp_ap($lp_m, p)]]);
    if (part == "marks") translate(pts[0]) cube(LP_MARK, center=true);
  }
}
