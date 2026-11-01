# Release Checklist / 发布检查

1. Update `Directory.Build.props`, the CMake base version, and `CHANGELOG.md`. Use release-candidate labels until manual acceptance is complete.
2. Run `scripts/test.ps1 -Configuration Debug` and `scripts/test.ps1 -Configuration Release`. No private game files or developer paths may be prerequisites.
3. Run `scripts/package.ps1`. It builds the pinned native dependencies, tests, publishes self-contained tools and audits the extracted runtime.
4. Check `out/release/`: separate Windows and source ZIPs, with `.sha256` files. The Windows manifest covers all files except itself.
5. Source ZIPs must not contain out/bin/obj, dependency caches, textures, PNGs, fonts, game executables, backups or private reports. Keep license notices.
6. Manually inspect opening, filtering, channels/mips, replacement, undo/redo, save/reopen and duplicate-name behavior on authorized copies. Test in-game before describing a particular mod as validated.
7. Only after owner approval, upload this standalone source tree and attach ZIPs/checksums to the intended release. Do not upload the old localization workspace.

本地准备不会创建远端仓库、推送代码或执行 GitHub Actions。保存格式限制是明确的功能边界，不应在发布说明中宣称支持新增、删改记录结构。
