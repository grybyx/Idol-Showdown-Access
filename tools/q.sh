#!/bin/sh
# Spielklassen ansehen (ISA_GAME_MANAGED, ISA_ROSLYN_DIR setzen):
#   tools/q.sh find <Teilname> | members <Typ>... | enum <Typ>...
here="$(cd "$(dirname "$0")" && pwd)"
"${PWSH:-pwsh}" -NoProfile -File "$here/inspect.ps1" "$@" 2>&1 | grep -v -E 'CS1701|Line \||~~~|^\s*$|inspect.ps1:5|Add-Type -Type'
