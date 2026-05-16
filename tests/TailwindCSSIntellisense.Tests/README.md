
## Testing

Automated tests are in `tests/TailwindCSSIntellisense.Tests`.

### What is covered

- Tailwind class parsing and detection behavior
- Sorting and formatting logic for class-ordering behavior
- Utility/helper methods used by parsing and sorting
- Integration-style scenarios for mixed-language class detection and IntelliSense-related filtering behavior
- Command-path validation behavior (CLI usage checks)

### Run tests locally

From the repository root:

```bash
dotnet test tests/TailwindCSSIntellisense.Tests/TailwindCSSIntellisense.Tests.csproj
```

### Add new tests

- Add tests under `UnitTests` for single-module logic
- Add tests under `IntegrationTests` for cross-module behavior
- Reuse `Stubs/TestStubs.cs` to mock Visual Studio SDK dependencies so tests stay headless
- Keep test names scenario-based and include edge cases (invalid classes, large inputs, mixed HTML/JS contexts)