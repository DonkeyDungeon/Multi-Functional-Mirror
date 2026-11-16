# Changelog

## 0.1.0-rc.1

- Separate Unity packages for Built-in, URP and HDRP, each with upright mirrors, a puddle and a showcase scene.
- Each viewing camera binds its own capture. A camera without a valid capture sees a transparent mirror surface.
- Disabling a mirror releases its reflection resources and preserves renderer properties set by other scripts.
- Material controls include distortion, scratches, stains and water ripples. Toolkit mirror surfaces are excluded from reflection captures.
- See [Compatibility](Assets/DonkeyDungeon/PlanarMirror/Documentation/Compatibility.md) for tested versions and limitations.
