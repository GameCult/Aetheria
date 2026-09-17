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

## Consequences already visible

- `FactionProductData` currently stores a manufacturer, a brand name and flavour
  text, and a per-role quality distribution, and `CraftedItemInstance.Product`
  points at one. Under these rulings the brand an item shows is chosen from its
  provenance, so an instance does not store which product it is.
- CultCache Studio's grouping of `GearData` by manufacturer
  (`CultLib\docs\studio-grouping-cut.md`, Cut 4) is no longer the target.

## Not in scope

- A Njordr daemon, CultMesh, or any service integration. Aetheria stays
  legacy-first; the lot model is local typed state in CultCache stores.
