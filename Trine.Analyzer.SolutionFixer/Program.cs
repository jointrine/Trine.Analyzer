using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
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
    static class Program
    {
        public static async Task FixSolution(string solutionPath)
        {
            MSBuildLocator.RegisterDefaults();

            var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
                new MemberOrderAnalyzer(), new EnumValueAnalyzer());
            var codeFixProviders = ImmutableArray.Create<CodeFixProvider>(
                new MemberOrderCodeFixProvider(),
                new EnumValueCodeFixProvider());

            using (var workspace = MSBuildWorkspace.Create())
            {
                var solution = await workspace.OpenSolutionAsync(solutionPath);
                var newSolution = solution;

                var totalNumFixes = 0;
                foreach (var projectId in solution.ProjectIds)
                {
                    var project = newSolution.GetProject(projectId)
                         ?? throw new Exception("Failed finding project " + projectId);
                    Console.Write($"Analyzing project {project.Name}");

                    // CG: Can only apply one code fix per file at a time
                    var analyzeProject = true;
                    var numFixes = 0;
                    var numErrors = 0;
                    while (analyzeProject)
                    {
                        analyzeProject = false;

                        var compilationWithAnalyzers = (await project.GetCompilationAsync()).WithAnalyzers(analyzers);
                        var diags = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
                        numErrors = diags.Count(d => d.Severity == DiagnosticSeverity.Error);
                        foreach (var documentId in project.DocumentIds)
                        {
                            var document = project.GetDocument(documentId)
                                ?? throw new Exception("Failed finding document " + documentId);
                            var updatedDocument = await AnalyzeDocumentAsync(document, diags, codeFixProviders);
                            if (updatedDocument != document)
                            {
                                Console.Write(".");
                                numFixes++;
                                totalNumFixes++;
                                analyzeProject = true;
                                project = updatedDocument.Project;
                                newSolution = updatedDocument.Project.Solution;
                            }
                        }
                    }

                    if (numErrors > 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($" {numErrors} UNFIXABLE ERRORS");
                    }
                    else if (numFixes > 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($" FIXED {numFixes} ERRORS");
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($" NO ERRORS FOUND");
                    }
                    Console.ResetColor();
                }

                if (totalNumFixes > 0)
                {
                    Console.WriteLine($"Apply {totalNumFixes} changes? (y/n)");
                    if (Console.ReadLine() == "y")
                    {
                        workspace.TryApplyChanges(newSolution);
                    }
                }
            }
        }

        static async Task Main(string[] args)
        {
            if (!args.Any())
            {
                Console.WriteLine("dotnet run <path to solution>");
                return;
            }
            await FixSolution(args[0]);
        }

        private static async Task<Document> AnalyzeDocumentAsync(Document document, IEnumerable<Diagnostic> diags, ImmutableArray<CodeFixProvider> codeFixProviders)
        {
            var syntaxTree = await document.GetSyntaxTreeAsync();
            var analyzerDiagnostics = diags.Where(d => d.Location.SourceTree == syntaxTree).ToArray();
            if (!analyzerDiagnostics.Any()) return document;

            var actions = new List<CodeAction>();
            var context = new CodeFixContext(document, analyzerDiagnostics[0], (a, d) => actions.Add(a), CancellationToken.None);
            foreach (var c in codeFixProviders.Where(p => p.FixableDiagnosticIds.Contains(analyzerDiagnostics[0].Id)))
            {
                await c.RegisterCodeFixesAsync(context);
            }

            if (!actions.Any())
            {
                return document;
            }

            foreach (var action in actions)
            {
                document = await ApplyFixAsync(document, action);
            }
            return document;
        }

        private static async Task<Document> ApplyFixAsync(Document document, CodeAction codeAction)
        {
            var operations = await codeAction.GetOperationsAsync(CancellationToken.None);
            var solution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
            return solution.GetDocument(document.Id)
                ?? throw new Exception("Failed getting document " + document.Id);
        }
    }
}
