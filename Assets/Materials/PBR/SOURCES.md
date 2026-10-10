# PBR material sources

All texture sets are from ambientCG (https://ambientcg.com), licensed CC0 1.0 Universal (public domain, https://creativecommons.org/publicdomain/zero/1.0/). 2K-JPG downloads; displacement and NormalDX dropped; NormalGL kept (OpenGL convention, used by Blender and Unity).

| Material | ambientCG id | URL | Metallic source |
|---|---|---|---|
| Paint_Clean_Light | Plastic010 | https://ambientcg.com/view?id=Plastic010 | none in set: constant 0 |
| Paint_Worn_Light | PaintedMetal012 | https://ambientcg.com/view?id=PaintedMetal012 | ambientCG Metalness map |
| Paint_Worn_Dark | PaintedMetal007 | https://ambientcg.com/view?id=PaintedMetal007 | ambientCG Metalness map |
| Steel_Brushed | Metal009 | https://ambientcg.com/view?id=Metal009 | ambientCG Metalness map |
| Metal_Scratched | Metal002 | https://ambientcg.com/view?id=Metal002 | ambientCG Metalness map |
| Gunmetal_Dark | Metal030 | https://ambientcg.com/view?id=Metal030 | ambientCG Metalness map |
| Plates_Riveted | MetalPlates002 | https://ambientcg.com/view?id=MetalPlates002 | ambientCG Metalness map |
| Plates_Panels | MetalPlates006 | https://ambientcg.com/view?id=MetalPlates006 | ambientCG Metalness map |
| Tread_Diamond | DiamondPlate001 | https://ambientcg.com/view?id=DiamondPlate001 | ambientCG Metalness map |
| Rubber | Rubber001 | https://ambientcg.com/view?id=Rubber001 | none in set: constant 0 |
| Plastic_Matte | Plastic004 | https://ambientcg.com/view?id=Plastic004 | none in set: constant 0 |
| Carbon_Fibre | Fabric004 | https://ambientcg.com/view?id=Fabric004 | set map is all 1.0; dielectric, so constant 0 |

Files per material: `_Color`, `_Roughness`, `_NormalGL`, `_Metalness` and `_AO` (where the set has them), plus `_MetallicSmoothness.png` generated here for Unity (Metallic in R, Smoothness = 1 - Roughness in A). AO exists only for Paint_Worn_Light and Paint_Worn_Dark.

Blender library: `Asset Sources/PBR Materials.blend` (catalog Aetheria/PBR, images linked by relative path to this folder). Unity materials use Aetheria/GlowFade.
