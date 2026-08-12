# Comparing provider identifiers

Provider identifiers arrive as a dictionary of provider name to identifier, on
the item and on the transfer object. Neither the keys nor the values are
normalised for us: they are written by whichever providers an operator enabled,
at different times, by different versions, on two servers that were never
coordinated with each other.

So the rules are per provider, they are written here, and they are a table
rather than a chain of conditionals. A chain is where a matcher quietly becomes
wrong, because the case that was handled and the case that fell through look the
same from outside.

This document is a rendering of
`Jellyfin.Plugin.MetadataSync/Matching/provider-identifiers.json`, which ships
inside the assembly and is the only place a rule is declared. The suite holds
the two against each other, so neither is quietly right when they disagree.

## The provider name

Compared `OrdinalIgnoreCase`, for every row.

The dictionary key is a provider name written by whichever provider version was
installed when the item was first saved, on either server. Two servers writing
the same provider with different capitalisation is a spelling difference and
never a different provider.

## One row per provider

The last row is the rule for a provider this table does not name. It is not a
gap, it is a decision, and it is the fail-safe direction.

| Provider | Identifier comparison | Whitespace | Normalisation | Reason |
| --- | --- | --- | --- | --- |
| `Imdb` | OrdinalIgnoreCase | trimmed | none | An IMDb identifier is a tt prefix and digits, and the prefix is the only part where case can differ at all. Stripping the prefix is deliberately not done: a value without it is not the same identifier written differently, it is a different string, and a table that repaired it would be guessing. |
| `Tmdb` | Ordinal | trimmed | LeadingZeros | A TMDb identifier is a decimal number. One server writing it zero-padded and the other not is the same identifier, so leading zeros are dropped before the comparison, and only when the whole value is digits. |
| `Tvdb` | Ordinal | trimmed | LeadingZeros | A TVDB identifier is a decimal number, and the same zero-padding difference occurs. Same rule and same bound: the normalisation applies only where the whole value is digits. |
| `MusicBrainzAlbum` | OrdinalIgnoreCase | trimmed | none | A MusicBrainz identifier is a UUID, whose hexadecimal digits carry the same value in either case. The hyphens are part of the identifier and are not removed, because a value without them is a different spelling this table has not been shown. |
| `MusicBrainzArtist` | OrdinalIgnoreCase | trimmed | none | A UUID, for the same reason as the album row. |
| `MusicBrainzReleaseGroup` | OrdinalIgnoreCase | trimmed | none | A UUID, for the same reason as the album row. |
| `MusicBrainzTrack` | OrdinalIgnoreCase | trimmed | none | A UUID, for the same reason as the album row. |
| _any provider with no row_ | Ordinal | trimmed | none | An identifier this table does not know is an opaque string. Comparing it case-insensitively assumes a shape nobody here has checked, and the fail-safe direction for an unknown identifier is to treat two spellings as two identifiers rather than one. |

## What a comparison answers

Three outcomes, and the third is not a weaker version of the first two.

`Match` means at least one provider is present on both sides and every provider
present on both sides agrees.

`Disagreement` means at least one provider is present on both sides and
disagrees.

`NoBasis` means no provider is present on both sides, so nothing was compared.
An item with an empty dictionary lands here. This is not a statement that two
items are different; it is a statement that these identifiers cannot decide, and
whatever reads this is owed that difference.

## Disagreement wins, and there is no first match

Every provider present on both sides is compared. One that disagrees decides the
whole comparison however many others agree.

This is stated as its own rule because the natural implementation is the wrong
one. A loop that returns as soon as two identifiers agree calls two items the
same on the strength of one provider while another provider sits in the same
dictionary saying they are not, and the outcome then depends on the order of a
dictionary nobody controls.

The asymmetry is deliberate. Two identifiers naming different works is evidence
that the items are different. Two naming the same work is not evidence against
that, because one of them can simply be wrong on one server.

## The fixture table

Every row here is a test. The expected outcome is read out of this document by
the suite rather than restated in code, so a row changed here and nowhere else
turns the suite red.

