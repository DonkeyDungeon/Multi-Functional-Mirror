# Multi-Functional Mirror — candidate 0.1.0

Publisher draft: DonkeyDungeon  
Support: kongtiaoxukun@gmail.com

## Import and place

Import the package for your project's render pipeline. Do not import the HDRP shader into a project without HDRP installed. Pipeline packages share the same core scripts and texture GUIDs; they do not include Unity's render-pipeline packages. Install/configure your chosen render pipeline using Unity Package Manager first.

Open `Samples/<pipeline>/MirrorShowcase`. Drag SquareMirror, RoundMirror or Puddle from `Prefabs/<pipeline>` into your scene. The reflective front points along local +Z; the cyan selection arrow shows this direction. Rotate the object to face the viewer. Puddles face upwards. Assign a material for the matching pipeline.

The component controls capture settings. The material controls appearance. A camera on the back of the surface is intentionally not reflected. Reflection captures exclude all toolkit mirror surfaces to prevent recursive rendering.

## Capture settings

* Resolution: 64–4096 pixels per side; 512 is a useful starting point. Memory and pixel cost grow quadratically. Each visible viewing-camera/surface pair owns one texture and one disabled camera.
* Reflection Layers: intersected with the viewer's culling mask. Exclude expensive layers when appropriate.
* Update Every Frames: 1 for smooth motion; larger values give visibly stale reflections between updates.
* Far Distance: limits reflected-camera distance. Oblique near-plane projection can affect effective far clipping; also use layer culling for predictable exclusion.
* Local Normal: front direction, +Z for supplied meshes. Nonuniform scale is handled with the inverse-transpose normal.
* Clip Offset: reduces geometry leaking through the reflecting plane; excessive values can remove nearby objects.
* Reflect Skybox / Background Color: choose the capture background.
* Transparent Background: preserve capture alpha. Transparency depends on pipeline target format and renderer behavior; see compatibility results before relying on it.
* Scene View Preview: editor preview, refreshed at most ten times per second.

## Appearance

* Mirror Color: tint is applied once after scratch/stain mixing. Alpha controls surface opacity.
* Normal Map / Distortion Strength: perturb the reflected image.
* Scratch Mask: green channel. Scratch Intensity blends pale scratch marks and adds normal-driven distortion. This is **not physically rough or blurred reflection**. A neutral normal map still allows visible scratch coloration.
* Water Stain Mask: alpha channel. Water Stain Strength adds surface marks independently from scratches.
* Puddle Mode: procedural alpha silhouette. Threshold controls coverage; Edge Softness smooths edges; Scale, Warp, Detail and Seed control the shape.
* Water Ripples: animated procedural distortion for puddles, with scale/speed/strength. This is a water-wave effect, not the removed rain system.

## Limitations

Planar surfaces only. No mirrors reflecting other toolkit mirrors. XR/stereo is skipped and unsupported. Mobile, consoles, ray tracing, camera stacks, custom renderers and third-party post-processing are not certified. URP uses the default renderer; reflections do not copy camera post-processing. HDRP uses its normal rendering machinery without any HS2/Graphics plugin integration or special skin SSS switch. Differences in exposure, volumetrics and screen-space effects need scene-specific assessment.

Captures run late in the update loop, outside the SRP render loop. Camera movement after the toolkit's update can produce a one-frame mismatch. Keep camera controllers earlier than execution order 10000, or explicitly call `RenderForCamera` after moving the camera and before rendering. Per-camera property blocks prevent shared-material texture overwrites. Existing renderer-level property-block values are preserved; applications using material-index property blocks must integrate the reflection properties themselves.

## Troubleshooting

Pink surface: check the matching pipeline and shader, then inspect Console errors. Empty reflection: check front direction, layers, camera position and that the surface is in view. Scratches missing: assign the supplied mask and raise intensity; check texture dimensions and green channel. High frame cost: reduce resolution, visible mirror count, capture layers or update frequency. Unsupported render requests: use the documented Unity/pipeline versions.

## Compatibility and changes

Consult the release's validation report for exact tested combinations. Candidate development targets Built-in and URP plus HDRP 16 on Unity 2023.2; wider version support must be separately tested. Unity 2022.3 is the store minimum, not a blanket compatibility claim for this package.

0.1.0 candidate: standalone namespace; removed HS2/BepInEx/Graphics/SSS bridge and external shader inspector; per-surface/per-camera resources; exception-safe culling restoration; independent property blocks; normalized texture names; procedural square/circle meshes; three pipeline material/prefab/demo sets.
