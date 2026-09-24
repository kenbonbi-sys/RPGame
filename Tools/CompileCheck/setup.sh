#!/usr/bin/env bash
# Downloads what the compile check needs into Tools/CompileCheck/.cache:
#   - Unity reference assemblies from NuGet (UnityEngine modules 2021.3, UnityEditor 2021.1,
#     UnityEngine.UI 2020.3) and NUnit,
#   - the Yarn Spinner for Unity source at the tag pinned in Packages/manifest.json,
# then adds the Unity 6 members listed in patch.spec to the reference assemblies.
# Needs: dotnet SDK 8+, git, curl, unzip (or python3). Run again after editing patch.spec.
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$here/../.." && pwd)"
cache="$here/.cache"
mkdir -p "$cache/nuget" "$cache/refs"

command -v dotnet >/dev/null || { echo "dotnet SDK 8+ is required (Ubuntu: apt install dotnet-sdk-8.0)"; exit 1; }

unzip_to() {   # unzip_to <zip> <dir>
  if command -v unzip >/dev/null; then unzip -qo "$1" -d "$2"
  else python3 -c "import sys,zipfile; zipfile.ZipFile(sys.argv[1]).extractall(sys.argv[2])" "$1" "$2"; fi
}

nuget() {      # nuget <id> <version>  -> extracted into .cache/nuget/<id>
  local id="$1" ver="$2" lower dir
  lower="$(echo "$id" | tr '[:upper:]' '[:lower:]')"
  dir="$cache/nuget/$lower.$ver"
  if [ ! -d "$dir" ]; then
    echo "  nuget $id $ver" >&2
    curl -fsSL --retry 3 -o "$dir.nupkg" "https://api.nuget.org/v3-flatcontainer/$lower/$ver/$lower.$ver.nupkg"
    mkdir -p "$dir" && unzip_to "$dir.nupkg" "$dir" && rm "$dir.nupkg"
  fi
  echo "$dir"
}

echo "[compile-check] reference assemblies"
modules="$(nuget UnityEngine.Modules 2021.3.33 | tail -1)"
sdk="$(nuget Unity3D.SDK 2021.1.14.1 | tail -1)"
ui="$(nuget Unity3D.UnityEngine.UI 2020.3.21 | tail -1)"
nunit="$(nuget NUnit 3.14.0 | tail -1)"
rm -f "$cache/refs/"*.dll
cp "$modules"/lib/net45/*.dll "$cache/refs/"
cp "$sdk/lib/UnityEditor.dll" "$ui/lib/UnityEngine.UI.dll" "$nunit/lib/netstandard2.0/nunit.framework.dll" "$cache/refs/"

echo "[compile-check] Yarn Spinner source"
yarn_url="$(grep -o '"dev.yarnspinner.unity": *"[^"]*"' "$root/Packages/manifest.json" | sed -E 's/.*: *"([^"]*)"/\1/')"
yarn_repo="${yarn_url%%#*}"
yarn_tag="${yarn_url##*#}"
if [ ! -f "$cache/yarn/.tag" ] || [ "$(cat "$cache/yarn/.tag")" != "$yarn_tag" ]; then
  rm -rf "$cache/yarn"
  git -c advice.detachedHead=false clone -q --depth 1 --branch "$yarn_tag" "$yarn_repo" "$cache/yarn"
  echo "$yarn_tag" > "$cache/yarn/.tag"
fi
echo "  $yarn_repo $yarn_tag"

echo "[compile-check] adding Unity 6 members (patch.spec)"
dotnet build "$here/RefPatch/RefPatch.csproj" -nologo -v q -c Release -o "$cache/refpatch" >/dev/null
dotnet "$cache/refpatch/RefPatch.dll" "$here/patch.spec" "$cache/refs"

sha1sum "$here/patch.spec" "$root/Packages/manifest.json" > "$cache/.setup-stamp"
echo "[compile-check] ready"