Identifiers are written `Provider=Value`, several separated by `;`, and an empty
cell is an empty dictionary.

The last column is what the row is for. A fixture that could never have failed
proves less than one that nearly did, and once both are green there is nothing
left to tell them apart except the sentence the row wrote down. It names the
implementation this row refuses rather than the behaviour it wants, because the
wanted behaviour is already the outcome column and writing it twice says nothing
new. The suite refuses an empty cell, a cell holding an outcome name where a
sentence belongs, and two rows naming one mistake.

| Case | This server | The peer | Outcome | The mistake it would catch |
| --- | --- | --- | --- | --- |
| a clean single-provider match | `Tmdb=550` | `Tmdb=550` | `Match` | A comparison that never answers a match at all, which leaves every pair of items undecided and resolves nothing anywhere. |
| agreement across two providers | `Tmdb=550;Imdb=tt0137523` | `Imdb=tt0137523;Tmdb=550` | `Match` | Pairing the two dictionaries by position rather than by provider name, so a TMDb number is held against an IMDb string as soon as one side writes them in a different order. |
| disagreement across two providers | `Tmdb=550;Imdb=tt0137523` | `Tmdb=550;Imdb=tt0111161` | `Disagreement` | Returning as soon as one provider agrees, so a second provider sitting in the same dictionary and saying the items are different is never read. |
| an identifier differing only in case, where the row says the identifier is opaque | `Anidb=Ab12` | `Anidb=aB12` | `Disagreement` | Comparing every identifier case-insensitively, which assumes a shape for a provider this table has never been shown. |
| an identifier differing only in case, where the row says case carries no value | `MusicBrainzAlbum=8F468C1B-1B4F-4E9C-9D5A-1E0BD1FCF9AA` | `MusicBrainzAlbum=8f468c1b-1b4f-4e9c-9d5a-1e0bd1fcf9aa` | `Match` | Comparing every identifier ordinally, so a UUID held in upper case on one server reads as a different work from the same UUID held in lower case on the other. |
| an identifier differing only in whitespace | `Tmdb= 550 ` | `Tmdb=550` | `Match` | Comparing the value exactly as it was stored, so a space around an identifier splits one work into two. |
| a provider name differing only in case | `tmdb=550` | `Tmdb=550` | `Match` | Looking the provider up by an ordinal dictionary key, so a provider version that wrote its own name in lower case reads as a provider present on one side only. |
| a numeric identifier zero-padded on one side | `Tvdb=0075978` | `Tvdb=75978` | `Match` | Comparing a decimal identifier as text, so zero padding written by one provider version reads as a different work. |
| a zero-padded identifier where the row declares no such normalisation | `Anidb=007` | `Anidb=7` | `Disagreement` | Dropping leading zeros from every value that is entirely digits, which repairs an identifier for a provider whose numbering nobody here has checked. |
| an item with an empty dictionary | | `Tmdb=550` | `NoBasis` | Answering a disagreement where nothing was compared, so an item nobody has run a metadata scan over becomes evidence that the two items are different. |
| both sides empty | | | `NoBasis` | Starting the comparison from agreement rather than from nothing, which matches two items that carry no identifiers at all. |
| no provider in common | `Imdb=tt0137523` | `Tmdb=550` | `NoBasis` | Reading a provider present on one side only as a disagreement, which reports two servers with different providers enabled as two different works. |
| one provider agrees and another is present on one side only | `Tmdb=550;Imdb=tt0137523` | `Tmdb=550` | `Match` | Requiring every provider here to be present on the peer as well, so one extra provider withholds the match the shared provider had already established. |

## What this table does not do

It does not decide identity. It compares identifiers, and what a `Match` is
worth against a library where two local items carry the same identifier is #31.

It does not repair an identifier. The only normalisation any row declares is
dropping leading zeros from a value that is entirely digits, and the two rows
that declare it say why. An IMDb value without its prefix is a different string
and stays one, because a table that repaired it would be guessing at what the
writer meant.

It does not read anything but the two dictionaries. No clock, no file, no
transport, so every row above is decidable with nothing running.
