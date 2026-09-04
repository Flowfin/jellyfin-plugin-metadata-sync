#!/usr/bin/env sh
# Compare the labels a label file declares with the labels a board carries, in
# both directions. This reads and reports; it changes nothing on either side.
#
# The rule this belongs to is written at the top of `.github/labels.yaml`: that
# file is the authority for which labels exist on this board, so it is the whole
# board rather than a list of labels the board should have among others. Nothing
# applies it, so a label the board carries and the file does not is deleted by
# nothing and stays until somebody removes it by hand. A label the file declares
# and the board does not carry is offered by the hygiene gate in the message a
# refused change reads, so the repair it suggests does not exist. Neither
# direction is visible from inside the file.
#
# Usage: label-drift.sh <label-file> <live-name-file>
#
#   <label-file>      a label file in the shape this board's own uses: one
#                     `- name: "<label>"` line per label.
#   <live-name-file>  one label name per line, as the board reports them.
#
# Exit 0 when the two agree, 1 when they differ, 2 when the comparison could
# not be made. The third code is why the parsing below refuses rather than
# skipping: a `- name:` line this cannot read would drop a row from the
# declared set, and a declared set short of a row reports nothing in the
# direction that row was the only member of. An empty declared set compares
# clean against a board carrying no label the file has never heard of, which is
# agreement spelled the same way as silence.

set -eu

if [ "$#" -ne 2 ]; then
    echo "usage: $0 <label-file> <live-name-file>" >&2
    exit 2
fi

label_file=$1
live_name_file=$2

refuse() {
    echo "the comparison was not made: $*" >&2
    exit 2
}

[ -f "$label_file" ] || refuse "no label file at $label_file"
[ -f "$live_name_file" ] || refuse "no live label list at $live_name_file"

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

grep '^- name: ' "$label_file" > "$work/name-lines" ||
    refuse "$label_file declares no labels"
sed -n 's/^- name: "\(.*\)"$/\1/p' "$work/name-lines" | LC_ALL=C sort > "$work/declared"

declared_lines=$(wc -l < "$work/name-lines")
declared_names=$(wc -l < "$work/declared")
if [ "$declared_lines" -ne "$declared_names" ]; then
    refuse "$label_file carries $declared_lines '- name:' line(s) and $declared_names of them are in the '- name: \"<label>\"' shape this reads"
fi

grep -v '^[[:space:]]*$' "$live_name_file" | LC_ALL=C sort > "$work/live" || true
[ -s "$work/live" ] || refuse "the live label list in $live_name_file is empty"

comm -13 "$work/live" "$work/declared" > "$work/absent"
comm -23 "$work/live" "$work/declared" > "$work/undeclared"

name_them() {
    if [ -s "$1" ]; then
        sed 's/^/  /' "$1"
    else
        echo "  none"
    fi
}

echo "Declared in $label_file: $declared_names. Carried by the board: $(wc -l < "$work/live")."
echo
echo "Declared and not carried by the board - the hygiene gate offers a label that does not exist:"
name_them "$work/absent"
echo
echo "Carried by the board and not declared - the next sync run deletes it:"
name_them "$work/undeclared"

if [ -s "$work/absent" ] || [ -s "$work/undeclared" ]; then
    echo
    echo "The file and the board disagree. Nothing here repairs it: a run that repaired the drift silently would remove the reading somebody needs in order to know a label was deleted."
    exit 1
fi

echo
echo "The file and the board agree."
