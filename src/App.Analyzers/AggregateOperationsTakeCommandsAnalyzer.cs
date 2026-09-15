using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace App.Analyzers;

/// <summary>
/// DDD0001: every public, non-static, state-changing operation on an aggregate root takes a command as its
/// first parameter (the Java starter's <c>aggregateOperationsTakeCommands</c> rule, at compile time).
/// A state-changing operation is a public instance method returning <c>void</c>, <c>Task</c> or <c>ValueTask</c>.
/// Further parameters are allowed for resolved dependencies (reference data, other aggregates, <c>TimeProvider</c>).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AggregateOperationsTakeCommandsAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "DDD0001";

    private const string AggregateRootMetadataName = "App.Domain.Common.IAggregateRoot`2";
    private const string CommandMetadataName = "App.Domain.Common.ICommand";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Aggregate operations take a command",
        messageFormat: "Operation '{0}' on aggregate '{1}' must take a command (a type implementing ICommand) as its first parameter",
        category: "Design",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "State-changing operations on an aggregate root are expressed as one command record per operation, "
                     + "so that the command hierarchy documents the aggregate's API and HAL links can be derived from it.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static startContext =>
        {
            var aggregateRoot = startContext.Compilation.GetTypeByMetadataName(AggregateRootMetadataName);
            var command = startContext.Compilation.GetTypeByMetadataName(CommandMetadataName);
            if (aggregateRoot is null || command is null)
            {
                return;
            }

            startContext.RegisterSymbolAction(
                symbolContext => Analyze(symbolContext, aggregateRoot, command),
                SymbolKind.NamedType);
        });
    }

    private static void Analyze(SymbolAnalysisContext context, INamedTypeSymbol aggregateRoot, INamedTypeSymbol command)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.TypeKind != TypeKind.Class || type.IsAbstract)
        {
            return;
        }

        if (!type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, aggregateRoot)))
        {
            return;
        }

        foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
        {
            if (!IsOperation(method))
            {
                continue;
            }

            var first = method.Parameters.FirstOrDefault();
            if (first is not null && ImplementsCommand(first.Type, command))
            {
                continue;
            }

            var location = method.Locations.FirstOrDefault() ?? Location.None;
            context.ReportDiagnostic(Diagnostic.Create(Rule, location, method.Name, type.Name));
        }
    }

    private static bool IsOperation(IMethodSymbol method)
    {
        if (method.MethodKind != MethodKind.Ordinary
            || method.IsStatic
            || method.IsOverride
            || method.IsImplicitlyDeclared
            || method.DeclaredAccessibility != Accessibility.Public)
        {
            return false;
        }

        var returnType = method.ReturnType;
        return returnType.SpecialType == SpecialType.System_Void
               || returnType is INamedTypeSymbol { Arity: 0, Name: "Task" or "ValueTask", ContainingNamespace: { Name: "Tasks", ContainingNamespace: { Name: "Threading", ContainingNamespace: { Name: "System" } } } };
    }

    private static bool ImplementsCommand(ITypeSymbol type, INamedTypeSymbol command)
        => SymbolEqualityComparer.Default.Equals(type, command)
           || type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, command));
}
