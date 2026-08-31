using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Jellyfin.Plugin.MetadataSync.Configuration;
using Jellyfin.Plugin.MetadataSync.Conflicts;
using Jellyfin.Plugin.MetadataSync.Fields;
using Jellyfin.Plugin.MetadataSync.Matching;
using Jellyfin.Plugin.MetadataSync.Reconciliation;
using Jellyfin.Plugin.MetadataSync.References;
using Jellyfin.Plugin.MetadataSync.Store;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Model.Entities;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Every place the plugin refuses is named here, against the test that trips it
/// and the neighbour that differs by one thing and is not refused. A refusal
/// nobody trips has never been seen to happen, and it is indistinguishable from
/// a line that cannot be reached at all.
/// </summary>
/// <remarks>
/// The behavioural tests this register names live beside the type they are
/// about, and a second copy of an assertion is a copy that drifts. What is
/// added here is the register, which is the part that keeps working after it is
/// written: a guard added to the plugin later with no entry fails the suite
/// rather than waiting for somebody to notice it in review, and a guard deleted
/// while its entry stays fails it the same way. Both directions, because a
/// register that only grows stops describing the tree the moment a guard moves.
/// <para>
/// Each entry also carries the arrangement that reaches its site, and the
/// register runs it. That is not a copy of the named test's assertion: the
/// named test asserts what the caller sees, and the register asserts where the
/// refusal came from, which is the thing naming a test cannot establish.
/// </para>
/// </remarks>
public class RefusalTests
{
    /// <summary>
    /// One entry per refusal site the plugin carries. The key is the file the
    /// site is in and the line of code that refuses, so an edit to the guard
    /// itself shows up here rather than passing under a line number that
    /// happens to still match.
    /// </summary>
    private static readonly Dictionary<string, (string TestClass, string Trip, string Neighbour, string DiffersBy, Action Reaches)> Register =
        new(StringComparer.Ordinal)
        {
            ["Configuration/PluginConfigurationProvider.cs -> ArgumentNullException.ThrowIfNull(read);"] =
                (nameof(ServiceRegistrationTests),
                 nameof(ServiceRegistrationTests.ConfigurationProviderRefusesAMissingReader),
                 nameof(ServiceRegistrationTests.ConfigurationProviderReturnsTheConfigurationItWasGiven),
                 "the delegate is there",
                 () => new PluginConfigurationProvider(null!, () => Array.Empty<Guid>())),

            ["Configuration/PluginConfigurationProvider.cs -> ArgumentNullException.ThrowIfNull(readLibrariesTheServerHolds);"] =
                (nameof(ServiceRegistrationTests),
                 nameof(ServiceRegistrationTests.ConfigurationProviderRefusesAMissingLibraryReader),
                 nameof(ServiceRegistrationTests.ConfigurationProviderReturnsTheConfigurationItWasGiven),
                 "the delegate answering which libraries the server holds is there",
                 () => new PluginConfigurationProvider(() => new PluginConfiguration(), null!)),

            ["Configuration/PluginConfigurationProvider.cs -> throw new ConfigurationRefusedException(problems);"] =
                (nameof(ConfigurationLoadTests),
                 nameof(ConfigurationLoadTests.AConfigurationThatCannotBeActedOnIsRefusedWhenItIsAskedFor),
                 nameof(ConfigurationLoadTests.AConfigurationThatCanBeActedOnIsHandedOver),
                 "the library the configuration names is one the server holds",
                 () => ProviderNamingALibraryTheServerDoesNotHold().Require()),

            ["Configuration/ConfigurationRefusedException.cs -> ArgumentNullException.ThrowIfNull(problems);"] =
                (nameof(ConfigurationLoadTests),
                 nameof(ConfigurationLoadTests.ARefusalBuiltFromNoProblemsAtAllIsRefused),
                 nameof(ConfigurationLoadTests.TheRefusalCarriesEveryReasonRatherThanTheFirst),
                 "the list of reasons the refusal is built from is there",
                 () => new ConfigurationRefusedException(null!)),

            ["Configuration/ServerLibraries.cs -> ArgumentNullException.ThrowIfNull(library);"] =
                (nameof(ConfigurationLoadTests),
                 nameof(ConfigurationLoadTests.ReadingTheLibrariesWithNoServerIsRefused),
                 nameof(ConfigurationLoadTests.TheRangeIsTheLibrariesTheServerLists),
                 "the server whose libraries are read is there",
                 () => ServerLibraries.Held(null!)),

            ["Configuration/ConfigurationValidation.cs -> ArgumentNullException.ThrowIfNull(configuration);"] =
                (nameof(ConfigurationValidationTests),
                 nameof(ConfigurationValidationTests.ValidatingAConfigurationThatIsNotThereIsRefused),
                 nameof(ConfigurationValidationTests.ADefaultConfigurationHasNothingToRefuse),
                 "the configuration being read is there",
                 () => ConfigurationValidation.Validate(null!, Array.Empty<Guid>())),

            ["Configuration/ConfigurationValidation.cs -> ArgumentNullException.ThrowIfNull(librariesTheServerHolds);"] =
                (nameof(ConfigurationValidationTests),
                 nameof(ConfigurationValidationTests.ValidatingAgainstALibrarySetThatIsNotThereIsRefused),
                 nameof(ConfigurationValidationTests.ADefaultConfigurationHasNothingToRefuse),
                 "the set the libraries are checked against is there, even when it is empty",
                 () => ConfigurationValidation.Validate(new PluginConfiguration(), null!)),

            ["Fields/FieldRegister.cs -> throw new InvalidOperationException(NoSuchLock(row));"] =
                (nameof(FieldRegisterTests),
                 nameof(FieldRegisterTests.ARowNamingALockTheServerDoesNotHaveIsRefused),
                 nameof(FieldRegisterTests.TheRegisterThatShipsInTheAssemblyLoads),
                 "the lock the row names is one the server has",
                 () => FieldRegister.Parse(FieldRegisterTests.LockTheServerDoesNotHaveRegister)),

            ["Fields/FieldRegister.cs -> throw new FieldNotDeclaredException(NoRowAtAll(field));"] =
                (nameof(FieldRegisterTests),
                 nameof(FieldRegisterTests.AFieldWithNoRowIsRefusedWhenSomethingAsksToMoveIt),
                 nameof(FieldRegisterTests.ADeclaredFieldThatMovesIsAnsweredWithItsOwnRow),
                 "the field has a row",
                 () => FieldRegister.RequireMovable("SortName")),

            ["Fields/FieldRegister.cs -> throw new FieldNotDeclaredException(ARowThatRefuses(row));"] =
                (nameof(FieldRegisterTests),
                 nameof(FieldRegisterTests.AFieldWhoseRowRefusesToMoveIsRefusedWithItsReason),
                 nameof(FieldRegisterTests.ADeclaredFieldThatMovesIsAnsweredWithItsOwnRow),
                 "the row the field has says it moves",
                 () => FieldRegister.RequireMovable("RunTimeTicks")),

            ["Fields/FieldRegister.cs -> using var stream = assembly.GetManifestResourceStream(resourceName) ?? throw new InvalidOperationException(NoRegisterEmbedded(resourceName));"] =
                (nameof(FieldRegisterTests),
                 nameof(FieldRegisterTests.ARegisterThatIsNotEmbeddedIsRefused),
                 nameof(FieldRegisterTests.TheRegisterThatShipsInTheAssemblyLoads),
                 "the assembly carries a register under that name",
                 () => FieldRegister.Load("Jellyfin.Plugin.MetadataSync.Fields.no-such-register.json")),

            ["Fields/FieldRegister.cs -> var read = JsonSerializer.Deserialize<RegisterFile>(text, _json) ?? throw new InvalidOperationException(NothingToRead());"] =
                (nameof(FieldRegisterTests),
                 nameof(FieldRegisterTests.RegisterTextThatDescribesNoRegisterIsRefused),
                 nameof(FieldRegisterTests.TheRegisterThatShipsInTheAssemblyLoads),
                 "the text describes a register",
                 () => FieldRegister.Parse("null")),

            ["Fields/FieldRegister.cs -> throw new InvalidOperationException(NoSuchKindGroup(row));"] =
                (nameof(FieldRegisterTests),
                 nameof(FieldRegisterTests.ARowNamingAKindGroupNothingDeclaresIsRefused),
                 nameof(FieldRegisterTests.TheRegisterThatShipsInTheAssemblyLoads),
                 "the group the row names is one the register declares",
                 () => FieldRegister.Parse(FieldRegisterTests.UndeclaredKindGroupRegister)),

            ["Matching/Candidate.cs -> ArgumentException.ThrowIfNullOrWhiteSpace(id);"] =
                (nameof(CandidateResolutionTests),
                 nameof(CandidateResolutionTests.ACandidateWithNoIdentityIsRefused),
                 nameof(CandidateResolutionTests.ExactlyOneCandidateNamingTheWorkResolves),
                 "the candidate has an identity an ambiguity could name it by",
                 () => new Candidate(" ", new Dictionary<string, string>(StringComparer.Ordinal))),

            ["Matching/Candidate.cs -> ArgumentNullException.ThrowIfNull(identifiers);"] =
                (nameof(CandidateResolutionTests),
                 nameof(CandidateResolutionTests.ACandidateWithNoIdentifiersIsRefused),
                 nameof(CandidateResolutionTests.ExactlyOneCandidateNamingTheWorkResolves),
                 "the candidate's identifier dictionary is there",
                 () => new Candidate("here:1", null!)),

            ["Matching/CandidateResolver.cs -> ArgumentNullException.ThrowIfNull(identifiers);"] =
                (nameof(CandidateResolutionTests),
                 nameof(CandidateResolutionTests.ResolvingAgainstIdentifiersThatAreNotThereIsRefused),
                 nameof(CandidateResolutionTests.AWorkWithNoIdentifiersOfItsOwnResolvesNothing),
                 "the work's identifier dictionary is there, even when it is empty",
                 () => CandidateResolver.Resolve(null!, Array.Empty<Candidate>())),

            ["Matching/CandidateResolver.cs -> ArgumentNullException.ThrowIfNull(candidates);"] =
                (nameof(CandidateResolutionTests),
                 nameof(CandidateResolutionTests.ResolvingCandidatesThatAreNotThereIsRefused),
                 nameof(CandidateResolutionTests.NoCandidateOfferedIsNotTheSameAsNoCandidateMatching),
                 "the candidate set is there, even when it is empty",
                 () => CandidateResolver.Resolve(new Dictionary<string, string>(StringComparer.Ordinal), null!)),

            ["Matching/ProviderIdentifiers.cs -> ArgumentNullException.ThrowIfNull(local);"] =
                (nameof(ProviderIdentifierTests),
                 nameof(ProviderIdentifierTests.ComparingWithNoLocalDictionaryIsRefused),
                 nameof(ProviderIdentifierTests.TwoDictionariesThatAreThereAreCompared),
                 "this server's identifiers are there",
                 () => ProviderIdentifiers.Compare(null!, new Dictionary<string, string>(StringComparer.Ordinal))),

            ["Matching/ProviderIdentifiers.cs -> ArgumentNullException.ThrowIfNull(peer);"] =
                (nameof(ProviderIdentifierTests),
                 nameof(ProviderIdentifierTests.ComparingWithNoPeerDictionaryIsRefused),
                 nameof(ProviderIdentifierTests.TwoDictionariesThatAreThereAreCompared),
                 "the peer's identifiers are there",
                 () => ProviderIdentifiers.Compare(new Dictionary<string, string>(StringComparer.Ordinal), null!)),

            ["Matching/ProviderIdentifiers.cs -> using var stream = assembly.GetManifestResourceStream(resourceName) ?? throw new InvalidOperationException(NoTableEmbedded(resourceName));"] =
                (nameof(ProviderIdentifierTests),
                 nameof(ProviderIdentifierTests.ATableThatIsNotEmbeddedIsRefused),
                 nameof(ProviderIdentifierTests.TheTableThatShipsInTheAssemblyLoads),
                 "the assembly carries a table under that name",
                 () => ProviderIdentifiers.Load("Jellyfin.Plugin.MetadataSync.Matching.no-such-table.json")),

            ["Matching/ProviderIdentifiers.cs -> var read = JsonSerializer.Deserialize<TableFile>(text, _json) ?? throw new InvalidOperationException(NothingToRead());"] =
                (nameof(ProviderIdentifierTests),
                 nameof(ProviderIdentifierTests.TableTextThatDescribesNoTableIsRefused),
                 nameof(ProviderIdentifierTests.TheTableThatShipsInTheAssemblyLoads),
                 "the text describes a table",
                 () => ProviderIdentifiers.Parse("null")),

            ["Matching/OrdinalIdentity.cs -> ArgumentNullException.ThrowIfNull(parentIdentifiers);"] =
                (nameof(OrdinalResolutionTests),
                 nameof(OrdinalResolutionTests.AnIdentityWithNoParentIdentifierDictionaryIsRefused),
                 nameof(OrdinalResolutionTests.AnOrdinalIsSpelledTheWayTheDocumentSpellsIt),
                 "the parent's identifier dictionary is there",
                 () => new OrdinalIdentity(null!, 1, 5, null, null)),

            ["Matching/OrdinalResolver.cs -> ArgumentNullException.ThrowIfNull(here);"] =
                (nameof(OrdinalResolutionTests),
                 nameof(OrdinalResolutionTests.ResolvingWithNoItemIsRefused),
                 nameof(OrdinalResolutionTests.TheOnlyRemainingCandidateIsNotTaken),
                 "the item being resolved is there",
                 () => OrdinalResolver.Resolve(null!, Array.Empty<OrdinalIdentity>())),

            ["Matching/OrdinalResolver.cs -> ArgumentNullException.ThrowIfNull(there);"] =
                (nameof(OrdinalResolutionTests),
                 nameof(OrdinalResolutionTests.ResolvingWithNoCandidatesIsRefused),
                 nameof(OrdinalResolutionTests.TheOnlyRemainingCandidateIsNotTaken),
                 "the candidate set is there, even when it is empty",
                 () => OrdinalResolver.Resolve(
                     new OrdinalIdentity(new Dictionary<string, string>(StringComparer.Ordinal), 1, 5, null, null),
                     null!)),

            ["Matching/OrdinalResolver.cs -> ArgumentNullException.ThrowIfNull(identity);"] =
                (nameof(OrdinalResolutionTests),
                 nameof(OrdinalResolutionTests.SpellingNoItemIsRefused),
                 nameof(OrdinalResolutionTests.AnOrdinalIsSpelledTheWayTheDocumentSpellsIt),
                 "the item being spelled is there",
                 () => OrdinalResolver.Spelled(null!)),

            ["Matching/OrdinalResolver.cs -> _ => throw new ArgumentOutOfRangeException(nameof(verdict), verdict, NoStatement()),"] =
                (nameof(OrdinalResolutionTests),
                 nameof(OrdinalResolutionTests.AVerdictWithNoDeclaredSentenceIsRefused),
                 nameof(OrdinalResolutionTests.TheDocumentSaysExactlyWhatTheResolverSays),
                 "the verdict is one the plugin declares a sentence for",
                 () => OrdinalResolver.Statement((OrdinalVerdict)99)),

            ["Conflicts/ConflictRules.cs -> using var stream = assembly.GetManifestResourceStream(resourceName) ?? throw new InvalidOperationException(NoRulesEmbedded(resourceName));"] =
                (nameof(ConflictRuleTests),
                 nameof(ConflictRuleTests.ARuleTableThatIsNotEmbeddedIsRefused),
                 nameof(ConflictRuleTests.TheRuleTableThatShipsInTheAssemblyLoads),
                 "the assembly carries a rule table under that name",
                 () => ConflictRules.Load("Jellyfin.Plugin.MetadataSync.Conflicts.no-such-rules.json")),

            ["Conflicts/ConflictRules.cs -> var read = JsonSerializer.Deserialize<RuleFile>(text, _json) ?? throw new InvalidOperationException(NothingToRead());"] =
                (nameof(ConflictRuleTests),
                 nameof(ConflictRuleTests.RuleTextThatDescribesNoRuleSetIsRefused),
                 nameof(ConflictRuleTests.TheRuleTableThatShipsInTheAssemblyLoads),
                 "the text describes a rule set",
                 () => ConflictRules.Parse("null")),

            ["Conflicts/ConflictRules.cs -> throw new InvalidOperationException(NoSuchOutcome(row));"] =
                (nameof(ConflictRuleTests),
                 nameof(ConflictRuleTests.ARuleProducingAnOutcomeNothingDeclaresIsRefused),
                 nameof(ConflictRuleTests.TheRuleTableThatShipsInTheAssemblyLoads),
                 "the outcome the rule produces is one the closed set carries",
                 () => ConflictRules.Parse(ConflictRuleTests.UndeclaredOutcomeRules)),

            ["Conflicts/ConflictRules.cs -> throw new InvalidOperationException(NotArgued(row));"] =
                (nameof(ConflictRuleTests),
                 nameof(ConflictRuleTests.ARuleWithNoReasonIsRefused),
                 nameof(ConflictRuleTests.TheRuleTableThatShipsInTheAssemblyLoads),
                 "the rule says why it is right as well as when it fires",
                 () => ConflictRules.Parse(ConflictRuleTests.UnarguedRules)),

            ["Conflicts/ConflictRules.cs -> throw new InvalidOperationException(TwoRulesUnderOneName(row));"] =
                (nameof(ConflictRuleTests),
                 nameof(ConflictRuleTests.TwoRulesUnderOneNameAreRefused),
                 nameof(ConflictRuleTests.TheRuleTableThatShipsInTheAssemblyLoads),
                 "each rule carries its own name",
                 () => ConflictRules.Parse(ConflictRuleTests.DuplicateNameRules)),

            ["Conflicts/ConflictResolver.cs -> ArgumentNullException.ThrowIfNull(rules);"] =
                (nameof(ConflictResolverTests),
                 nameof(ConflictResolverTests.NoRuleSetIsRefused),
                 nameof(ConflictResolverTests.TheEvaluationOrderComesFromTheDeclaredTable),
                 "the rule set to walk is there",
                 () => ConflictResolver.Resolve(ConflictResolverTests.NothingOnEitherSide(), null!)),

            ["Conflicts/ConflictResolver.cs -> throw new InvalidOperationException(NoConditionFor(rule));"] =
                (nameof(ConflictResolverTests),
                 nameof(ConflictResolverTests.ARuleWithNoConditionIsRefused),
                 nameof(ConflictResolverTests.TheEvaluationOrderComesFromTheDeclaredTable),
                 "every rule in the set has a condition behind it",
                 () => ConflictResolver.Resolve(
                     ConflictResolverTests.NothingOnEitherSide(),
                     ConflictRules.Parse(ConflictResolverTests.RuleWithNoConditionHere))),

            ["Conflicts/ConflictEntries.cs -> ArgumentNullException.ThrowIfNull(plan);"] =
                (nameof(ConflictEntryTests),
                 nameof(ConflictEntryTests.APassThatHandsOverNoPlanIsRefused),
                 nameof(ConflictEntryTests.APlanWithNothingToTellProducesNoRows),
                 "the plan to read the decisions out of is there",
                 () => ConflictEntries.From(null!, DateTimeOffset.UnixEpoch)),

            ["Conflicts/ConflictEntries.cs -> ArgumentNullException.ThrowIfNull(change);"] =
                (nameof(ConflictEntryTests),
                 nameof(ConflictEntryTests.ARowThatIsNotThereIsRefused),
                 nameof(ConflictEntryTests.AFieldBothServersAgreeOnOwesNoRow),
                 "the row being asked about is there",
                 () => ConflictEntries.IsOwed(null!)),

            ["Conflicts/ConflictGrouping.cs -> ArgumentNullException.ThrowIfNull(entries);"] =
                (nameof(ConflictGroupingTests),
                 nameof(ConflictGroupingTests.NoDecisionsAtAllIsRefused),
                 nameof(ConflictGroupingTests.AnAccountWithNothingToTellHasNoLines),
                 "the decisions to read the lines out of are there",
                 () => ConflictGrouping.Of(null!)),

            ["Conflicts/ConflictGrouping.cs -> ArgumentNullException.ThrowIfNull(entry);"] =
                (nameof(ConflictGroupingTests),
                 nameof(ConflictGroupingTests.ADecisionThatIsNotThereIsRefused),
                 nameof(ConflictGroupingTests.DecisionsOneRuleTookToOneEndAreOneLine),
                 "the decision being filed under a line is there",
                 () => ConflictGrouping.Of(new ConflictEntry[] { null! })),

            ["References/ReferenceResolver.cs -> ArgumentNullException.ThrowIfNull(incoming);"] =
                (nameof(ReferenceResolutionTests),
                 nameof(ReferenceResolutionTests.ResolvingAReferenceThatIsNotThereIsRefused),
                 nameof(ReferenceResolutionTests.AGenreThisServerAlreadyHoldsResolvesToIt),
                 "the reference to resolve is there",
                 () => ReferenceResolver.ResolveGenre(null!, Array.Empty<string>())),

            ["References/ReferenceResolver.cs -> ArgumentNullException.ThrowIfNull(here);"] =
                (nameof(ReferenceResolutionTests),
                 nameof(ReferenceResolutionTests.ResolvingAgainstEntriesThatAreNotThereIsRefused),
                 nameof(ReferenceResolutionTests.AGenreThisServerAlreadyHoldsResolvesToIt),
                 "the entries to resolve against are there",
                 () => ReferenceResolver.ResolveGenre("Comedy", null!)),

            ["References/ReferenceResolver.cs -> using var stream = assembly.GetManifestResourceStream(resourceName) ?? throw new InvalidOperationException(NoTableEmbedded(resourceName));"] =
                (nameof(ReferenceResolutionTests),
                 nameof(ReferenceResolutionTests.ATableThatIsNotEmbeddedIsRefused),
                 nameof(ReferenceResolutionTests.TheTableThatShipsInTheAssemblyLoads),
                 "the assembly carries a table under that name",
                 () => ReferenceResolver.Load("Jellyfin.Plugin.MetadataSync.References.no-such-table.json")),

            ["References/ReferenceResolver.cs -> var read = JsonSerializer.Deserialize<TableFile>(text, _json) ?? throw new InvalidOperationException(NothingToRead());"] =
                (nameof(ReferenceResolutionTests),
                 nameof(ReferenceResolutionTests.TableTextThatDescribesNoTableIsRefused),
                 nameof(ReferenceResolutionTests.TheTableThatShipsInTheAssemblyLoads),
                 "the text describes a table",
                 () => ReferenceResolver.Parse("null")),

            ["References/ReferenceResolver.cs -> throw new InvalidOperationException(NoSuchKind(row));"] =
                (nameof(ReferenceResolutionTests),
                 nameof(ReferenceResolutionTests.ARowNamingAKindThisPluginDoesNotResolveIsRefused),
                 nameof(ReferenceResolutionTests.TheTableThatShipsInTheAssemblyLoads),
                 "the kind the row answers for is one this plugin resolves",
                 () => ReferenceResolver.Parse(ReferenceResolutionTests.UnknownKindTable)),

            ["Reconciliation/Planner.cs -> ArgumentNullException.ThrowIfNull(request);"] =
                (nameof(PlannerTests),
                 nameof(PlannerTests.PlanningFromARequestThatIsNotThereIsRefused),
                 nameof(PlannerTests.AnEmptyRequestIsAnEmptyPlan),
                 "the request is there, even when it asks for nothing",
                 () => Planner.Plan(null!)),

            ["Reconciliation/Applier.cs -> ArgumentNullException.ThrowIfNull(target);"] =
                (nameof(ApplierTests),
                 nameof(ApplierTests.AnApplierWithNoTargetIsRefused),
                 nameof(ApplierTests.AnEmptyPlanReachesTheLibraryNotAtAll),
                 "the route to the library is there",
                 () => new Applier(null!, new RecordingWrittenValues())),

            ["Reconciliation/Applier.cs -> ArgumentNullException.ThrowIfNull(written);"] =
                (nameof(WrittenValuesTests),
                 nameof(WrittenValuesTests.AnApplierWithNowhereToRecordIsRefused),
                 nameof(WrittenValuesTests.ARowThePlanDecidedAgainstIsNotRecorded),
                 "there is somewhere to record what was written",
                 () => new Applier(new RecordingPlanTarget(), null!)),

            ["Store/PairingStores.cs -> ArgumentNullException.ThrowIfNull(stores);"] =
                (nameof(PairingStoresTests),
                 nameof(PairingStoresTests.NoStoresIsAnAnswerAndNoSetOfStoresIsARefusal),
                 nameof(PairingStoresTests.EveryStoreContributesToTheReportIncludingTheOnesHoldingNothing),
                 "the set of stores is there, even where it is empty",
                 () => new PairingStores(null!)),

            ["Store/PairingStores.cs -> throw new StoreRemovedADifferentNumberException(removing.Holdings[n].Store, removing.Holdings[n].Count, removed);"] =
                (nameof(PairingStoresTests),
                 nameof(PairingStoresTests.AStoreThatRemovesADifferentNumberThanItReportedIsRefused),
                 nameof(PairingStoresTests.RemovalDeletesOnePairingAndLeavesTheOtherWhereItWas),
                 "the store removed the number of rows it had just reported holding",
                 () => new PairingStores(new IPairingStore[] { AStoreThatDisagreesWithItself() }).Remove(Guid.Empty)),

            ["Store/StoreFormat.cs -> ArgumentException.ThrowIfNullOrWhiteSpace(directory);"] =
                (nameof(StoreFormatTests),
                 nameof(StoreFormatTests.AStampWithNoDirectoryIsRefused),
                 nameof(StoreFormatTests.ADirectoryWithNoStampIsTheEarliestFormat),
                 "the directory the stamp is read from is there",
                 () => new StoreFormat(null!)),

            ["Store/StoreFormat.cs -> throw new StoreFormatRefusedException(_path, \"no store format this build can read\", _current);"] =
                (nameof(StoreFormatTests),
                 nameof(StoreFormatTests.AStampThatCannotBeReadIsRefused),
                 nameof(StoreFormatTests.ADirectoryWithNoStampIsTheEarliestFormat),
                 "the stamp says a format rather than something unreadable",
                 () => FormatOverAStampSaying("this is not a stamp").Declared()),

            ["Store/StoreFormat.cs -> throw new StoreFormatRefusedException(_path, Says(stamp.Format), _current);"] =
                (nameof(StoreFormatTests),
                 nameof(StoreFormatTests.AFormatFromTheFutureIsRefused),
                 nameof(StoreFormatTests.ADirectoryThisPluginHasWrittenToSaysWhichFormatItIsIn),
                 "the format the stamp declares is one this build writes",
                 () => FormatOverAStampSaying("{\"format\":" + (StoreFormat.Current + 1) + "}").Declared()),

            ["Store/StoreFormat.cs -> ArgumentNullException.ThrowIfNull(chain);"] =
                (nameof(StoreMigrationTests),
                 nameof(StoreMigrationTests.AMigrationWithNoChainIsRefused),
                 nameof(StoreMigrationTests.TheStoreThisBuildReadsHasNothingToStepForward),
                 "the chain of steps is there",
                 () => new StoreFormat("directory", 1, null!)),

            ["Store/StoreFormat.cs -> ArgumentOutOfRangeException.ThrowIfLessThan(current, Earliest);"] =
                (nameof(StoreMigrationTests),
                 nameof(StoreMigrationTests.AFormatBelowTheEarliestOneIsNotAFormatToRead),
                 nameof(StoreMigrationTests.TheStoreThisBuildReadsHasNothingToStepForward),
                 "the format being read is one that has existed",
                 () => new StoreFormat("directory", StoreFormat.Earliest - 1, Array.Empty<FormatStep>())),

            ["Store/StoreFormat.cs -> throw new StoreFormatRefusedException(_path, Unstepped(declared, format, reaching.Count), _current);"] =
                (nameof(StoreMigrationTests),
                 nameof(StoreMigrationTests.AFormatNoStepStartsFromIsRefusedBeforeAnythingIsCopied),
                 nameof(StoreMigrationTests.ADirectoryStepsForwardOneFormatAtATimeAndTheStampFollows),
                 "a step starts from every format the directory has to pass through",
                 () => MigrationOverAChainMissingItsSecondStep().Migrate()),

            ["Store/FormatStep.cs -> ArgumentNullException.ThrowIfNull(apply);"] =
                (nameof(StoreMigrationTests),
                 nameof(StoreMigrationTests.AStepWithNoChangeToMakeIsRefused),
                 nameof(StoreMigrationTests.AStepMovesADirectoryByExactlyOneFormat),
                 "the change the step makes is there",
                 () => new FormatStep(1, null!)),

            ["Store/FormatStep.cs -> ArgumentOutOfRangeException.ThrowIfLessThan(from, StoreFormat.Earliest);"] =
                (nameof(StoreMigrationTests),
                 nameof(StoreMigrationTests.AStepStartingBeforeAnyFormatThatHasExistedIsRefused),
                 nameof(StoreMigrationTests.AStepMovesADirectoryByExactlyOneFormat),
                 "the step starts from a format that has existed",
                 () => new FormatStep(StoreFormat.Earliest - 1, _ => { })),

            ["Store/WrittenValues.cs -> ArgumentException.ThrowIfNullOrWhiteSpace(directory);"] =
                (nameof(WrittenValuesTests),
                 nameof(WrittenValuesTests.AStoreWithNoDirectoryIsRefused),
                 nameof(WrittenValuesTests.AFieldThatWasNeverWrittenHasNoRecord),
                 "the directory the store keeps itself in is there",
                 () => new WrittenValues(null!)),

            ["Store/WrittenValues.cs -> ArgumentException.ThrowIfNullOrWhiteSpace(field);"] =
                (nameof(WrittenValuesTests),
                 nameof(WrittenValuesTests.AFieldWithNoNameIsRefusedRatherThanAnswered),
                 nameof(WrittenValuesTests.EachPairingItemAndFieldIsItsOwnRecord),
                 "the field has a name",
                 () => StoreInATemporaryDirectory().Record(Guid.Empty, Guid.Empty, " ", "a value", null)),

            ["Store/ConflictLog.cs -> ArgumentException.ThrowIfNullOrWhiteSpace(directory);"] =
                (nameof(ConflictLogTests),
                 nameof(ConflictLogTests.ALogWithNoDirectoryIsRefused),
                 nameof(ConflictLogTests.APairingNothingWasDecidedForHasNoAccount),
                 "the directory the log keeps itself in is there",
                 () => new ConflictLog(null!)),

            ["Store/ConflictLog.cs -> ArgumentNullException.ThrowIfNull(entry);"] =
                (nameof(ConflictLogTests),
                 nameof(ConflictLogTests.ADecisionThatIsNotThereIsRefused),
                 nameof(ConflictLogTests.ADecisionIsReadBackByASecondInstance),
                 "the decision to keep is there",
                 () => LogInATemporaryDirectory().Record(Guid.Empty, null!)),

            ["Store/ConflictExport.cs -> ArgumentNullException.ThrowIfNull(account);"] =
                (nameof(ConflictExportTests),
                 nameof(ConflictExportTests.AnAccountThatIsNotThereIsRefused),
                 nameof(ConflictExportTests.APairingNothingWasDecidedForStillExportsAnAccount),
                 "the account to write out is there",
                 () => ConflictExport.Written(null!)),

            ["Store/ConflictExport.cs -> ArgumentException.ThrowIfNullOrWhiteSpace(text);"] =
                (nameof(ConflictExportTests),
                 nameof(ConflictExportTests.ATextThatIsNotThereIsRefused),
                 nameof(ConflictExportTests.AnAccountSurvivesBeingWrittenOutAndReadBack),
                 "the text to read is there",
                 () => ConflictExport.Read("   ")),

            ["Store/ConflictExport.cs -> throw new JsonException(\"The text names nothing, so it is not an account.\");"] =
                (nameof(ConflictExportTests),
                 nameof(ConflictExportTests.ATextThatIsNotAnAccountIsRefused),
                 nameof(ConflictExportTests.AnAccountSurvivesBeingWrittenOutAndReadBack),
                 "the text names an account rather than nothing",
                 () => ConflictExport.Read("null")),

            ["Reconciliation/Applier.cs -> ArgumentNullException.ThrowIfNull(plan);"] =
                (nameof(ApplierTests),
                 nameof(ApplierTests.ApplyingAPlanThatIsNotThereIsRefused),
                 nameof(ApplierTests.AnEmptyPlanReachesTheLibraryNotAtAll),
                 "the plan is there, even when it writes nothing",
                 () => _ = new Applier(new RecordingPlanTarget(), new RecordingWrittenValues()).ApplyAsync(null!, CancellationToken.None)),

            ["Reconciliation/Applier.cs -> cancellationToken.ThrowIfCancellationRequested();"] =
                (nameof(ApplierTests),
                 nameof(ApplierTests.ACancelledPassStopsWithinOneItem),
                 nameof(ApplierTests.AnItemThatWritesIsHandedToTheTargetExactlyOnce),
                 "the operator has not asked the pass to stop",
                 () => new Applier(new RecordingPlanTarget(), new RecordingWrittenValues())
                     .ApplyAsync(OneItemThatWrites(), new CancellationToken(canceled: true))
                     .GetAwaiter()
                     .GetResult()),

            ["Reconciliation/Revert.cs -> ArgumentNullException.ThrowIfNull(request);"] =
                (nameof(RevertTests),
                 nameof(RevertTests.ARevertWithNoRequestIsRefused),
                 nameof(RevertTests.AFieldThisPluginWroteGoesBackToWhatWasThereBefore),
                 "there is something to decide the revert from",
                 () => Revert.Plan(null!, new RecordingWrittenValues())),

            ["Reconciliation/Revert.cs -> ArgumentNullException.ThrowIfNull(written);"] =
                (nameof(RevertTests),
                 nameof(RevertTests.ARevertWithNoRecordIsRefused),
                 nameof(RevertTests.AFieldThisPluginWroteGoesBackToWhatWasThereBefore),
                 "there is a record to prove what this plugin wrote",
                 () => Revert.Plan(new RevertRequest(), null!)),

            ["Reconciliation/DryRun.cs -> ArgumentNullException.ThrowIfNull(request);"] =
                (nameof(DryRunTests),
                 nameof(DryRunTests.ADryRunFromARequestThatIsNotThereIsRefused),
                 nameof(DryRunTests.ADryRunOverAnEmptyRequestIsAnEmptyPlan),
                 "there is something to plan from",
                 () => DryRun.Of(null!, new RecordingPassProgress())),

            ["Reconciliation/DryRun.cs -> ArgumentNullException.ThrowIfNull(progress);"] =
                (nameof(DryRunTests),
                 nameof(DryRunTests.ADryRunWithNowhereToReadProgressIsRefused),
                 nameof(DryRunTests.ADryRunOverAnEmptyRequestIsAnEmptyPlan),
                 "there is somewhere to read how far an earlier pass got",
                 () => DryRun.Of(new PlanRequest(), null!)),

            ["Reconciliation/Pass.cs -> ArgumentNullException.ThrowIfNull(applier);"] =
                (nameof(PassResumptionTests),
                 nameof(PassResumptionTests.APassWithNoApplierIsRefused),
                 nameof(PassResumptionTests.AnOrdinaryPassWritesEveryItemAndRecordsEachOne),
                 "the half of the pass that writes is there",
                 () => new Pass(null!, new RecordingPassProgress(), TimeProvider.System, PassClock.NotReached)),

            ["Reconciliation/Pass.cs -> ArgumentNullException.ThrowIfNull(progress);"] =
                (nameof(PassResumptionTests),
                 nameof(PassResumptionTests.APassWithNowhereToRecordProgressIsRefused),
                 nameof(PassResumptionTests.AnOrdinaryPassWritesEveryItemAndRecordsEachOne),
                 "there is somewhere to record how far the pass got",
                 () => new Pass(new Applier(new RecordingPlanTarget(), new RecordingWrittenValues()), null!, TimeProvider.System, PassClock.NotReached)),

            ["Reconciliation/Pass.cs -> ArgumentNullException.ThrowIfNull(time);"] =
                (nameof(PassTimeBoundTests),
                 nameof(PassTimeBoundTests.APassWithNoClockIsRefused),
                 nameof(PassTimeBoundTests.AFinishedPassStillClearsItsResumePoint),
                 "there is a clock to measure the pass against",
                 () => new Pass(new Applier(new RecordingPlanTarget(), new RecordingWrittenValues()), new RecordingPassProgress(), null!, PassClock.NotReached)),

            ["Reconciliation/Pass.cs -> ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(limit, TimeSpan.Zero);"] =
                (nameof(PassTimeBoundTests),
                 nameof(PassTimeBoundTests.APassAllowedNoTimeIsRefused),
                 nameof(PassTimeBoundTests.TheSmallestBoundTheRangeAdmitsIsAccepted),
                 "the pass is allowed some time to run in",
                 () => new Pass(new Applier(new RecordingPlanTarget(), new RecordingWrittenValues()), new RecordingPassProgress(), TimeProvider.System, TimeSpan.Zero)),

            ["Reconciliation/Pass.cs -> ArgumentNullException.ThrowIfNull(request);"] =
                (nameof(PassResumptionTests),
                 nameof(PassResumptionTests.RunningAPassWithNoRequestIsRefused),
                 nameof(PassResumptionTests.AnOrdinaryPassWritesEveryItemAndRecordsEachOne),
                 "there is something to run the pass over",
                 () => _ = new Pass(new Applier(new RecordingPlanTarget(), new RecordingWrittenValues()), new RecordingPassProgress(), TimeProvider.System, PassClock.NotReached)
                     .RunAsync(null!, CancellationToken.None)),

            ["Reconciliation/Pass.cs -> cancellationToken.ThrowIfCancellationRequested();"] =
                (nameof(PassResumptionTests),
                 nameof(PassResumptionTests.ACancelledPassStopsBeforeTheItem),
                 nameof(PassResumptionTests.AnOrdinaryPassWritesEveryItemAndRecordsEachOne),
                 "the operator has not asked the pass to stop",
                 () => new Pass(new Applier(new RecordingPlanTarget(), new RecordingWrittenValues()), new RecordingPassProgress(), TimeProvider.System, PassClock.NotReached)
                     .RunAsync(OneItemToPlan(), new CancellationToken(canceled: true))
                     .GetAwaiter()
                     .GetResult()),

            ["Store/PassProgress.cs -> ArgumentException.ThrowIfNullOrWhiteSpace(directory);"] =
                (nameof(PassProgressTests),
                 nameof(PassProgressTests.AStoreWithNoDirectoryIsRefused),
                 nameof(PassProgressTests.APairingWithNoInterruptedPassHasNothingRecorded),
                 "the directory the record keeps itself in is there",
                 () => new PassProgress(null!)),

            ["Reconciliation/ItemReader.cs -> ArgumentNullException.ThrowIfNull(library);"] =
                (nameof(ParticipatingLibraryTests),
                 nameof(ParticipatingLibraryTests.AReaderRefusesAMissingLibrary),
                 nameof(ParticipatingLibraryTests.ANonParticipatingLibraryIsNeverEnumerated),
                 "the library to read from is there",
                 () => new ItemReader(null!, Array.Empty<Guid>(), PluginConfiguration.ItemsPerReadDefault)),

            ["Reconciliation/ItemReader.cs -> ArgumentNullException.ThrowIfNull(participating);"] =
                (nameof(ParticipatingLibraryTests),
                 nameof(ParticipatingLibraryTests.AReaderRefusesAMissingSet),
                 nameof(ParticipatingLibraryTests.NothingIsAskedOfTheLibraryWhenNoLibraryParticipates),
                 "the set of participating libraries is there, even where it is empty",
                 () => new ItemReader(LibraryItems.Empty().Library, null!, PluginConfiguration.ItemsPerReadDefault)),

            ["Reconciliation/ItemReader.cs -> ArgumentOutOfRangeException.ThrowIfLessThan(itemsPerRead, 1);"] =
                (nameof(BoundedReadTests),
                 nameof(BoundedReadTests.AReaderRefusesAPageSmallerThanOneItem),
                 nameof(BoundedReadTests.APageOfOneItemIsARead),
                 "the page is one item rather than none",
                 () => new ItemReader(LibraryItems.Empty().Library, Array.Empty<Guid>(), 0)),

            ["Reconciliation/LibraryPlanTarget.cs -> ArgumentNullException.ThrowIfNull(library);"] =
                (nameof(LibraryPlanTargetTests),
                 nameof(LibraryPlanTargetTests.ATargetWithNoLibraryIsRefused),
                 nameof(LibraryPlanTargetTests.AWriteGoesThroughTheSupportedCallAndNothingElse),
                 "the library the write goes to is there",
                 () => new LibraryPlanTarget(null!)),

            ["Reconciliation/LibraryPlanTarget.cs -> ArgumentNullException.ThrowIfNull(item);"] =
                (nameof(LibraryPlanTargetTests),
                 nameof(LibraryPlanTargetTests.WritingAnItemPlanThatIsNotThereIsRefused),
                 nameof(LibraryPlanTargetTests.AWriteGoesThroughTheSupportedCallAndNothingElse),
                 "the item plan is there",
                 () => _ = TargetOverAnEmptyLibrary().WriteAsync(null!, CancellationToken.None)),

            ["Reconciliation/LibraryPlanTarget.cs -> var found = _library.GetItemById(item.LocalItemId) ?? throw new ItemNotInLibraryException(NoSuchItem(item.LocalItemId));"] =
                (nameof(LibraryPlanTargetTests),
                 nameof(LibraryPlanTargetTests.AnItemThatIsNotInTheLibraryIsRefused),
                 nameof(LibraryPlanTargetTests.AWriteGoesThroughTheSupportedCallAndNothingElse),
                 "the library still holds the item the plan is about",
                 () => Carried(TargetOverAnEmptyLibrary(), Writing("Name", "theirs"))),

            ["Reconciliation/LibraryPlanTarget.cs -> ArgumentNullException.ThrowIfNull(asHeldNow);"] =
                (nameof(LibraryPlanTargetTests),
                 nameof(LibraryPlanTargetTests.TakingATokenFromAnItemThatIsNotThereIsRefused),
                 nameof(LibraryPlanTargetTests.TheTokenChangesWhenTheItemIsSavedAndNotOtherwise),
                 "the item the token is taken from is there",
                 () => LibraryPlanTarget.StampOf(null!)),

            ["Reconciliation/LibraryPlanTarget.cs -> var planned = item.LastSavedWhenPlanned ?? throw new WriteRefusedException(NoStampToCompare(item.LocalItemId));"] =
                (nameof(LibraryPlanTargetTests),
                 nameof(LibraryPlanTargetTests.APlanThatCarriesNoTokenIsRefused),
                 nameof(LibraryPlanTargetTests.AWriteGoesThroughTheSupportedCallAndNothingElse),
                 "the plan carries the token it was made from",
                 () => Carried(TargetOverTheItem(), WithNoToken())),

            ["Reconciliation/LibraryPlanTarget.cs -> throw new ItemChangedSincePlannedException(SomethingElseWrote(item.LocalItemId));"] =
                (nameof(LibraryPlanTargetTests),
                 nameof(LibraryPlanTargetTests.AnItemSomethingElseWroteSinceThePlanIsDeferred),
                 nameof(LibraryPlanTargetTests.AWriteGoesThroughTheSupportedCallAndNothingElse),
                 "nothing wrote the item between the plan and the write",
                 () => Carried(TargetOverTheItemAsSomethingElseLeftIt(), Writing("Name", "theirs"))),

            ["Reconciliation/LibraryPlanTarget.cs -> throw new WriteRefusedException(ASetInOneString(change.Field));"] =
                (nameof(LibraryPlanTargetTests),
                 nameof(LibraryPlanTargetTests.AFieldThatCarriesASetIsRefused),
                 nameof(LibraryPlanTargetTests.AWriteGoesThroughTheSupportedCallAndNothingElse),
                 "the field is one the server holds as a single value rather than a set",
                 () => Carried(TargetOverTheItem(), Writing("Tags", "one, two"))),

            ["Reconciliation/LibraryPlanTarget.cs -> throw new WriteRefusedException(NoWriterFor(change.Field));"] =
                (nameof(LibraryPlanTargetTests),
                 nameof(LibraryPlanTargetTests.AFieldWithNoWriterIsRefused),
                 nameof(LibraryPlanTargetTests.AWriteGoesThroughTheSupportedCallAndNothingElse),
                 "the field is one the register declares as moving",
                 () => Carried(TargetOverTheItem(), Writing("SortName", "theirs"))),

            ["Reconciliation/LibraryPlanTarget.cs -> throw new WriteRefusedException(NotADate(field, value));"] =
                (nameof(LibraryPlanTargetTests),
                 nameof(LibraryPlanTargetTests.ADateInAnotherSpellingIsRefused),
                 nameof(LibraryPlanTargetTests.ADateIsReadInTheRoundTripSpelling),
                 "the date is written in the round-trip spelling",
                 () => Carried(TargetOverTheItem(), Writing("PremiereDate", "05/06/1979"))),

            ["Reconciliation/LibraryPlanTarget.cs -> throw new WriteRefusedException(NotAYear(value));"] =
                (nameof(LibraryPlanTargetTests),
                 nameof(LibraryPlanTargetTests.AYearThatIsNotANumberIsRefused),
                 nameof(LibraryPlanTargetTests.AYearThatIsANumberIsWritten),
                 "the year is a plain number",
                 () => Carried(TargetOverTheItem(), Writing("ProductionYear", "nineteen seventy nine"))),

            ["References/ReferenceResolver.cs -> throw new InvalidOperationException(NoSuchProperty(row));"] =
                (nameof(ReferenceResolutionTests),
                 nameof(ReferenceResolutionTests.ARowNamingAWayOfDifferingThatNothingDeclaresIsRefused),
                 nameof(ReferenceResolutionTests.TheTableThatShipsInTheAssemblyLoads),
                 "the way of differing the row answers for is one that is declared",
                 () => ReferenceResolver.Parse(ReferenceResolutionTests.UnknownPropertyTable)),

            ["References/ReferenceResolver.cs -> throw new InvalidOperationException(NoSuchAnswer(row));"] =
                (nameof(ReferenceResolutionTests),
                 nameof(ReferenceResolutionTests.ARowGivingAnAnswerOutsideTheClosedSetIsRefused),
                 nameof(ReferenceResolutionTests.TheTableThatShipsInTheAssemblyLoads),
                 "the answer the row gives is one the closed set carries",
                 () => ReferenceResolver.Parse(ReferenceResolutionTests.UnknownAnswerTable)),

            ["References/ReferenceResolver.cs -> throw new InvalidOperationException(NotArgued(row));"] =
                (nameof(ReferenceResolutionTests),
                 nameof(ReferenceResolutionTests.ARowWithNoReasonIsRefused),
                 nameof(ReferenceResolutionTests.TheTableThatShipsInTheAssemblyLoads),
                 "the row says why its answer is right as well as what it is",
                 () => ReferenceResolver.Parse(ReferenceResolutionTests.UnarguedTable)),

            ["References/ReferenceResolver.cs -> throw new InvalidOperationException(TwoRowsForOnePair(row));"] =
                (nameof(ReferenceResolutionTests),
                 nameof(ReferenceResolutionTests.TwoRowsForOnePairAreRefused),
                 nameof(ReferenceResolutionTests.TheTableThatShipsInTheAssemblyLoads),
                 "one row answers each pair of kind and difference",
                 () => ReferenceResolver.Parse(ReferenceResolutionTests.DoubleAnsweredTable)),

            ["References/ReferenceResolver.cs -> throw new InvalidOperationException(NoRowForPair(kind, property));"] =
                (nameof(ReferenceResolutionTests),
                 nameof(ReferenceResolutionTests.ATableLeavingAPairUnansweredIsRefused),
                 nameof(ReferenceResolutionTests.TheTableThatShipsInTheAssemblyLoads),
                 "every pair of kind and difference has a row",
                 () => ReferenceResolver.Parse(ReferenceResolutionTests.IncompleteTable)),
        };

