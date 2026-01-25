# QuakeKit Unit Tests

This directory contains unit tests that can run outside of Unity using standard .NET testing tools.

## Running Tests

### From Command Line

```bash
# Run all tests
dotnet test tests/QuakeKit.Tests/QuakeKit.Tests.csproj

# Run with detailed output
dotnet test tests/QuakeKit.Tests/QuakeKit.Tests.csproj --verbosity detailed

# Run with code coverage
dotnet test tests/QuakeKit.Tests/QuakeKit.Tests.csproj --collect:"XPlat Code Coverage"
```

### From IDE

- **Visual Studio**: Open the .csproj and use Test Explorer
- **Visual Studio Code**: Install C# Dev Kit extension
- **Rider**: Open the .csproj and use Unit Tests window

## What Can Be Tested

✅ **Testable without Unity:**
- Native binding P/Invoke signatures
- Data structures (TextureBounds, etc.)
- Pure C# logic and algorithms
- Type conversions and utilities
- Configuration parsing

❌ **Requires Unity:**
- Asset importers (ScriptedImporter)
- Editor tools
- MonoBehaviour components
- Unity asset database operations

## Adding Tests

1. Create a test file in the appropriate directory
2. Use xUnit attributes: `[Fact]` for simple tests, `[Theory]` for parameterized tests
3. Run tests locally before committing

## CI/CD

Tests run automatically on push via GitHub Actions. See [.github/workflows/test.yml](../.github/workflows/test.yml).
