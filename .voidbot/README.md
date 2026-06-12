# Aetheria Repo Persona

This directory contains Aetheria's repo-local VoidBot Persona surface.

The canonical continuity store is `state/aetheria.cc`, a CultCache `.cc` file
using VoidBot typed self-state documents. Do not edit the `.cc` file by hand.
Memory writes should cross a typed operation boundary through
`E:\Projects\VoidBot\scripts\void-self-state.mjs apply-operation` or the
equivalent VoidBot service API.

Current state:

- `state/aetheria.cc` is the canonical Persona state.
- `voice/identity.json` identifies the repo Face projection.
- `birth/` and `logs/` hold initialization evidence.
- Aetheria is not yet registered as a VoidBot Discord identity, so automatic
  Qdrant indexing depends on a later registry entry.

The first Epiphany birth pass completed repo scouting, then failed in the birth
runner because a built helper executable was missing. Treat that as unfinished
initialization evidence, not as a completed Persona birth.
