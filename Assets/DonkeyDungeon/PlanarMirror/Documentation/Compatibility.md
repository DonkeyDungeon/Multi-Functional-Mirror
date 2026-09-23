# Compatibility and validation / 兼容性与验证

Multi-Functional Mirror — release candidate 0.1.0. Publisher draft: DonkeyDungeon. Support: kongtiaoxukun@gmail.com.

| Pipeline / 管线 | Editor / 编辑器 | Pipeline package | Windows D3D11 | Clean import / 全新导入 |
|---|---|---|---|---|
| Built-in | 2022.3.51f1 | Built-in | PASS | PASS |
| URP | 2023.2.20f1c1 | 16.0.6 | PASS | PASS |
| HDRP | 2023.2.20f1c1 | 16.0.6 | PASS | PASS |

Built-in also passed an initial editor rendering test in 2023.2.20f1c1. Other combinations are not certified. Unity 6.4.2f1 with SRP 17.4.0 failed initial dependency compilation in this installed environment and is not listed as supported. This does not establish that every Unity 6 installation is incompatible.

Test hardware: NVIDIA GeForce RTX 4060 Ti. Output: 1280×720. Three surfaces, each with a 1024×1024 reflection texture; a second viewing camera is added for isolation tests. Windows tests use explicit render requests for deterministic offscreen captures. These are functional checks, not a performance benchmark.

Verified: actual active renderer; nonempty reflection pixels on square/circle/puddle surfaces; real supplied scratch texture changes final output; distortion changes final output; shared materials retain independent reflection textures; additional camera allocation; destroyed source-camera pruning; disable, re-enable and delete cleanup; no test-time runtime errors. Fresh projects install only the required pipeline, import the exported unitypackage, and repeat the checks.

已验证：运行中的真实管线、三种表面的反射像素、随包划痕贴图的实际效果、扭曲参数、多镜面共享材质隔离、多观察相机分配与销毁清理、禁用/重启/删除资源释放。三种管线均已在 Windows 程序和全新导入工程中通过这些检查。

Not certified / 尚未认证：XR、移动端、主机、Unity 6、URP/HDRP 14、任意第三方后处理、相机堆叠、自定义 Renderer、全面透明背景兼容、全部变换组合、性能宣传数据。No recursive mirror reflections. Scratch intensity is a mask/distortion effect, not physical roughness or blur.

Transparent-background behavior depends on capture alpha and pipeline buffer settings. HDRP projects may require an alpha-capable color buffer. Do not promise this feature works with every project configuration without additional visual testing.

This candidate has not been approved by Unity Asset Store review. Publisher registration, source/license records, official publishing-tool validation, product artwork and pricing remain publisher preparation tasks.
