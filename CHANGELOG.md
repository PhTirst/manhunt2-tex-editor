# Changelog

## 0.1.0-rc.1 - 2026-11-01

- Extract the WinForms GUI and native TEX core into a standalone project.
- Pin upstream archive versions and hashes; remove old workspace dependencies.
- Preserve record order and handle duplicate names by physical record offset.
- Retain format, dimensions, mip count and slot capacity during PNG replacement.
- Catch batch-operation failures, refresh the preview after work, and prevent closing during background operations.
- Target .NET 10 and publish a self-contained Windows x64 package.
- Add synthetic raw/Z2HM and BGRA/BC3 tests, CI configuration, manifests and checksums.

This release candidate still requires manual GUI and in-game acceptance for the intended mod.