    /// <summary>
    /// Every refusal site the scan finds in the plugin is named in the
    /// register. This is the leg that catches a guard added without a proof.
    /// </summary>
    [Fact]
    public void EveryRefusalSiteInThePluginIsInTheRegister()
    {
        var unregistered = RefusalSites()
            .Where(site => !Register.ContainsKey(site))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(unregistered);
    }

    /// <summary>
    /// Every entry in the register still has a site behind it. This is the leg
    /// that catches a guard deleted while its entry stayed, and it is what
    /// makes deleting a guard turn the suite red for the register as well as
    /// for the test that tripped it.
    /// </summary>
    [Fact]
    public void EveryRegisterEntryStillHasASiteBehindIt()
    {
        var sites = RefusalSites().ToHashSet(StringComparer.Ordinal);

        var dangling = Register.Keys
            .Where(site => !sites.Contains(site))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(dangling);
    }

    /// <summary>
    /// The trip and the neighbour each entry names are tests the suite runs,
    /// and they are two different tests.
    /// </summary>
    /// <remarks>
    /// What this does not do is prove that the named trip is the thing that
    /// reaches the site, or that the neighbour differs from it by the one thing
    /// the entry says. Both pairings are claims made when the entry is written.
    /// The site is reached by the entry's own arrangement, which
    /// <see cref="EveryRegisteredSiteIsObservedToRefuseAtItsOwnLine"/> runs, so
    /// what is left unproved here is the naming and not the reach. The bound is
    /// stated rather than left for a reader to discover.
    /// </remarks>
    [Fact]
    public void EveryRegisterEntryNamesTestsTheSuiteRuns()
    {
        foreach (var entry in Register.Values)
        {
            Assert.NotEqual(entry.Trip, entry.Neighbour);
            Assert.False(string.IsNullOrWhiteSpace(entry.DiffersBy));

            AssertIsAFact(entry.TestClass, entry.Trip);
            AssertIsAFact(entry.TestClass, entry.Neighbour);
        }
    }

