# A deliberate regression for the sign-off gate

Nothing uses this file and nothing should merge this branch. It exists so the
commit carrying it exists, because the regression being proved is in the
commit's authorship and not in any tracked byte.

The commit that adds this file is authored with `--get-all` where an address
belongs, and carries a `Signed-off-by` trailer naming the same value. That is
the exact shape that reached the mainline behind a green check: both sides of
the old comparison came from the commit, the commit agreed with itself, and the
gate said so.

One commit, one refusal, so the run names one line.
