using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.CodeAnalysis.Text;
using Trine.Analyzer;

namespace SolutionFixer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if (!args.Any())
            {
                Console.WriteLine("dotnet run <path to solution>");
                return;
            }
            await FixSolution(args[0]);
        }

        public static async Task FixSolution(string solutionPath)
        {
            MSBuildLocator.RegisterDefaults();

            var analyzer = new MemberOrderAnalyzer();
            var codeFixProvider = new MemberOrderCodeFixProvider();

            using (var workspace = MSBuildWorkspace.Create())
            {
                var solution = await workspace.OpenSolutionAsync(solutionPath);
                var newSolution = solution;

                foreach (var projectId in solution.ProjectIds)
                {
                    var project = newSolution.GetProject(projectId)
                         ?? throw new Exception("Failed finding project " + projectId);
                    Console.Write($"Analyzing project {project.Name}");

                    // CG: Can only apply one code fix per file at a time
                    var analyzeProject = true;
                    while (analyzeProject)
                    {
                        analyzeProject = false;

                        var compilationWithAnalyzers = project.GetCompilationAsync().Result.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
                        var diags = compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().Result;

                        foreach (var documentId in project.DocumentIds)
                        {
                            var document = project.GetDocument(documentId);
                            var updatedDocument = await AnalyzeDocumentAsync(document, diags, codeFixProvider);
                            if (updatedDocument != document)
                            {
                                Console.Write(".");
                                analyzeProject = true;
                                project = updatedDocument.Project;
                                newSolution = updatedDocument.Project.Solution;

                            }
                        }
                    }
                    Console.WriteLine();
                }
                workspace.TryApplyChanges(newSolution);
            }
        }

        private static async Task<Document> AnalyzeDocumentAsync(Document document, IEnumerable<Diagnostic> diags, CodeFixProvider codeFixProvider)
        {
            var syntaxTree = await document.GetSyntaxTreeAsync();
            var analyzerDiagnostics = diags.Where(d => d.Location.SourceTree == syntaxTree).ToArray();
            if (!analyzerDiagnostics.Any()) return document;

            var actions = new List<CodeAction>();
            var context = new CodeFixContext(document, analyzerDiagnostics[0], (a, d) => actions.Add(a), CancellationToken.None);
            codeFixProvider.RegisterCodeFixesAsync(context).Wait();

            if (!actions.Any())
            {
                return document;
            }

            return ApplyFix(document, actions.ElementAt(0));
        }

        private static Document ApplyFix(Document document, CodeAction codeAction)
        {
            var operations = codeAction.GetOperationsAsync(CancellationToken.None).Result;
            var solution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
            return solution.GetDocument(document.Id) 
                ?? throw new Exception("Failed getting document " + document.Id);
        }
    }
}
