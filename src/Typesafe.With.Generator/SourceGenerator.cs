using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Typesafe.With.Generator
{
    /**
     * This source generator generates With method for classes which satisfy the following conditions:
     * 1. Class is public
     * 2. Class has at least one public property
     *
     * The generated code will be placed in the same namespace as the class.
     * The generated extension methods will be internal.
     * 
     * Future work:
     * - Support for exposing With methods via attribute 
     */
    
    // Ref: https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.cookbook.md
    [Generator]
    public class WithMethodGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Get MSBuild properties
            // Ref: https://andrewlock.net/creating-a-source-generator-part-13-providing-and-accessing-msbuild-settings-in-source-generators/
            var msBuildProperties = context.AnalyzerConfigOptionsProvider
                .Select((provider, _) =>
                    {
                        return new
                        {
                            ShouldGenerate = provider.GlobalOptions.TryGetValue("build_property.GenerateWither", out var gen)
                                             && bool.TryParse(gen, out var generateWither)
                                             && generateWither,
                            IncludePrivate = provider.GlobalOptions.TryGetValue("build_property.WitherIncludePrivateProps", out var priv)
                                             && bool.TryParse(priv, out var includePrivateProps)
                                             && includePrivateProps,
                            AssemblyName = provider.GlobalOptions.TryGetValue("build_property.AssemblyName", out var assemblyName)
                                ? assemblyName
                                : string.Empty,
                            ProjectDir = provider.GlobalOptions.TryGetValue("build_property.ProjectDir", out var projectDir)
                                ? projectDir
                                : string.Empty,

                            // You can access other MSBuild properties here
                            // RootNamespace = provider.GlobalOptions.TryGetValue("build_property.RootNamespace", out var ns) ? ns : string.Empty,
                        };
                    }
                );

            // Get all class declarations
            IncrementalValuesProvider<ClassDeclarationSyntax> classDeclarations = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (s, _) => IsSyntaxTargetForGeneration(s),
                    transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx))
                .Where(static m => m is not null)!;

            
            // Combine with compilation and MSBuild properties
            IncrementalValueProvider<(Compilation, ImmutableArray<ClassDeclarationSyntax>, MSBuildProperties)> combined
                = context.CompilationProvider.Combine(classDeclarations.Collect())
                    .Combine(msBuildProperties)
                    .Select((tuple, _) => (
                        tuple.Left.Left,
                        tuple.Left.Right,
                        new MSBuildProperties(tuple.Right.AssemblyName, tuple.Right.ProjectDir, tuple.Right.ShouldGenerate, tuple.Right.IncludePrivate)
                    ));

            // Generate the source
            context.RegisterSourceOutput(combined,
                static (spc, source) => Execute(source.Item1, source.Item2, source.Item3, spc));

            
            // // Combine with compilation
            // IncrementalValueProvider<(Compilation, ImmutableArray<ClassDeclarationSyntax>)> compilationAndClasses
            //     = context.CompilationProvider.Combine(classDeclarations.Collect());
            //
            // // Generate the source
            // context.RegisterSourceOutput(compilationAndClasses,
            //     static (spc, source) => Execute(source.Item1, source.Item2, spc));
        }

        private class MSBuildProperties
        {
            private readonly string _assemblyName;
            private readonly string _projectDir;
            private readonly bool _shouldGenerate;
            private readonly bool _includePrivate;

            public MSBuildProperties(string assemblyName, string projectDir, bool shouldGenerate, bool includePrivate)
            {
                _assemblyName = assemblyName;
                _projectDir = projectDir;
                _shouldGenerate = shouldGenerate;
                _includePrivate = includePrivate;
            }
        }
        
        private static bool IsSyntaxTargetForGeneration(SyntaxNode node)
        {
            if (node is not ClassDeclarationSyntax cds) return false;

            // We don't generate methods if there aren't any properties
            var properties = cds.Members
                .OfType<PropertyDeclarationSyntax>()
                .ToArray();

            if (properties.Length == 0) return false;

            if (!IsPublic(cds)) return false;
            
            return true;
        }

        private static bool IsPublic(ClassDeclarationSyntax classDeclaration)
        {
            return classDeclaration.Modifiers.Any(x => x.Text == "public");
        }
        
        private static bool HasAttribute(ClassDeclarationSyntax classDeclaration)
        {
            if (classDeclaration.AttributeLists.Count == 0) return false;
            
            var witherAttributes =
                from attributeList in classDeclaration.AttributeLists
                from attribute in attributeList.Attributes
                where attribute.Name.ToString() is "Wither" or "WitherAttribute"
                select true;
        
            return witherAttributes.Any();
        }
        
        private static bool ShouldExposeWithMethods(ClassDeclarationSyntax classDeclaration)
        {
            if (classDeclaration.AttributeLists.Count == 0) return false;
            
            var witherAttributes =
                from attributeList in classDeclaration.AttributeLists
                from attribute in attributeList.Attributes
                where attribute.Name.ToString() is "Wither" or "WitherAttribute"
                select true;
        
            return witherAttributes.Any();
        }
        
        private static bool IsInNamespaces(ClassDeclarationSyntax classDeclaration)
        {
            var namespaceEndings = new string[] { "Model", "Models" };
            
            var currentNode = classDeclaration.Parent;
            while (currentNode != null)
            {
                if (currentNode is NamespaceDeclarationSyntax namespaceDecl)
                {
                    return namespaceEndings.Contains(GetNamespaceEnding(namespaceDecl.Name));
                }
                
                if (currentNode is FileScopedNamespaceDeclarationSyntax fileScopedNamespace)
                {
                    return namespaceEndings.Contains(GetNamespaceEnding(fileScopedNamespace.Name));
                }
                
                currentNode = currentNode.Parent;
            }
            
            return false;

            string GetNamespaceEnding(NameSyntax name) => name.ToString().Split('.').Last();
        }

        private static bool IsInAssembly(IAssemblySymbol assemblySymbol)
        {
            var assemblyEndings = new string[] { "Domain", "Model" };

            var assemblyNameEnding = GetAssemblyNameEnding(assemblySymbol.Name);
            
            return assemblyEndings.Contains(assemblyNameEnding);
            
            string GetAssemblyNameEnding(string name) => name.ToString().Split('.').Last();
        }
        
        
        private static ClassDeclarationSyntax? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
        {
            var classDeclaration = (ClassDeclarationSyntax)context.Node;
            var symbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);
            
            if (symbol == null) return null;

            // Check if any of the following criterias are met:
            // 1. Type has WitherAttribute
            // 2. Namespace ends with Model/Models
            // 3. Assembly name is Domain or Model

            return classDeclaration;
            
            // if (IsInNamespaces(classDeclaration) || IsInAssembly(symbol.ContainingAssembly))
            // {
            //     return classDeclaration;
            // }
            
            // return null;
        }
        
        private static void Execute(Compilation compilation, ImmutableArray<ClassDeclarationSyntax> classes, MSBuildProperties props, SourceProductionContext context)
        {
            // First, generate the attribute class
            string attributeSource = GenerateWitherAttributeClass();
            context.AddSource("WitherAttribute.g.cs", attributeSource);

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
        
        private static string GenerateWitherAttributeClass()
        {
            return """
                   namespace Typesafe.With.Generator
                   {
                       [System.AttributeUsage(System.AttributeTargets.Class)]
                       public class WitherAttribute : System.Attribute
                       {
                       }
                   }
                   """;
        }
        
        private static string GenerateWithMethods(INamedTypeSymbol classSymbol, ClassDeclarationSyntax classDeclaration)
        {
            var properties = classSymbol.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(p => p.DeclaredAccessibility == Accessibility.Public)
                .ToArray();

            var namespaceName = GetNamespace(classDeclaration);
            var sourceBuilder = new StringBuilder();

            var accessModifier = ShouldExposeWithMethods(classDeclaration) ? "public" : "internal"; //classDeclaration.Modifiers.Select(x => x.Text).FirstOrDefault() ?? "internal";
            var fullyQualifiedTypeName = classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            
            sourceBuilder.AppendLine("using Typesafe.With;");
            sourceBuilder.AppendLine();
            
            if (!string.IsNullOrEmpty(namespaceName))
            {
                sourceBuilder.AppendLine($"namespace {namespaceName}");
                sourceBuilder.AppendLine("{");
            }

            // Begin class declaration
            sourceBuilder.AppendLine($"    {accessModifier} static class {classSymbol.Name}WithExtensions");
            sourceBuilder.AppendLine("    {");

            // Generate With methods for each property
            foreach (var property in properties)
            {
                var methodName = $"With{property.Name}";
                sourceBuilder.AppendLine($"        {accessModifier} static {fullyQualifiedTypeName} {methodName}(this {fullyQualifiedTypeName} self, {property.Type} value) => self.With(instance => instance.{property.Name}, value);");
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