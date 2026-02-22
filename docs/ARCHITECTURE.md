# Architecture

This repo contains a C# nREPL server for ClojureCLR plus supporting packages and examples.

## Runtime Architecture

**Core runtime (C#)**
- `NReplServer` accepts TCP clients and routes nREPL ops.
- `BencodeCodec` encodes/decodes nREPL messages.
- Middleware-like handlers implement `eval`, `complete`, `info`, `eldoc`, etc.

**ClojureCLR runtime**
- ClojureCLR assemblies are loaded on first evaluation.
- The server maintains per-session namespaces and supports CIDER-compatible metadata responses.

## Package Architecture

- `clojureCLR-nrepl.csproj` produces the core library (`ClojureCLR.NRepl`).
- `cli/clojureCLR-nrepl-cli.csproj` is the standalone server binary.
- `packages/specter-clr/` is a CLR-friendly port of Specter.

## Examples

Examples are pure Clojure entrypoints under `examples/`. They optionally start an nREPL server and then run the demo logic:

- `run-core.clj` (core.async + nREPL)
- `run-specter.clj`
- `run-webservice.clj` (HttpListener)
- `run-webservice-minimal.clj` (Minimal API)
- `run-repl-only.clj` (minimal nREPL starter)

## Build/Release Flow

1. Build core library: `dotnet build`
2. Run tests: `dotnet test tests/clojureCLR-nrepl.Tests.csproj -c Release`
3. Package (see `docs/RELEASE.md`)

