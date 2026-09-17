# Item Provenance Target

Date: 2026-09-17

Status: target, rulings only. The substrate map, the identity/lifecycle/authority
table and the cut map are not written yet. This document owns the ends; a cut map
will own the means.

## Why

Aetheria's crafted items should already have the shape of the future economy.
Njordr (`F:\Projects\Njordr\docs\architecture\njordr.md`) tracks production and
distribution at the level of individual lots, and a lot's provenance is the DAG of
recipe runs and input lots that made it. Aetheria adopts that shape now, with its
current mocked quality, so later supply-chain stats plug into provenance that
already exists.

## Rulings (operator, 2026-09-17)

- **The manufacturer does not belong to a design.** `ItemData.Manufacturer` leaves
  the catalog design.
- **A lot's provenance says who built it, where, and from what.** In Aetheria that
  maps to:
  - a specific **faction**;
  - a specific **station**, meaning the stats of the factory item itself are
    known, and that factory maps to an item instance in that station's inventory;
  - a set of **item inputs**, each carrying its own provenance.
- **Branding is derived, not stored.** Provenance is enough to reconstruct the
  branding of an item, with market segmentation appropriate to the production
  quality achievable at that station. Branding is an Aetheria-layer projection;
  Njordr's lot does not grow a brand field.
- **Provenance determines stats.** Today that means the mocked quality; later it
  incorporates the supply chain.

Operator's words, for the parts a paraphrase could lose: "The lot provenance
should say who built it and where and from what ingredients, and in Aetheria that
maps to a specific faction, a specific station (meaning we have the stats for the
factory item itself as well, which should even map to an item instance in that
station's inventory in Aetheria), and a set of item inputs, carrying their own
provenance. That is enough to reconstruct the branding for that item, with market
segmentation appropriate to the production quality achievable at that station."

- **Terminus generates backwards (operator, 2026-09-17).** Terminus starts from the
  output products that need to exist and synthesizes a fake economy to
  materialize them: the product is chosen first, then a plausible faction,
  station, factory and input lots that would have produced it. The forward
  supply chain is Njordr's future direction, not Terminus scope. Operator's words:
  "Right now, for Terminus, we want to go in the opposite direction: start with
  the output products, and generate a fake economy to materialize those goods."

- **Synthesis recurses to mining, includes consumer goods, and feeds routes**
  **(operator, 2026-09-17).** Backward generation runs all the way down to
  extraction. Consumer goods are part of it, and routes move them between places,
  which is meant to give Aetheria piracy gameplay. The aim is a convincing economy
  without simulating one. Njordr's interspersed profile (shipment materialization
  and interception) is the shape precedent, adopted as local typed state. Operator's
  words: "right now we have no rules whatsoever for generating what looks like
  economic activity. If we recurse the backwards generation all the way down to the
  level of mining, we can get a convincing enough economy without actually
  simulating one. Especially if we're accounting for consumer goods. Just add some
  routes to move consumer goods around, and we can get a lot of bang for our buck.
  Some decent piracy gameplay, even, which Aetheria feels empty without."

- **Scope: schema now, generation later (operator, 2026-09-17).** Backward
  generation, routes and piracy come after Terminus ships
  (`docs/three-gates-scope.md` owns that). The schema cut lands now: "I want the
  schema cut now so that the shape of the data reflects the shape of the game we
  want to build."

- **Terminus mint sites write a placeholder origin (operator, 2026-09-17; F0 (a)).**
  Generation starts from branded items with specific role spreads and synthesizes
  whatever provenance is needed to hit those properties. This pass does not
  generate the crafting pyramid, so there is nothing honest to name for where
  and from what; the origin says only which faction made it. Operator's words:
  "since we are starting from branded items with specific role spreads, we
  synthesize whatever provenance is necessary to hit those properties. Since
  we're not going to bother generating every level of the crafting pyramid for
  this pass, that also means there's no honest thing to put there, so A, I guess?"
- **Factories are built in place from raw materials (operator, 2026-09-17; F2,
  next scope).** A factory's provenance terminates in extracted inputs at its own
  station, not in another factory. Switching a factory to a different design
  costs retooling, and so does a tech upgrade on a sub-assembly: a different
  item filling the same role in an ingredient slot counts as a different design.
  Operator's words: "these are things that should be buildable in-place out of
  raw materials, with some cost for retooling every time you want to switch
  designs (including tech upgrades on sub-assemblies, meaning a different item
  with the same role filling an ingredient slot)".

## Consequences already visible

- `FactionProductData` currently stores a manufacturer, a brand name and flavour
  text, and a per-role quality distribution, and `CraftedItemInstance.Product`
  points at one. Under these rulings the brand an item shows is chosen from its
  provenance, so an instance does not store which product it is.
- CultCache Studio's grouping of `GearData` by manufacturer
  (`CultLib\docs\studio-grouping-cut.md`, Cut 4) is no longer the target.

## Design text and product text (operator, 2026-09-17)

- A design has no manufacturer field. Operator: "a design shouldn't have a manufacturer
  field at all, and a product should show its manufacturer flavor text in addition to the
  Design, which would be a dry description of functionality".
- The item panel shows both: the design's description (what the item does) and, when the
  item's provenance resolves a product, that product's flavour text. `PropertiesPanel`
  already has this shape.
- The catalog content does not. Products currently repeat their design's name and
  description (seen on Arctica, 2026-09-17), and design descriptions are written as
  flavour. Fixing it is a content pass: dry functional text on designs, manufacturer
  voice on products, agent-drafted and operator-reviewed, written through the catalog's
  own types.
- **Names follow the same split.** A product's name is the manufacturer's brand name; a
  design's name is a dry functional one. Operator, on Arctica: "Arctica is Alakrita
  branding, by the name I presume they're optimizing for heat efficiency with this release,
  but it's just another laser, eh?" So the panel titles an item with its product name when
  provenance resolves one and shows the design name as what the item is. Catalog designs
  currently carry brand names (the design itself is named Arctica); the content pass renames
  them.
- **The content pass also consolidates designs.** Operator: "content pass should also
  consolidate different items into one design if the behavior matches and its Role axes can
  justifiably express both". Two catalog items whose behaviours match become one design with
  roles, and their differences move into per-role quality on each product's lots.
  - Roles are authored before merging, since they are what makes a merge justifiable.
  - A merge repoints every reference to the retired design (products, loadouts, ammo,
    blueprints, run and player saves) in the same commit, and the census proves none dangle.
  - A difference roles cannot express (different behaviour, a changed member such as a
    hardpoint layout) keeps a separate design, or waits for document variants
    (`CultLib/docs/document-variants-target.md`).

## Not in scope

- A Njordr daemon, CultMesh, or any service integration. Aetheria stays
  legacy-first; the lot model is local typed state in CultCache stores.
