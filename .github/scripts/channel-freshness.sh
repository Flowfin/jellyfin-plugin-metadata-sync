#!/usr/bin/env sh
# Compare the versions a plugin catalogue advertises for one plugin with the
# releases that plugin's repository has published, in both directions. This
# reads and reports; it changes nothing on either side.
#
# The failure it is against is the one #88 is written around and that this board
# has met: a publish run reports success, the catalogue is rebuilt from the
# releases, and the file an operator's server actually reads still advertises
# the version before it. A green publish is therefore not a current channel, and
# the only reading that settles it is one taken from the served file.
#
# Usage: channel-freshness.sh <manifest-file> <releases-file> <guid>
#
#   <manifest-file>  the catalogue as served: a JSON array of plugin entries,
#                    each with a `guid` and a `versions` array of objects
#                    carrying a `version`.
#   <releases-file>  this repository's releases as the API returns them: a JSON
#                    array of objects carrying `tag_name`, `draft` and
#                    `prerelease`.
#   <guid>           the plugin's identifier, as `build.yaml` declares it.
#
# A tag carries a suffix the catalogue's version string does not, and one
# version can be released once per server line, so the tag is cut down to the
# version and the two sides are compared as sets.
#
# Exit 0 when the two agree, 1 when they differ, 2 when the comparison could not
# be made. The third code is what separates a disagreement from a reading that
# never happened: a catalogue carrying no entry for this guid, a repository with
# no published release, and a file that is not JSON at all each compare clean
# against everything, which is agreement spelled the same way as silence.

set -eu

if [ "$#" -ne 3 ]; then
    echo "usage: $0 <manifest-file> <releases-file> <guid>" >&2
    exit 2
fi

manifest=$1
releases=$2
guid=$3

for f in "$manifest" "$releases"; do
    if [ ! -f "$f" ]; then
        echo "cannot read $f" >&2
        exit 2
    fi
done

if ! jq -e 'type == "array"' "$manifest" >/dev/null 2>&1; then
    echo "the catalogue is not a readable JSON array: $manifest" >&2
    exit 2
fi

if ! jq -e 'type == "array"' "$releases" >/dev/null 2>&1; then
    echo "the release list is not a readable JSON array: $releases" >&2
    exit 2
fi

if [ -z "$guid" ]; then
    echo "no plugin identifier was given" >&2
    exit 2
fi

entries=$(jq --arg g "$guid" '[.[] | select(.guid == $g)] | length' "$manifest")
if [ "$entries" -eq 0 ]; then
    echo "the catalogue carries no entry for $guid" >&2
    exit 2
fi

# An entry with no `versions` array advertises nothing, which is a difference
# rather than an unreadable file, so it falls through to the comparison below.
advertised=$(jq -r --arg g "$guid"     '[.[] | select(.guid == $g)][0].versions // [] | .[].version' "$manifest"     | sed '/^$/d' | sort -u)

# A draft is not published and a prerelease is not what the stable channel
# advertises, so neither is a release this comparison is about.
published=$(jq -r     '.[] | select(.draft == false) | select(.prerelease == false) | .tag_name'     "$releases" | sed -e 's/-stable$//' -e 's/-jf[0-9][0-9]*$//' -e '/^$/d' | sort -u)

if [ -z "$published" ]; then
    echo "this repository has published no release" >&2
    exit 2
fi

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
printf '%s
' "$advertised" | sed '/^$/d' > "$work/advertised"
printf '%s
' "$published" > "$work/published"

unadvertised=$(comm -23 "$work/published" "$work/advertised")
unreleased=$(comm -13 "$work/published" "$work/advertised")

say() {
    echo "$1"
    if [ -z "$2" ]; then
        echo "  none"
    else
        printf '%s
' "$2" | sed 's/^/  /'
    fi
}

say 'Published and not advertised' "$unadvertised"
say 'Advertised and not published' "$unreleased"

if [ -n "$unadvertised" ] || [ -n "$unreleased" ]; then
    echo
    echo "The catalogue and the releases disagree."
    exit 1
fi

echo
echo "The catalogue advertises exactly the versions this repository has published."
exit 0
