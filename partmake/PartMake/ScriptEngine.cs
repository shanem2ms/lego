using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace partmake
{
    public class ScriptEngine
    {
        static Action<string> Write = (string? message) => { System.Diagnostics.Debug.WriteLine(message); };

        public delegate void WriteDel(string text);
        public WriteDel WriteLine;
        MetadataReference[] references;

        CSharpCompilation Compile(string codeToCompile)
        {
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(codeToCompile);


            if (this.references == null)
            {
                HashSet<string> refPaths = new HashSet<string>();
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.IsDynamic)
                        continue;
                    string loc = assembly.Location.Trim();
                    if (loc.Length > 0)
                        refPaths.Add(loc);
                }

                references = refPaths.Select(r => MetadataReference.CreateFromFile(r)).ToArray();
            }
            string assemblyName = Path.GetRandomFileName();

            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            return compilation;
        }

        public void Run(string codeToCompile, LayoutVis vis)
        {
            Compile(codeToCompile);
            using (var ms = new MemoryStream())
            {
                var compilation = Compile(codeToCompile);
                EmitResult result = compilation.Emit(ms);

                if (!result.Success)
                {
                    WriteLine("Compilation failed!");
                    IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic =>
                        diagnostic.IsWarningAsError ||
                        diagnostic.Severity == DiagnosticSeverity.Error);

                    foreach (Diagnostic diagnostic in failures)
                    {
                        WriteLine($"\t{diagnostic.Id}: [{diagnostic.Location.GetMappedLineSpan()}] {diagnostic.GetMessage()}");
                    }
                }
                else
                {
                    WriteLine("Compilation successful!");

                    ms.Seek(0, SeekOrigin.Begin);

                    Assembly assembly = AssemblyLoadContext.Default.LoadFromStream(ms);
                    var type = assembly.GetType("partmake.script.Script");
                    var instance = assembly.CreateInstance("partmake.script.Script");
                    var meth = type.GetMember("Run").First() as MethodInfo;
                    List<PartInst> outparts = new List<PartInst>();
                    meth.Invoke(instance, new object[] { outparts, this });
                    lock (vis.PartList)
                    {
                        vis.PartList.Clear();
                        vis.PartList.AddRange(outparts);
                    }
                }
            }


        }

        public enum CodeCompleteType
        {
            Member,
            Function
        }

        public List<string> CodeComplete(string codeToCompile, int position, string variable, CodeCompleteType ccType)
        {
            Write("Let's compile!");

            //Write("Parsing the code into the SyntaxTree");
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(codeToCompile);
            if (this.references == null)
            {
                HashSet<string> refPaths = new HashSet<string>();
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.IsDynamic)
                        continue;
                    string loc = assembly.Location.Trim();
                    if (loc.Length > 0)
                        refPaths.Add(loc);
                }

                references = refPaths.Select(r => MetadataReference.CreateFromFile(r)).ToArray();
            }
            string assemblyName = Path.GetRandomFileName();

            WriteLine("Compiling ...");
            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var semanticModel = compilation.GetSemanticModel(syntaxTree);

            var syntaxNode = syntaxTree.GetRoot().DescendantNodes(new TextSpan(position, variable.Length))
                .Where(n => n is ExpressionSyntax &&
                n.Span.Start == position && n.Span.End == (position + variable.Length)).FirstOrDefault();
            if (syntaxNode != null)
            {
                if (ccType == CodeCompleteType.Member)
                {
                    var info = semanticModel.GetTypeInfo(syntaxNode as ExpressionSyntax);
                    if (info.Type != null)
                    {
                        var hashset = info.Type.GetMembers().Select(m => m.Name).ToHashSet();
                        hashset.Remove(".ctor");
                        return hashset.ToList();
                    }
                }
                else if (ccType == CodeCompleteType.Function)
                {
                    var syminfo = semanticModel.GetSymbolInfo(syntaxNode);
                    List<string> symlist = new List<string>();
                    foreach (var sym in syminfo.CandidateSymbols)
                    {
                        string symstr = sym.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                        symlist.Add(symstr);
                    }
                    return symlist;
                }
            }
            return null;
        }
    }
}