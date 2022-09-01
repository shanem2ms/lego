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
using System.Windows;

namespace partmake
{
    namespace script
    {
        public class Api
        {
            public delegate void WriteDel(string text);
            public static WriteDel WriteLine;
            public static Part GetPart(string name)
            {
                return LDrawFolders.GetCacheItem(name);
            }

            public static List<PartInst> Parts = null;
            public static List<System.Numerics.Vector4> Locators;
            public static OctTree octTree = null;

            public static void Reset()
            {
                script.Api.Parts = new List<PartInst>(); ;
                script.Api.octTree = new OctTree();
                script.Api.Locators = new List<System.Numerics.Vector4>();
            }

            public static void AddUnconnected(PartInst pi)
            {
                Parts.Add(pi);
                octTree.AddPart(pi);
            }
            public static void Connect(PartInst pi1, int connectorIdx1, PartInst pi0,
                int connectorIdx0)
            {
                var ci0 = pi0.item.Connectors[connectorIdx0];
                var ci1 = pi1.item.Connectors[connectorIdx1];

                System.Numerics.Matrix4x4 m2 =
                    ci1.IM44 * ci0.M44 * pi0.mat;
                pi1.mat = m2;
                ConnectionInst cinst = new ConnectionInst()
                {
                    p0 = pi0,
                    c0 = connectorIdx0,
                    p1 = pi1,
                    c1 = connectorIdx1
                };
                pi0.connections[connectorIdx0] = cinst;
                pi1.connections[connectorIdx1] = cinst;
                Api.Parts.Add(pi1);
            }
        }
    }
    public class ScriptEngine
    {
        static Action<string> Write = (string? message) => { System.Diagnostics.Debug.WriteLine(message); };

        MetadataReference[] references;

        CSharpCompilation Compile(List<string> sources)
        {            
            List<SyntaxTree> syntaxTrees = new List<SyntaxTree>();
            foreach (string src in sources)
            {
                syntaxTrees.Add(
                    CSharpSyntaxTree.ParseText(src));
            }


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
                syntaxTrees: syntaxTrees,
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            return compilation;
        }

        public void Run(List<string> codeToCompile, Scene scene, LayoutVis vis)
        {
            using (var ms = new MemoryStream())
            {
                var compilation = Compile(codeToCompile);
                EmitResult result = compilation.Emit(ms);

                if (!result.Success)
                {
                    script.Api.WriteLine("Compilation failed!");
                    IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic =>
                        diagnostic.IsWarningAsError ||
                        diagnostic.Severity == DiagnosticSeverity.Error);

                    foreach (Diagnostic diagnostic in failures)
                    {
                        script.Api.WriteLine($"\t{diagnostic.Id}: [{diagnostic.Location.GetMappedLineSpan()}] {diagnostic.GetMessage()}");
                    }
                }
                else
                {
                    ms.Seek(0, SeekOrigin.Begin);

                    Assembly assembly = AssemblyLoadContext.Default.LoadFromStream(ms);
                    var type = assembly.GetType("partmake.script.Script");
                    var instance = assembly.CreateInstance("partmake.script.Script");
                    var meth = type.GetMember("Run").First() as MethodInfo;
                    script.Api.Reset();
                    meth.Invoke(instance, new object[] {});
                    script.Api.octTree.CheckCollisions();
                    lock (scene)
                    {
                        scene.Rebuild(script.Api.Parts, script.Api.Locators);
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
                        hashset.RemoveWhere((str) => { return str.StartsWith("get_") || str.StartsWith("set_"); });
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