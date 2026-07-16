# Original Game Rendering And View Specification

Baselines: RTS `d12d7c5c^`; ARPG `origin/master` at `ab2c2944`.

## Projection Contexts

Aetheria does not have one universal camera. It has distinct products:

- pilot gameplay world through main plus follow/dock virtual cameras;
- auxiliary fog and field cameras writing named textures;
- tactical minimap cameras and map-scale icons;
- full zone-map projection;
- sector/galaxy topology projection;
- world effects, transparent passes, trails, and overlays.

`ZoneRenderer` currently coordinates many of these, but that Unity class is a
reference composition, not the target owner.

## Entity Presentation Graph

`EntityInstance` is a composite presentation root containing:

- hull renderers and collision presentation;
- tactical map icon;
- influence and ping subviews;
- shield subview;
- equipment, radiator, thruster, weapon, and articulation hardpoints;
- fade-capable and persistent material partitions;
- visibility/fade lifecycle.

`ShipInstance` adds thruster particles, Aether-drive effects, tractor beam, and
ship-specific composition. Weapon presentation adds muzzle, charge, projectile,
beam, impact, explosion, particle, and trail subviews.

One entity/one opaque prefab is insufficient as a portable contract. Eve needs
a typed entity-presentation graph with named subviews, attachment sockets,
activation predicates, render channels, and effect bindings.

## Semantic Render Channels

Required Aetheria channels include:

```text
world.geometry
world.celestial
world.transparent
world.shields
world.projectiles
world.trails
world.effects
field.fog-density
field.fog-displacement
field.gravity
field.influence
map.zone.objects
map.zone.gravity
map.minimap.objects
map.minimap.fields
map.sector.topology
overlay.targeting
```

Channel identity is semantic. Unity layer numbers are runtime-variant metadata.
The pilot view excludes minimap channels; map cameras include their map channels
without requiring pilot geometry. A channel may render to display or a named
texture consumed by another material/channel.

Each channel requires space, camera role, visibility set, ordering, output
target, update policy, clear policy, composition mode, and consumer bindings.

## Fields

Original products include fog density/tint, displacement and patches, sector
boundary fog, gravity wells/waves/terrain/depth, influence, slime/gravity
textures, and map/minimap overlays. Eve Fields is the portable numeric base,
but parity requires named outputs, per-product resolution, blend operation,
viewport, cadence, material consumer, and camera visibility.

## Asset And Material Contract

Provider assets own authored transforms, bounds, meshes, materials, sprites,
textures, sounds, and effect content. Semantic radius never rescales authored
art. Runtime variants map portable roles to native resources.

Required asset kinds include entity presentation, subview, mesh, material
profile, effect, projectile presentation, trail profile, field material, map
symbol, and audio. Entity presentations declare root asset, subviews,
attachments, material slots, visibility tags, map representations, and effect
bindings.

Material profiles describe intent such as opaque PBR, transparent, additive,
field, shield, and celestial. They expose typed parameters and texture slots;
they do not publish Unity shader names as portable authority.

The current native environment seed packages the pre-generated Radiance image
at `Assets/Textures/studio2.hdr` strictly as the reflection texture for
stellar-tinted ambient lighting. Gravity-fog raymarching fills the visible
frame; the studio image is not a skybox or an ambient-color authority. The HDR
source is 4096 by 2048 pixels with
SHA-256 `A2886F1024F67DCB082DA3C97741B4B0824A2E784A697807DBE5DCEB597F85E0`;
it entered the reference project in commit `b53e4aec` on 2021-02-26. The
repository contains no external license attribution for that file, so its
redistribution clearance remains unverified. Treat it as migration material,
not a provenance claim.

Texture generation is an offline authoring concern. Aetheria consumes packaged,
pre-generated textures today; the intended authoring path moves baking into
Blender. Neither the daemon nor generic Eve runtimes depend on Substance
archives or a Substance importer.

## Effects

Effects are semantic burst, loop, beam, trail, impact, shield-hit, destruction,
and screen-feedback lifecycles. Contracts declare source event, spawn space,
attachment socket, channel, lifetime, pooling class, parameters, and start/
update/stop bindings. Effect realization cannot mutate gameplay state.

The current tractor lowering uses provider-owned, pre-generated particle
textures and materials. `beam.presentation` carries source identity, provider
asset role, daemon power, activation threshold, authored radius/distance,
render channel, and an advertised activation action. EveUnity attaches and
modulates that prefab; it does not raycast, apply force, infer contact, or move
cargo. The provider build removes the fossil's embedded tractor object from
ship presentation prefabs, so the standalone `beam.presentation` effect is the
only live writer of tractor visuals. Ymir contact facts remain the only
collection gate.

Projectile simulation and projectile presentation are separate. The daemon and
Ymir own identity/pose/contact; presentation owns visual asset, trail, impact,
and visibility tags.

## Maps

Minimap, zone map, and sector map are separate projections sharing stable world
identities. They are not merely alternate cameras over the pilot document.

- minimap: local contacts, icons, influence, gravity, independent zoom;
- zone map: viewport objects, fields, selection, and navigation;
- sector map: topology, links, discovery, factions, entrance/exit/boss/current.

## Runtime Ownership

Target ownership: EveUnity owns generic native lowering primitives once they
exist. Current implementation covers basic playable-world entities, provider
assets, input, camera following, command transport, receipts, and refresh.
Semantic render channels, entity presentation graphs, map products, material
profiles, effect lifecycles, pooling, and parity diagnostics remain incomplete
or unimplemented.

Aetheria publishes semantic state and provider asset variants. Eve owns
portable contracts and plugin ABIs. Native layer allocation, masks, render
textures, prefab assembly, materials, GPU buffers, particles, trails, pooling,
culling, and diagnostics are derived runtime state, never portable authority.

## Visual Parity Proofs

Capture and compare pilot world, shield hit, each projectile/beam/trail family,
fog/fields, minimap, zone map, and sector map. Diagnostics must identify source
projection, channel, camera, target texture, visibility set, active asset
variant, and provider event/state version.
