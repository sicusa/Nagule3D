namespace Nagule.CodeGenerators;

using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static Common;

[Generator]
internal partial class NaAssetModuleGenerator : IIncrementalGenerator
{
    public static readonly string AttributeName = "NaAssetModuleAttribute";
    public static readonly string AttributeType = $"Nagule.{AttributeName}`1";
    public static readonly string StatefulAttributeType = $"Nagule.{AttributeName}`2";
    private static readonly string AttributeSource = $$"""
// <auto-generated/>
#nullable enable

namespace Nagule;

using Sia;

[{{Common.GeneratedCodeAttribute}}]
[global::System.AttributeUsage(
    global::System.AttributeTargets.Class | global::System.AttributeTargets.Struct,
    Inherited = false, AllowMultiple = false)]
internal sealed class {{AttributeName}}<TAssetRecord> : global::System.Attribute
    where TAssetRecord : class, IAssetRecord
{
    public Type? ManagerType { get; }

    public {{AttributeName}}(Type? managerType = null)
    {
        ManagerType = managerType;
    }
}

[{{Common.GeneratedCodeAttribute}}]
[global::System.AttributeUsage(
    global::System.AttributeTargets.Class | global::System.AttributeTargets.Struct,
    Inherited = false, AllowMultiple = false)]
internal sealed class {{AttributeName}}<TAssetRecord, TAssetState> : global::System.Attribute
    where TAssetRecord : class, IAssetRecord
    where TAssetState : struct
{
    public Type? ManagerType { get; }

    public {{AttributeName}}(Type? managerType = null)
    {
        ManagerType = managerType;
    }
}
""";

    protected record CodeGenerationInfo(
        INamespaceSymbol Namespace,
        ImmutableArray<TypeDeclarationSyntax> ParentTypes,
        string ModuleName,
        string ManagerType,
        ITypeSymbol RecordType,
        ITypeSymbol? StateType,
        string ComponentType,
        string Qualifiers);

    private static SymbolDisplayFormat AssetManagerQualifiedFormat =
        new SymbolDisplayFormat(
            SymbolDisplayGlobalNamespaceStyle.Included,
            SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            SymbolDisplayGenericsOptions.None,
            SymbolDisplayMemberOptions.None,
            SymbolDisplayDelegateStyle.NameOnly,
            SymbolDisplayExtensionMethodStyle.Default,
            SymbolDisplayParameterOptions.None,
            SymbolDisplayPropertyStyle.NameOnly,
            SymbolDisplayLocalOptions.None,
            SymbolDisplayKindOptions.None,
            SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);
        
    private static void RegisterSourceOutput(IncrementalGeneratorInitializationContext context, string attributeName)
    {
        var codeGenInfos = context.SyntaxProvider.ForAttributeWithMetadataName(
            attributeName,
            static (syntaxNode, token) => true,
            static (syntax, token) =>
                (syntax, ParentTypes: GetParentTypes(syntax.TargetNode)))
            .Where(static t => t.ParentTypes.All(
                static typeDecl => typeDecl.Modifiers.Any(SyntaxKind.PartialKeyword)))
            .Select(static (t, token) => {
                var (syntax, parentTypes) = t;
                var model = syntax.SemanticModel;
                var assetAttr = syntax.Attributes[0];

                var cstrArgs = assetAttr.ConstructorArguments;
                var managerType =
                    ((INamedTypeSymbol?)cstrArgs[0].Value)?.ToDisplayString(AssetManagerQualifiedFormat)
                    ?? "global::Nagule.AssetManagerBase";
                
                var typeArgs = assetAttr.AttributeClass!.TypeArguments;
                var recordType = typeArgs[0];
                var stateType = typeArgs.Length == 2 ? typeArgs[1] : null;

                var componentType = FindAssetComponentName(recordType);
                string? recordTypeQualifiers = null;

                if (componentType != null) {
                    var recordFullName = recordType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    recordTypeQualifiers =
                        recordFullName.Substring(0, recordFullName.Length - recordType.Name.Length);
                }

                return new CodeGenerationInfo(
                    ModuleName: ((TypeDeclarationSyntax)syntax.TargetNode).Identifier.ToString(),
                    Namespace: syntax.TargetSymbol.ContainingNamespace,
                    ParentTypes: parentTypes,
                    ManagerType: managerType,
                    RecordType: recordType,
                    ComponentType: componentType!,
                    StateType: stateType,
                    Qualifiers: recordTypeQualifiers!
                );
            })
            .Where(static info => info.ComponentType != null);

        context.RegisterSourceOutput(codeGenInfos, (context, info) => {
            using var source = CreateSource(out var builder);
            GenerateSource(source, info);
            context.AddSource(GenerateFileName(info), builder.ToString());
        });
    }
    
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(context => {
            context.AddSource(AttributeName + ".g.cs",
                SourceText.From(AttributeSource, Encoding.UTF8));
        });
        RegisterSourceOutput(context, AttributeType);
        RegisterSourceOutput(context, StatefulAttributeType);
    }

    private static string GenerateFileName(CodeGenerationInfo info)
    {
        var builder = new StringBuilder();
        builder.Append(info.Namespace.ToDisplayString());
        builder.Append('.');
        foreach (var parentType in info.ParentTypes) {
            builder.Append(parentType.Identifier.ToString());
            builder.Append('.');
        }
        builder.Append(info.ModuleName);
        builder.Append(".g.cs");
        return builder.ToString();
    }

    private static void GenerateSource(IndentedTextWriter source, CodeGenerationInfo info)
    {
        using (GenerateInNamespace(source, info.Namespace)) {
            using (GenerateInPartialTypes(source, info.ParentTypes)) {
                source.Write("public partial class ");
                source.Write(info.ComponentType);
                source.WriteLine("Manager");
                source.Indent++;
                source.Write(": ");
                source.Write(info.ManagerType);
                source.Write('<');
                source.Write(info.Qualifiers);
                source.Write(info.ComponentType);
                if (info.StateType != null) {
                    source.Write(", ");
                    source.Write(info.StateType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                }
                source.WriteLine(">;");
                source.Indent--;
                source.WriteLine();

                source.Write("partial class ");
                source.Write(info.ModuleName);
                source.WriteLine(" : global::Nagule.AssetModuleBase");
                source.WriteLine('{');
                source.Indent++;

                source.WriteLine("protected sealed override void RegisterAssetManager(global::Sia.World world)");
                source.Indent++;
                source.Write("=> AddAddon<");
                source.Write(info.ComponentType);
                source.WriteLine("Manager>(world);");
                source.Indent--;

                source.Indent--;
                source.WriteLine('}');
            }
        }
    }
}