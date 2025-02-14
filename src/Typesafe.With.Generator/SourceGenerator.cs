using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Typesafe.With.Generator
{
    [Generator]
    public class CustomGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Get all class declarations
            IncrementalValuesProvider<ClassDeclarationSyntax> classDeclarations = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (s, _) => IsSyntaxTargetForGeneration(s),
                    transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx))
                .Where(static m => m is not null)!;

            // Combine with compilation
            IncrementalValueProvider<(Compilation, ImmutableArray<ClassDeclarationSyntax>)> compilationAndClasses
                = context.CompilationProvider.Combine(classDeclarations.Collect());

            // Generate the source
            context.RegisterSourceOutput(compilationAndClasses,
                static (spc, source) => Execute(source.Item1, source.Item2, spc));
        }
        
        private static bool IsSyntaxTargetForGeneration(SyntaxNode node)
        {
            if (node is not ClassDeclarationSyntax cds) return false;

            var properties = cds.Members
                .OfType<PropertyDeclarationSyntax>()
                .ToArray();

            if (properties.Length == 0) return false;

            return true;
        }

        private static ClassDeclarationSyntax GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
        {
            return (ClassDeclarationSyntax)context.Node;
        }
        
        private static void Execute(Compilation compilation, ImmutableArray<ClassDeclarationSyntax> classes, SourceProductionContext context)
        {
            // Generate code for each class
            foreach (var classDeclaration in classes.Distinct())
            {
                var semanticModel = compilation.GetSemanticModel(classDeclaration.SyntaxTree);
                var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);
                if (classSymbol == null) continue;

                var source = GenerateWithMethods(classSymbol, classDeclaration);
                
                context.AddSource($"{classSymbol.Name}.With.g.cs", source);
            }
        }
        
        private static string GenerateWithMethods(INamedTypeSymbol classSymbol, ClassDeclarationSyntax classDeclaration)
        {
            var properties = classSymbol.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(p => p.DeclaredAccessibility == Accessibility.Public)
                .ToArray();

            var namespaceName = GetNamespace(classDeclaration);
            var sourceBuilder = new StringBuilder();

            var fullyQualifiedTypeName = classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            
            sourceBuilder.AppendLine("using Typesafe.With;");
            sourceBuilder.AppendLine();
            
            if (!string.IsNullOrEmpty(namespaceName))
            {
                sourceBuilder.AppendLine($"namespace {namespaceName}");
                sourceBuilder.AppendLine("{");
            }

            // Begin class declaration
            sourceBuilder.AppendLine($"    internal static class {classSymbol.Name}WithExtensions");
            sourceBuilder.AppendLine("    {");

            // Generate With methods for each property
            foreach (var property in properties)
            {
                var methodName = $"With{property.Name}";
                sourceBuilder.AppendLine($"        internal static {fullyQualifiedTypeName} {methodName}(this {fullyQualifiedTypeName} self, {property.Type} value) => self.With(instance => instance.{property.Name}, value);");
                sourceBuilder.AppendLine();
            }

            sourceBuilder.AppendLine("    }"); // End class

            if (!string.IsNullOrEmpty(namespaceName))
            {
                sourceBuilder.AppendLine("}"); // End namespace
            }

            return sourceBuilder.ToString();
        }

        private static string GetNamespace(ClassDeclarationSyntax classDeclaration)
        {
            var namespaceName = string.Empty;
            var parent = classDeclaration.Parent;
            
            while (parent != null)
            {
                if (parent is NamespaceDeclarationSyntax namespaceDeclaration)
                {
                    namespaceName = namespaceDeclaration.Name.ToString();
                    break;
                }
                if (parent is FileScopedNamespaceDeclarationSyntax fileScopedNamespace)
                {
                    namespaceName = fileScopedNamespace.Name.ToString();
                    break;
                }
                parent = parent.Parent;
            }

            return namespaceName;
        }
    }
}