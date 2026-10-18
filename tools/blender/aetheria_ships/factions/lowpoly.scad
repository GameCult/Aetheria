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
