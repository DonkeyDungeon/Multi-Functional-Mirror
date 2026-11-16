# Compatibility and validation

Multi-Functional Mirror — 0.1.0-rc.1. Maintainer: DonkeyDungeon. Support: kongtiaoxukun@gmail.com.

| Pipeline | Editor | Pipeline package | Windows D3D11 | Clean import |
|---|---|---|---|---|
| Built-in | 2022.3.51f1 | Built-in | PASS | PASS |
| URP | 2023.2.20f1c1 | 16.0.6 | PASS | PASS |
| HDRP | 2023.2.20f1c1 | 16.0.6 | PASS | PASS |

Built-in also passed an initial editor rendering test in 2023.2.20f1c1. Other combinations are not certified. Unity 6.4.2f1 with SRP 17.4.0 failed initial dependency compilation in this installed environment and is not listed as supported. This does not establish that every Unity 6 installation is incompatible.

Test hardware: NVIDIA GeForce RTX 4060 Ti. Output: 1280×720. Three surfaces, each with a 1024×1024 reflection texture; a second viewing camera is added for isolation tests. Windows tests use explicit render requests for deterministic offscreen captures. These are functional checks, not a performance benchmark.

Verified: actual active renderer; nonempty reflection pixels on square/circle/puddle surfaces; real supplied scratch texture changes final output; distortion changes final output; shared materials retain independent reflection textures; additional camera allocation; destroyed source-camera pruning; disable, re-enable and delete cleanup; no test-time runtime errors. Fresh projects install only the required pipeline, import the exported unitypackage, and repeat the checks.

Validation does not cover XR, mobile, consoles, Unity 6, URP or HDRP 14, arbitrary third-party post-processing, camera stacking, custom renderers, comprehensive transparent-background compatibility, all transform combinations, or performance benchmarks. Reflection capture excludes other PlanarMirror surfaces. Scratch intensity controls a mask and distortion.

Transparent-background behavior depends on capture alpha and pipeline buffer settings. HDRP projects may require an alpha-capable color buffer. Do not promise this feature works with every project configuration without additional visual testing.

Release downloads and SHA-256 checksums are provided through GitHub Releases.
