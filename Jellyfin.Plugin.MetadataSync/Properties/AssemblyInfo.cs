using System.Runtime.CompilerServices;

// The field register's loader takes the resource name and the register text as
// parameters, so every refusal inside it is a line the suite can reach. Those
// seams are internal rather than public because they are a way in for a proof
// and not an API for anybody else to call.
[assembly: InternalsVisibleTo("Jellyfin.Plugin.MetadataSync.Tests")]