    /// <summary>
    /// Every registered site is seen refusing, at the line the register names
    /// it by. This is the leg that separates a register from a proof: the three
    /// legs above are satisfied by an entry naming a line nothing ever reaches,
    /// and this one is not.
    /// </summary>
    /// <remarks>
    /// Reaching the line is not the property. The guard here is spelled
    /// <c>ArgumentNullException.ThrowIfNull</c>, and that line executes on the
    /// neighbour as much as on the trip, so a check that asked only which lines
    /// ran would pass on an arrangement that was never refused at all. What is
    /// asserted instead is where the refusal came from: the exception carries
    /// the frames it was thrown through, and the first of them inside the
    /// plugin is read back out of the source to give the same site string the
    /// scan produces.
    /// </remarks>
    [Fact]
    public void EveryRegisteredSiteIsObservedToRefuseAtItsOwnLine()
    {
        foreach (var (site, entry) in Register)
        {
            var refusal = Record.Exception(entry.Reaches);

            Assert.NotNull(refusal);
            Assert.Equal(site, SiteThatRefused(refusal));
        }
    }

    /// <summary>
    /// The scan looks at files. If it ever looks at none, both register legs
    /// pass for the wrong reason, so it says so instead.
    /// </summary>
    [Fact]
    public void TheScanActuallyReadsThePluginSources()
    {
        Assert.NotEmpty(PluginSourceFiles());
    }

