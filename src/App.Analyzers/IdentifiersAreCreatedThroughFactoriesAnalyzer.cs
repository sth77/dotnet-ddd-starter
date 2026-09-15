using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace App.Analyzers;

/// <summary>
/// DDD0002: identifiers are structs, so <c>new SampleId()</c> and <c>default(SampleId)</c> compile and yield an
/// empty Guid that would pass through the type system. This rule turns both into compile errors; the only ways to
/// obtain an identifier are its <c>New()</c> and <c>From(...)</c> factories (the guard Vogen used to provide).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class IdentifiersAreCreatedThroughFactoriesAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "DDD0002";

    private const string IdentifierMetadataName = "App.Domain.Common.IIdentifier";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Identifiers are created through their factories",
        messageFormat: "Do not create '{0}' with new or default; use {0}.New() or {0}.From(...)",
        category: "Design",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A defaulted identifier is an empty Guid that the type system cannot distinguish from a real one.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static startContext =>
        {
            var identifier = startContext.Compilation.GetTypeByMetadataName(IdentifierMetadataName);
            if (identifier is null)
            {
                return;
            }

            startContext.RegisterOperationAction(
                operationContext => Analyze(operationContext, identifier),
                OperationKind.ObjectCreation,
                OperationKind.DefaultValue);
        });
    }

    private static void Analyze(OperationAnalysisContext context, INamedTypeSymbol identifier)
    {
        var (type, isViolation) = context.Operation switch
        {
            IObjectCreationOperation creation => (creation.Type, creation.Arguments.Length == 0),
            IDefaultValueOperation defaultValue => (defaultValue.Type, true),
            _ => (null, false),
        };

        if (!isViolation || type is not INamedTypeSymbol { IsValueType: true } named)
        {
            return;
        }

        // Nullable<TId> defaults to null, which is fine and idiomatic.
        if (named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            return;
        }

        if (!named.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, identifier)))
        {
            return;
        }

        // Inside the identifier's own declaration the private constructor is the legitimate path.
        if (SymbolEqualityComparer.Default.Equals(context.ContainingSymbol.ContainingType, named))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, context.Operation.Syntax.GetLocation(), named.Name));
    }
}
