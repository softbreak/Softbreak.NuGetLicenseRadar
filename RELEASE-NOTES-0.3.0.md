# 0.3.0 (release candidate)

- Discover C# solution members from `.sln` and `.slnx`; require explicit selection when multiple solutions exist.
- Use `--recursive` for directory-wide discovery; fail on missing dependency data in scope.
- Responsive Spectre.Console output, framework labels, scan progress, version-specific NuGet links and scope-aware restore guidance.
- Turkish and English UI; preserve original license evidence, JSON schema v1 and review status codes.
- Runtime compatibility: .NET 6–10. Older unsupported runtimes do not receive Microsoft security support.

Validation before version bump: CI #40, 770 successful test executions, five isolated runtime smoke tests; real Softbreak Web solution and recursive acceptance scenarios passed. Final package CI and interactive hyperlink acceptance remain to be verified.
