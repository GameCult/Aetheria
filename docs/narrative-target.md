# Narrative: target

Date: 2026-09-25

Status: target shape, not mapped into cuts. It is the brief for a future narrative campaign, written now so that
the invariants below are on record before any narrative code or content is written against them.

## Invariants

1. **Every quest names its title and its author, on screen, always.** Operator, 2026-09-25: "quests are
   self-contained and whenever you're playing some content from a quest (or inspecting a quest item) you can see
   the quest title and author always on screen in a sticky header. Author field says whose words it is. For a lot
   of the content it's gonna be Opus 5.5, obviously."
   - **Author credits everyone whose authorship is in the quest**: whose words they are, and whose authorial intent
     shaped them. Operator, same day: "or maybe Metacrat + Opus 5.5, there's certainly some authorial intent to be
     attributed." So a quest Metacrat directed and Opus 5.5 wrote is credited "Metacrat + Opus 5.5"; a human
     writer's own quest names that human. Placing, formatting or copy-editing someone else's quest is not
     authorship and adds no name. The field is a list of credited authors, never a single owner slot that forces
     one name to stand for joint work.
   - **The header is sticky**: it shows for the whole time any content from that quest is on screen — dock text a
     quest injects into a station's story, quest dialogue, and inspecting a quest item — not only on a quest log
     page.
   - **Quests are self-contained**: one quest, one author, one title. A quest's text does not live inside another
     author's file, so attribution is a property of the quest, not of a paragraph.
   - This is the engine-level form of the authorship boundary the project already keeps by hand
     (`docs/tutorial-script-sketch.md` opens with an authorship block): **Emily Harvey's writing is attributed to
     her and never presented as anyone else's, and AI-extended writing is visibly labelled and never presented as
     hers.** Her permission to extend is not involvement or endorsement; a quest that extends her premise names its
     actual author, not her.
2. **A place carries its own flavour; progression lives in quests layered over places.** Operator, on Star Sonata:
   "every stage of introducing you to the wider galaxy is a deeper exploration of the game's mechanics, and loaded
   with flavor despite it being cheap text that you read when you dock somewhere."

## Shape (the operator's two tiers)

- **The curated opening.** A hand-built walk-through of the basic actions, "the very first few levels you're
  dropped into". This is where Emily's tutorial premise lives (`docs/tutorial-script-sketch.md`, and her own
  script `AetheriaTexts.ctd`, which is not edited).
- **The widening galaxy.** Each stage teaches one system more deeply (mining, trade, ranged combat against turrets,
  buying a ship and moving gear across), through cheap dock text at story stations and quests that branch into
  those stations. Star Sonata's tutorial is the reference skeleton (community guides: dock and take a mission, kill
  a pest, scoop the loot, first warp gate, gather a commodity, a tougher enemy, buy a ship, ranged play against
  stationary targets, the missions/trade/mining triad, graduation on an economic threshold).

## Machinery that already exists

- **Ink.** `ServerShared/Narrative/StoryProcessor.cs` compiles `GameData/Narrative/Locations/*.ink` (a story per
  place) and `GameData/Narrative/Quests/*.ink`. File-level global tags already carry metadata (`#planet:`,
  `#constraint:`, `#trade:` in `ReconStationAlpha.ink`), so `#title:` and `#author:` fit the existing convention.
- **Places.** Story stations are placed by `ZoneGenerator.cs:~270-288` (`station.Story = i`), carrying a `Location`
  from the galaxy zone (`EntitySerializer.cs:77`).
- **Dock text.** Docking at an entity with a story shows the **Local** tab (`UI/Menu/MenuPanel.cs:52`);
  `UI/Menu/LocalMenu.cs` runs the location's story and injects each quest's branch at that dock (`:78-87`).
- **The gap.** The live catalog has no station hull, so no story station is generated and the Local tab never
  appears. Locomotion Cut 1's content restore is recovering a station hull from the legacy record.

## Open

- The header needs a quest identity at every surface that can show quest content: the Ink story a line came from,
  injected branches in `LocalMenu`, and quest items. Where that identity lives (Ink global tags read at compile
  time, a typed quest record, or both) is for the campaign's map.
- **Existing narrative files are Emily Harvey's.** Operator, 2026-09-25: "Pretty sure those words are all
  Emily's. Attribution goes to Emily Harvey." `GameData/Narrative/*.ink` (`HeroOrZero`, `ReconStationAlpha`,
  `RSA/TheActualFactualFactory`, `RSA/QuestTexts`, the Terminus files, `demooutline`) are credited to her when the
  author field lands, and fall under the same boundary as `AetheriaTexts.ctd`: not edited in place, quoted and
  attributed, and any extension is a separate, labelled quest crediting its actual authors. Git commit accounts
  are not attribution and are not a source for it.
