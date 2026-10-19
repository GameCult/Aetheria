# Prior art for agent-authored stand-in hulls (aetheria-release, 2026-10-19)

Serves: the stand-in hull prompt bench for the demo cast and freelancer factions
(operator 2026-10-19: "Let's see if we can figure out a prompt that gets Opus to
spit out some decent stand-in hulls for the demo cast (including ships from
non-present factions to be piloted by other freelancers)"). Gathered by Eyes
pass `eyes-ag7-llm-cad-prior-art`, session `self-2026-10-19-ag7`. Companion to
`2026-10-18-agent-built-hull-art-prior-art.md` and
`2026-10-18-faction-form-language-prior-art.md`.

Evidence levels: **read** (HTML or abstract page read), **snippet** (search
excerpt only).

## Loops and their measured gains

- **SceneCraft** (ICML 2024, arXiv 2403.01248, read). Removing the render-review
  inner loop dropped constraint score 88.9 to 26.1, the largest loss in its
  ablation. Removing the learned code-skill library dropped it 88.9 to 64.5 and
  CLIP 69.8 to 48.3. Human eval put its aesthetics win rate at 74.5% against
  BlenderGPT's 14.5%. It measures scene layout from given assets, not part form.
- **CADCodeVerify** (ICLR 2025, arXiv 2410.05340, read). Two refinement rounds;
  no gain observed beyond round 2. Point-cloud distance 0.156 to 0.126. Dropping
  the reference views or the few-shot questions made it worse. The VLM's own
  verification answers were correct only 64.6% and 68.2% of the time; human
  gold feedback (0.120) beat VLM feedback (0.137) on a 50-sample subset.
- **BlenderGym** (CVPR 2025, arXiv 2504.01786, read). VLM verifier agreement
  with human pairwise judgments 0.66 (Claude 3.5 Sonnet) against 0.79
  human-to-human; Claude was the only verifier that consistently gave full
  reasoning. Scarce compute favours generation, abundant compute favours
  verification: a scaled small verifier over 32 Claude candidates beat unscaled
  GPT-4o and Claude. Generators hallucinate differences, and edits mismatch the
  diagnosis in scripts over about 80 lines. Metrics miss physical
  implausibility such as floating objects.
- **LL3M** (arXiv 2508.08228, read). Planner, retrieval, coder, critic and
  verifier agents; the critic sees 5 renders, the verifier checks each fix was
  applied, refinement loops until the verifier approves. Blender-API retrieval
  raised complex-operation use 1.20 to 5.86 and cut execution errors 26%.
  Without retrieval the coder sometimes deleted offending lines to silence
  errors. Without shared prior code the coder regenerates a substantially
  different asset. Disconnected handles survive auto-refinement; VLMs struggle
  with spatial artifacts. Agent ablations are qualitative only; no user study.
- **Procedura** (arXiv 2608.26238, read). Named parts with typed mates, per-part
  compile, mate and connectivity gates, and a vision critic that fixes one
  diagnosed issue at a time. Ablation on MechBench-36 (judge score): full 0.828,
  no refine 0.800, no planning 0.791, no 3D feedback 0.819; dropping planning
  caused the biggest aesthetics drop, 0.827 to 0.775. A floater at 70% span was
  1.9% of volume, so a volume check misses it. Case study: 24 to 3 to 2 HIGH
  issues over three critic cycles. CSG suits hard-surface better than organic.
- **P3D-Bench** (arXiv 2606.11152, abstract). Multi-part code generation is much
  harder than single-part: models get identity right, dimensions, part count
  and part geometry wrong.
- **ModelRift Pantheon benchmark** (vendor blog, one run per cell, read). Opus
  4.7 scored 3.0/5 with "uniform colour and weight"; Sonnet 4.6 3.4/5 with the
  cleanest massing. Explicit measured dimensions helped; human annotations drawn
  on the render beat text corrections. A preview PNG looked better than the
  exported STL, which had non-manifold surfaces.

## Self-judgement

- Huang et al., ICLR 2024 (arXiv 2310.01798, snippet): intrinsic
  self-correction without external feedback often fails and can hurt; reported
  gains partly came from oracle labels or weak first prompts.
- Panickssery et al., NeurIPS 2024 (arXiv 2404.13076, snippet): LLM evaluators
  recognise and favour their own generations; text domain.
- Multimodal self-judging leans on text priors over the image (arXiv 2406.11280,
  2508.20655, snippets).

## Failure modes in CAD code

- BenchCAD and ASSEMCAD (snippets): successful renders still show floating
  components; union without face glue gives floating subpieces; polar patterns
  are an error cluster. ASSEMCAD mitigates with safe-union and overlap
  validators.
- Shape-grammar ships (CESCG 2016; 2022 Hacettepe thesis; snippets): primitive
  sets plus rules give similar outlines with varied detail; unconstrained
  derivation tends toward unstructured results.

## Game-art practice (criteria a critic can check)

- Stellaris Dev Diary 230 (search excerpt): silhouette and proportions read
  first at distance; big shapes settled before small; shared motifs unify a
  family.
- Fractured Space art direction, Creative Bloq (search excerpt): an arrowhead
  silhouette reads as a threat across the map; three manufacturers each get a
  distinct vocabulary (stacked cylinders, brutalist cubes, naval modular
  shells); human figures dropped in as scale checks.
- "Moth" analysis, John Liberto, ArtStation (search excerpt): 3 large, 4 mid
  and many small volumes; the large volumes dictate the silhouette.
- Primary/secondary/tertiary shapes (steveyang.readthedocs.io, read, after Neil
  Blevins): primary defines silhouette, secondary individuality, tertiary story.

## Gaps

No controlled study of design-language or style-spec conditioning for
LLM-authored CAD; no measurement of a parts library against floating-part or
symmetry errors (nearest: SceneCraft's skill library, LL3M's retrieval); no
sweep of render-critique round counts for aesthetic quality; no direct
measurement of a model praising its own render (inferred from text-domain
work); no source on boxy "lego" look or scale confusion in LLM 3D code; no
authoritative game-art source for 60/30/10 ship proportions. 3D-PreMise
(2401.06437) and MeshCoder (2508.14879) seen as abstracts only.
