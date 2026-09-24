#!/usr/bin/env bash
# Compiles the game's C# the way Unity would, without Unity:
#   Assembly-CSharp (editor), Assembly-CSharp (player build), Assembly-CSharp-Editor (tools + tests).
# Exit code 0 = no compile errors. Pass --warnings to also list warnings in Assets/.
# It only proves the code compiles; it does not run the game or the tests.
set -uo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$here/../.." && pwd)"
cache="$here/.cache"
show_warnings=0
[ "${1:-}" = "--warnings" ] && show_warnings=1

if [ ! -f "$cache/.setup-stamp" ] || ! (cd / && sha1sum --status -c "$cache/.setup-stamp" 2>/dev/null); then
  "$here/setup.sh" || exit 1
fi

status=0
seen=""
for proj in Runtime/Assembly-CSharp Player/Assembly-CSharp-Player Editor/Assembly-CSharp-Editor; do
  out="$(dotnet build "$here/Game/$proj.csproj" -nologo -v q 2>&1)"
  errors="$(echo "$out" | grep -E ': error ' | sed -E "s# \[[^]]*\]\$##; s#$root/##; s#$cache/##" | sort -u)"
  name="$(basename "$proj")"
  if [ -n "$errors" ]; then
    echo "✗ $name"
    new="$(comm -23 <(echo "$errors") <(echo "$seen" | sort -u))"
    if [ -n "$new" ]; then echo "$new"; else echo "  (same errors as above)"; fi
    seen="$(printf '%s\n%s' "$seen" "$errors")"
    status=1
  else
    echo "✓ $name"
  fi
  if [ "$show_warnings" = 1 ]; then
    echo "$out" | grep -E ': warning CS' | grep "$root/Assets/" | sed -E "s# \[[^]]*\]\$##; s#$root/##" | sort -u
  fi
done
exit $status
