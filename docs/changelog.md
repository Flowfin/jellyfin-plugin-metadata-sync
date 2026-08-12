# The changelog, and what an entry is

An operator reading a release note is deciding one thing: whether upgrading is
safe to do without reading anything else. Most entries do not affect that
decision. A few decide it entirely, and those are the ones a changelog sorted by
feature and fix hides, because a change to the contract this plugin codes
against can arrive spelled as a fix and read as one.

So an entry carries a class, and three of the classes exist to mark exactly the
changes an operator has to read before upgrading.

## An entry is written from the issue, not from the diff

Every change on this board starts as an issue that says what is wrong, what the
evidence is and what done means. The entry is a sentence out of that, which is
why it can be one line: the reasoning is already written down and the entry
points at it.

A changelog written from the diff says what moved in the tree. That is a
different document, and `git log` is better at it. The failure this avoids is an
entry saying a file changed, next to an entry saying an operator has to
reconfigure something before the next pass, in the same voice and the same size.

## The classes

One row per class. `Label` is what routes a pull request into it, and it is the
label that decides, not the wording of the title.

| Class | Label | What it is for |
| --- | --- | --- |
| Contract change | `changelog: contract` | The consumer contract this plugin speaks to the pairing plugin over: the purpose it registers, the shape of a payload, or the version it pins. Two servers running different versions of this plugin meet here first. |
| Field register change | `changelog: field-register` | Which fields move and which never do. A field that starts moving writes to a library that was not being written to before, and a field that stops moving leaves two servers diverging where they used to agree. |
| Conflict rule change | `changelog: conflict-rule` | How a disagreement between two servers is decided. The same libraries and the same pass produce a different winner afterwards, and nothing in the result says which rule chose it. |
| Security | `security` | A change to what this plugin exposes, to who may call it, or to what it holds. |
| Added | `enhancement` | Something this plugin did not do before. |
| Fixed | `bug` | Something that did not work as the plan said it would. |
| Documentation | `documentation` | A change to what is written down, with no change to what runs. |

The three at the top are not severities. A contract change can be small and a
fix can be large; what puts an entry in one of those three is what an operator
has to know before upgrading, and that is a property of the subject rather than
of the size.

An entry belongs to exactly one class. A change that would carry two is two
changes, and this plan already refuses a pull request carrying two topics for a
separate reason.

## An example of each

Each example is one line, in the past tense, naming what changed and what it
means for somebody running this. It ends with the issue the reasoning is reached
through, and it never restates that reasoning.

The number in each is written as `#123` throughout. These are examples of the
shape rather than entries anybody wrote, and putting a real issue number on an
invented sentence would leave a document making a claim about work nobody did.

**Contract change**

    - Pinned the consumer contract to a version and refused registration against
      an older pairing plugin, rather than failing part-way through a pass (#123)

**Field register change**

    - Declared every image field as never moving, with what the refusal costs
      written beside it (#123)

**Conflict rule change**

    - Refused a conflict that no declared rule decides, instead of falling
      through to the last rule that matched (#123)

**Security**

    - Kept the server's user-scoped types out of the plugin assembly, asserted by
      a scan of the built metadata rather than by review (#123)

**Added**

    - Resolved an episode as its series plus an ordinal, and refused absolute
      numbering, season zero and a file covering more than one episode (#123)

**Fixed**

    - Refused a commit whose author address is not an address, instead of
      comparing a message with itself and agreeing (#123)

**Documentation**

    - Wrote down what is stored where, including the things this plugin holds in
      neither place (#123)

## What holds this up

`ChangelogTests` in the suite reads the table above and
`.github/release-drafter.yml`, and refuses four things. A class here with no
category there, so a class cannot be argued for and then quietly not applied. A
category there with no row here, so a category cannot arrive with nothing saying
what it is for. A label either side names that `.github/labels.yaml` does not
declare, because the label is what routes an entry and a label that does not
exist routes nothing. And a class with no example above.

The bound is worth knowing. The suite reads these files as lines rather than
parsing YAML, so it answers what the files say and never what the drafter does
with them. It also cannot judge whether an entry was put in the right class,
which is what the review is for, or whether the sentence in an entry is true.

`.github/workflows/pr-hygiene.yml` refuses a pull request that carries no class
label, and one that carries two. It also refuses a change to the field register
or to the conflict rules that carries no label of that change's own class, which
is the case this document is most about: those two arrive spelled as a fix and
are read as one.

The gate carries the class list rather than reading this table, because it runs
with nothing checked out. The four legs above hold the two against each other,
so a class added here and not there is a class the gate would refuse every
change that carries it, and the suite says so before anybody meets it on a pull
request.

The contract class is the one the gate cannot key to a subject. There is no
contract type in this tree for it to watch, and a path written for a file that
does not exist is a rule pointed at nothing. So a contract change is refused
only by the leg that asks for a class at all, and which class it belongs in is
still a convention this document describes.

## Where the entries end up

`.github/workflows/changelog.yaml` is the route that drafts a release from them.
Its header says what that route waits on, and none of it is decided here.
