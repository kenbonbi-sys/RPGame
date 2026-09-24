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

pkg_dir() { echo "$cache/nuget/$(echo "$1" | tr '[:upper:]' '[:lower:]').$2"; }

fetch() {      # fetch <id> <version>: extracts the NuGet package into $(pkg_dir id version)
  local id="$1" ver="$2" lower dir
  lower="$(echo "$id" | tr '[:upper:]' '[:lower:]')"
  dir="$(pkg_dir "$id" "$ver")"
  [ -d "$dir" ] && return 0
  echo "  nuget $id $ver"
  rm -rf "$dir.tmp" && mkdir -p "$dir.tmp"
  curl -fsSL --retry 3 -o "$dir.nupkg" "https://api.nuget.org/v3-flatcontainer/$lower/$ver/$lower.$ver.nupkg"
  unzip_to "$dir.nupkg" "$dir.tmp"
  chmod -R u+rwX "$dir.tmp"   # packages zipped on Windows extract without read permission
  mv "$dir.tmp" "$dir"
  rm -f "$dir.nupkg"
}

echo "[compile-check] reference assemblies"
fetch UnityEngine.Modules 2021.3.33
fetch Unity3D.SDK 2021.1.14.1
fetch Unity3D.UnityEngine.UI 2020.3.21
fetch NUnit 3.14.0
rm -f "$cache/refs/"*.dll
cp "$(pkg_dir UnityEngine.Modules 2021.3.33)"/lib/net45/*.dll "$cache/refs/"
cp "$(pkg_dir Unity3D.SDK 2021.1.14.1)/lib/UnityEditor.dll" \
   "$(pkg_dir Unity3D.UnityEngine.UI 2020.3.21)/lib/UnityEngine.UI.dll" \
   "$(pkg_dir NUnit 3.14.0)/lib/netstandard2.0/nunit.framework.dll" "$cache/refs/"

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
