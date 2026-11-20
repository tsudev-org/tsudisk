# Contributing to TsuDisk

_Tiếng Việt: mọi đóng góp đều được chào đón. Bạn có thể viết issue và pull request bằng tiếng Việt hoặc tiếng Anh._

TsuDisk is a Windows partition manager that runs with administrator rights and rewrites partition tables, so a logic
bug can destroy data. Contributions are welcome, and the bar for changes that touch disks is deliberately high. Read
[README.md](README.md) (safety mechanisms) and [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) first.

## Reporting bugs and requesting features

- **Bug**: open an issue with the *Bug report* form: <https://github.com/tsudev-org/tsudisk/issues/new/choose>.
  Include the version (`tsudisk phien-ban`), Windows version, the disk layout (`tsudisk danh-sach --json`, device
  names may be masked) and the steps to reproduce.
- **Feature**: use the *Feature request* form; see [docs/ROADMAP.md](docs/ROADMAP.md) for what is planned.
- **Security vulnerability** (including any way to make TsuDisk touch a partition outside the published plan): never
  in a public issue - see [SECURITY.md](SECURITY.md).

**Never test on a disk with data you care about.** Use a virtual disk (VHDX), as `tests/e2e/run-e2e.ps1` does.

## Getting the source, building and testing

Requirements: the .NET SDK pinned in [`global.json`](global.json) (any OS for the core and unit tests; Windows for the
executable and the end-to-end tests); Node.js 22 for the website.

```bash
git clone https://github.com/tsudev-org/tsudisk.git
cd tsudisk
dotnet build TsuDisk.slnx -c Release              # warnings are errors
dotnet test tests/TsuDisk.Core.Tests -c Release   # unit, fault-injection and fuzz tests (Linux or Windows)
```

On Windows, as administrator:

```powershell
dotnet publish src/TsuDisk.Cli -c Release -r win-x64 -o publish
./tests/e2e/run-e2e.ps1 -Exe ./publish/tsudisk.exe   # builds a 3 GB VHDX and runs the real tsudisk.exe on it
```

The fuzz tests use a fixed seed; set `TSUDISK_FUZZ_SEED=<number>` to explore other inputs.

## Pull request requirements

Every change goes through a pull request into `main` (direct pushes are blocked) and all CI checks must pass.

1. **One change per pull request** with a description of what and why. Commit messages follow
   [Conventional Commits](https://www.conventionalcommits.org/) (`feat: ...`, `fix: ...`, `docs: ...`).
2. **Tests**: every change in behaviour comes with tests in the same pull request. A bug fix comes with a regression
   test that fails without the fix. A new write operation also needs fault-injection tests (a step that fails
   triggers the rollback, which restores the previous layout) and, where possible, an end-to-end case on the VHDX.
3. **Safety invariants**: plans must pass `SafetyGuard` (other partitions keep position, size and type; no overlap;
   1 MiB alignment). A change to what a plan may touch needs an ADR in [docs/adr/](docs/adr/).
4. **System tools** (`diskpart`, `reagentc`, `bcdedit`) are called by absolute System32 path with separate arguments,
   never through a shell; native libraries load from System32 only.
5. **CI must be green**: build with warnings as errors (.NET analyzers `latest-recommended`), unit tests with coverage,
   Windows packaging and E2E on a virtual disk, website build, accessibility and worker tests, gitleaks, CodeQL
   (`security-extended`), vulnerable NuGet package check, TsuDEV standards check.
6. **Changelog**: user-visible changes get an entry in [CHANGELOG.md](CHANGELOG.md); the release notes are taken from it.

## Releases

Versions use the calendar format `YY.M.DDNN` (`26.11.1701` = release 01 on 17/11/2026, see README). A release is a
tag `vYY.M.DDNN` on `main`; the release workflow builds `tsudisk.exe`, attaches an SPDX SBOM, a Sigstore-signed
`SHA256SUMS.txt` and a GitHub build provenance attestation.

## License

By contributing you agree that your work is licensed under the [Apache License 2.0](LICENSE). Everyone taking part
follows the [TsuDEV Code of Conduct](https://github.com/tsudev-org/.github/blob/main/CODE_OF_CONDUCT.md).
