# Repository Layout

This repo is organized for release-oriented workflows.

```
.
├── src/                      # Core C# implementation
├── cli/                      # CLI entrypoint
├── tests/                    # xUnit tests
├── packages/                 # Publishable packages (e.g. specter-clr)
├── examples/                 # Demos (Clojure-only + C# host templates)
├── tools/                    # Build/pack scripts
├── docs/                     # Documentation
├── nupkgs/                   # Local package output
└── clojureCLR-nrepl.sln       # Solution (library + CLI + tests + packages)
```

## Build

```
dotnet build clojureCLR-nrepl.sln
```

## Test

```
dotnet test tests/clojureCLR-nrepl.Tests.csproj -c Release
```

## Examples

Examples can be launched with `Clojure.Main` (Clojure-only) or `dotnet run` (C# host templates):

```
Clojure.Main -i examples/run-repl-only.clj -e "(demo.run-repl-only/-main)"
```

## Release

See `docs/RELEASE.md` for packaging and NuGet publish steps.
