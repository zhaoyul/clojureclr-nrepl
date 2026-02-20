#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WORK_DIR="${ROOT_DIR}/.tmp/clr.core.async"
OUT_DIR="${ROOT_DIR}/nupkgs"
TAG="${TAG:-v1.7.701}"
VERSION="${VERSION:-1.7.701-clrfix2}"
PACKAGE_ID="${PACKAGE_ID:-clojure.core.async.clrfix}"
AUTHORS="${AUTHORS:-ClojureCLR nREPL Contributors}"
REPO_URL="${REPO_URL:-https://github.com/clojure/clr.core.async}"
DESCRIPTION="${DESCRIPTION:-Patched core.async for ClojureCLR (array type-hint fix).}"
LICENSE="${LICENSE:-EPL-1.0}"
PACKAGE_TAGS="${PACKAGE_TAGS:-Clojure;ClojureCLR;core.async}"

rm -rf "${WORK_DIR}"
mkdir -p "${WORK_DIR}"
mkdir -p "${OUT_DIR}"

git clone --depth 1 --branch "${TAG}" https://github.com/clojure/clr.core.async.git "${WORK_DIR}"

PATCH_FILE="${WORK_DIR}/src/main/clojure/clojure/core/async/impl/concurrent.clj"
python - <<PY
path = "${PATCH_FILE}"
text = open(path, "r", encoding="utf-8").read()
if "^|Object[]|" not in text:
    raise SystemExit("Patch target not found in " + path)
text = text.replace("^|Object[]|", "^|System.Object[]|")
open(path, "w", encoding="utf-8").write(text)
PY

BUILD_DIR="${WORK_DIR}/build"
mkdir -p "${BUILD_DIR}"

cat > "${BUILD_DIR}/README.md" <<'MD'
# core.async (CLR fix)

This package is a patched build of `clojure.core.async` for ClojureCLR.

Fixes:
- CLR array type hint: `^|Object[]|` -> `^|System.Object[]|`

Source: https://github.com/clojure/clr.core.async
MD

cat > "${BUILD_DIR}/core.async.csproj" <<'XML'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <AssemblyName>clojure.core.async</AssemblyName>
    <RootNamespace>clojure.core.async</RootNamespace>
    <PackageId>__PACKAGE_ID__</PackageId>
    <Version>__VERSION__</Version>
    <Authors>__AUTHORS__</Authors>
    <Description>__DESCRIPTION__</Description>
    <PackageLicenseExpression>__LICENSE__</PackageLicenseExpression>
    <PackageRequireLicenseAcceptance>false</PackageRequireLicenseAcceptance>
    <PackageTags>__PACKAGE_TAGS__</PackageTags>
    <RepositoryType>git</RepositoryType>
    <RepositoryUrl>__REPO_URL__</RepositoryUrl>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="Placeholder.cs" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="clojure.tools.analyzer.clr" Version="1.3.2" />
  </ItemGroup>

  <ItemGroup>
    <None Include="README.md" Pack="true" PackagePath="\" />
    <EmbeddedResource Include="../src/main/clojure/**/*.clj">
      <LogicalName>$([System.String]::Copy('%(RecursiveDir)').Replace('\\','/').Replace('/','.'))%(Filename)%(Extension)</LogicalName>
    </EmbeddedResource>
  </ItemGroup>
</Project>
XML

python - <<PY
path = "${BUILD_DIR}/core.async.csproj"
text = open(path, "r", encoding="utf-8").read()
repl = {
    "__PACKAGE_ID__": "${PACKAGE_ID}",
    "__VERSION__": "${VERSION}",
    "__AUTHORS__": "${AUTHORS}",
    "__DESCRIPTION__": "${DESCRIPTION}",
    "__LICENSE__": "${LICENSE}",
    "__PACKAGE_TAGS__": "${PACKAGE_TAGS}",
    "__REPO_URL__": "${REPO_URL}",
}
for k, v in repl.items():
    text = text.replace(k, v)
open(path, "w", encoding="utf-8").write(text)
PY

cat > "${BUILD_DIR}/Placeholder.cs" <<'CS'
namespace clojure.core.async
{
    internal static class Placeholder
    {
    }
}
CS

dotnet pack -c Release -o "${OUT_DIR}" "${BUILD_DIR}/core.async.csproj" /p:PackageVersion="${VERSION}"

echo "Built package: ${OUT_DIR}/${PACKAGE_ID}.${VERSION}.nupkg"