    /// <summary>
    /// Returns the site string for the place inside the plugin that a refusal
    /// was thrown from, in the same spelling the scan produces.
    /// </summary>
    /// <remarks>
    /// The frames are read outermost-throw first, and the first one carrying a
    /// file inside the plugin is the answer: the helpers that throw on a
    /// caller's behalf live in the framework and carry no file at all, so the
    /// first file-bearing frame is the line somebody wrote. It is read out of
    /// the source rather than trusted from the entry, so a guard that moved to
    /// another line reports the line it moved to.
    /// </remarks>
    /// <summary>
    /// A store over a directory of this run's own, for the arrangements above
    /// that only need one to exist. The directory is left behind deliberately:
    /// it is under the temporary path, nothing here writes to it, and a delete
    /// in a register entry would be the register doing work of its own.
    /// </summary>
    /// <returns>The store.</returns>
    private static StoreFormat FormatOverAStampSaying(string stamp)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "metadata-sync-refusals-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

        Directory.CreateDirectory(directory);

        var format = new StoreFormat(directory);

        File.WriteAllText(format.Location, stamp);
        return format;
    }

    /// <summary>
    /// A store two formats behind a build whose chain carries only the first of
    /// the two steps, which is the arrangement that reaches the refusal for a
    /// format nothing steps from.
    /// </summary>
    /// <returns>The store.</returns>
    private static StoreFormat MigrationOverAChainMissingItsSecondStep()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "metadata-sync-refusals-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

        Directory.CreateDirectory(directory);

        var chain = new[] { new FormatStep(StoreFormat.Earliest, _ => { }) };
        var format = new StoreFormat(directory, StoreFormat.Earliest + 2, chain);

        File.WriteAllText(
            format.Location,
            string.Format(CultureInfo.InvariantCulture, "{{\"format\":{0}}}", StoreFormat.Earliest));

        return format;
    }

    private static WrittenValues StoreInATemporaryDirectory()
    {
        return new WrittenValues(TemporaryDirectory());
    }

    private static ConflictLog LogInATemporaryDirectory()
    {
        return new ConflictLog(TemporaryDirectory());
    }

    private static string TemporaryDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "metadata-sync-refusals-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

        Directory.CreateDirectory(directory);
        return directory;
    }

    /// <summary>
    /// A store that reports holding one row and answers a removal with another
    /// number. No honest store produces that, and it is the arrangement the
    /// report refuses.
    /// </summary>
    /// <returns>The store.</returns>
    private static AStoreOfItsOwn AStoreThatDisagreesWithItself()
    {
        var store = new AStoreOfItsOwn("AStoreThatDisagreesWithItself", "one row");

        store.Add(Guid.Empty, "the row it reports");
        store.AnswersRemovalWith = 4;

        return store;
    }

    private static string SiteThatRefused(Exception refusal)
    {
        var root = Path.Combine(RepositoryRoot(), "Jellyfin.Plugin.MetadataSync");

        foreach (var frame in new StackTrace(refusal, true).GetFrames())
        {
            var file = frame.GetFileName();
            var line = frame.GetFileLineNumber();

            if (file is null || line <= 0 || !IsUnder(root, file))
            {
                continue;
            }

            var text = File.ReadLines(file).Skip(line - 1).First().Trim();
            return $"{RelativeTo(root, file)} -> {text}";
        }

        // Every frame with a file was outside the plugin, or none carried one.
        // The second case is a build without line information rather than a
        // refusal from somewhere else, and the caller's assertion says which by
        // printing the site it wanted.
        return "no frame inside the plugin carried a source line";
    }

    /// <summary>
    /// The item a write-path arrangement is about. It is the same identifier in
    /// every one of them, so an arrangement over an empty library and one over a
    /// library holding it differ by the library alone.
    /// </summary>
    private static readonly Guid _plannedItem = new("33333333-3333-3333-3333-333333333333");

    private static LibraryPlanTarget TargetOverAnEmptyLibrary()
    {
        var (library, _) = LibraryCalls.Empty();
        return new LibraryPlanTarget(library);
    }

    private static LibraryPlanTarget TargetOverTheItem()
    {
        var (library, _) = LibraryCalls.Holding(_plannedItem, new Movie());
        return new LibraryPlanTarget(library);
    }

    /// <summary>
    /// Runs a write to its end on this thread, so a refusal thrown inside the
    /// asynchronous half arrives here rather than sitting in a task nobody
    /// looked at.
    /// </summary>
    private static void Carried(LibraryPlanTarget target, ItemPlan plan)
    {
        target.WriteAsync(plan, CancellationToken.None).GetAwaiter().GetResult();
    }

    private static ItemPlan Writing(string field, string value)
    {
        var plan = new ItemPlan
        {
            LocalItemId = _plannedItem,
            Kind = "Movie",
            LastSavedWhenPlanned = LibraryPlanTarget.StampOf(new Movie()),
        };

        plan.Changes.Add(new PlannedChange
        {
            Field = field,
            PeerValue = value,
            Writes = true,
            ValueToWrite = value,
            Reason = "arranged to reach a refusal",
        });

        return plan;
    }

    private static PluginConfigurationProvider ProviderNamingALibraryTheServerDoesNotHold()
    {
        var configuration = new PluginConfiguration { PairingId = new Guid("44444444-4444-4444-4444-444444444444") };
        configuration.ParticipatingLibraries.Add(new Guid("22222222-2222-2222-2222-222222222222"));

        return new PluginConfigurationProvider(() => configuration, Array.Empty<Guid>);
    }

    private static LibraryPlanTarget TargetOverTheItemAsSomethingElseLeftIt()
    {
        var moved = new Movie { DateLastSaved = new DateTime(2026, 8, 13, 1, 0, 0, DateTimeKind.Utc) };
        var (library, _) = LibraryCalls.Holding(_plannedItem, moved);
        return new LibraryPlanTarget(library);
    }

    private static ItemPlan WithNoToken()
    {
        var plan = new ItemPlan { LocalItemId = _plannedItem, Kind = "Movie" };

        plan.Changes.Add(new PlannedChange
        {
            Field = "Name",
            PeerValue = "theirs",
            Writes = true,
            ValueToWrite = "theirs",
            Reason = "arranged to reach a refusal",
        });

        return plan;
    }

    private static Plan OneItemThatWrites()
    {
        var plan = new Plan();
        plan.Items.Add(Writing("Name", "theirs"));
        return plan;
    }

    private static PlanRequest OneItemToPlan()
    {
        var item = new ItemObservation
        {
            LocalItemId = new Guid("aaaaaaaa-0000-0000-0000-000000000001"),
            PeerItemId = new Guid("bbbbbbbb-0000-0000-0000-000000000002"),
            Kind = "Movie",
        };

        item.Fields.Add(new FieldObservation
        {
            Field = "Overview",
            LocalValue = null,
            PeerValue = "theirs",
            LastWrittenByThisPlugin = null,
            FieldLockedHere = false,
            FieldLockedOnPeer = false,
        });

        var request = new PlanRequest { Direction = SyncDirection.TwoWay };
        request.Items.Add(item);
        return request;
    }

    private static bool IsUnder(string root, string file)
    {
        return !Path.GetRelativePath(root, file).StartsWith("..", StringComparison.Ordinal);
    }

    private static void AssertIsAFact(string testClass, string testMethod)
    {
        var type = typeof(RefusalTests).Assembly.GetType(
            $"{typeof(RefusalTests).Namespace}.{testClass}");
        Assert.NotNull(type);

        var method = type.GetMethod(testMethod, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(method);

        Assert.NotEmpty(method.GetCustomAttributes(typeof(FactAttribute), false));
    }

    /// <summary>
    /// Reads the plugin sources and returns one entry per line of code that
    /// refuses. Comment lines are skipped, because a comment describing a
    /// refusal is not one.
    /// </summary>
    /// <remarks>
    /// The bound is honest: this is a line scan and not a parse. A throw
    /// spelled across two lines is missed, and the word inside a block comment
    /// or a string literal is counted. It reads the two spellings this
    /// repository uses, which are the <c>throw</c> keyword and the argument
    /// helpers that throw on the caller's behalf.
    /// </remarks>
    private static IReadOnlyList<string> RefusalSites()
    {
        var root = Path.Combine(RepositoryRoot(), "Jellyfin.Plugin.MetadataSync");

        return PluginSourceFiles()
            .SelectMany(file => RefusalLines(file)
                .Select(line => $"{RelativeTo(root, file)} -> {line}"))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private static IEnumerable<string> RefusalLines(string file)
    {
        return File.ReadLines(file)
            .Select(line => line.Trim())
            .Where(line => !line.StartsWith("//", StringComparison.Ordinal))
            .Where(line => !line.StartsWith('*'))
            .Where(line => line.Contains("throw ", StringComparison.Ordinal)
                || line.Contains(".Throw", StringComparison.Ordinal));
    }

    private static string RelativeTo(string root, string file)
    {
        return Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
    }

    private static IReadOnlyList<string> PluginSourceFiles()
    {
        var pluginDirectory = Path.Combine(RepositoryRoot(), "Jellyfin.Plugin.MetadataSync");
        Assert.True(Directory.Exists(pluginDirectory), $"Plugin sources not found at {pluginDirectory}");

        return Directory.GetFiles(pluginDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();
    }

    private static string RepositoryRoot([CallerFilePath] string thisFile = "")
    {
        // This file sits one directory below the repository root, and the
        // compiler writes its path in. Walking up from the test binary instead
        // would depend on the configuration and the target framework.
        var testProjectDirectory = Path.GetDirectoryName(thisFile);
        Assert.NotNull(testProjectDirectory);

        var root = Path.GetDirectoryName(testProjectDirectory);
        Assert.NotNull(root);
        return root;
    }
}
