# Release

This repo publishes two NuGet packages:
- `clojure.core.async.clrfix`
- `com.rpl.specter.clr`

## Prerequisites

1. A NuGet.org API key with `Push` scope, restricted to the two package IDs above.
2. Versions to release:
   - `clojure.core.async.clrfix` default: `1.7.701-clrfix2`
   - `com.rpl.specter.clr` default: `1.1.7-clrfix1`

Recommended: export an environment variable:
```
export NUGET_API_KEY="YOUR_KEY"
```

## Build and Pack

1. Generate CLR-friendly `.clj` sources for Specter:
```
clojure -Sdeps '{:deps {org.clojure/tools.reader {:mvn/version "1.3.7"}}}' -e '(load-file "tools/gen-cljr.clj")'
```

2. Build core.async package:
```
PACKAGE_ID=clojure.core.async.clrfix VERSION=1.7.701-clrfix2 tools/build_core_async_local.sh
```

3. Build Specter package:
```
dotnet pack packages/specter-clr/specter-clr.csproj -c Release -o nupkgs /p:PackageId=com.rpl.specter.clr /p:PackageVersion=1.1.7-clrfix1
```

Packages will be under `nupkgs/`.

## Push to NuGet.org

1. Push core.async:
```
dotnet nuget push nupkgs/clojure.core.async.clrfix.1.7.701-clrfix2.nupkg -k "$NUGET_API_KEY" -s https://api.nuget.org/v3/index.json --skip-duplicate
```

2. Push Specter:
```
dotnet nuget push nupkgs/com.rpl.specter.clr.1.1.7-clrfix1.nupkg -k "$NUGET_API_KEY" -s https://api.nuget.org/v3/index.json --skip-duplicate
```

## Notes

- The Specter build embeds `.clj` resources. Always edit `.cljc` sources and regenerate before packing.
- NuGet indexing can take several minutes after upload.
