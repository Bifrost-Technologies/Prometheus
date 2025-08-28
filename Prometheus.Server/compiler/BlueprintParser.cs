
using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
namespace Prometheus.Server.compiler
{
    public static class BlueprintParser
    {
        /// <summary>
        /// Extracts all code snippets wrapped in XML-like tags from a blueprint string.
        /// </summary>
        public static Dictionary<string, string> ExtractCodeBlocks(string blueprint)
        {
            var result = new Dictionary<string, string>();
            var pattern = @"<(?<filename>[\w\.]+)>\s*(?<code>[\s\S]*?)\s*</\k<filename>>";
            foreach (Match m in Regex.Matches(blueprint, pattern))
            {
                var name = m.Groups["filename"].Value.Trim();
                var code = m.Groups["code"].Value;
                result[name] = code;
            }
            return result;
        }

        /// <summary>
        /// Parses the <Files> section of the blueprint to determine which files
        /// go in Root, State, and Instructions folders.
        /// </summary>
        private static (List<string> Root, List<string> State, List<string> Instructions)
            ParseFileLayout(string blueprint)
        {
            string ExtractList(string tag)
            {
                var pat = $@"<{tag}>\s*([\s\S]*?)\s*</{tag}>";
                var m = Regex.Match(blueprint, pat);
                return m.Success ? m.Groups[1].Value : string.Empty;
            }

            List<string> SplitNames(string raw)
                => raw
                   .Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                   .Select(s => s.Trim())
                   .Where(s => !string.IsNullOrWhiteSpace(s))
                   .ToList();

            var rootList = SplitNames(ExtractList("Root"));
            var stateList = SplitNames(ExtractList("State"));
            var instructionsList = SplitNames(ExtractList("Instructions"));

            return (rootList, stateList, instructionsList);
        }

        /// <summary>
        /// Creates a Rust‐style project skeleton and writes each extracted code file
        /// into its correct folder (root/src, src/state, src/instructions).
        /// </summary>
        public static void CreateProjectFromBlueprint(string projectName, string blueprint)
        {
            // Extract code snippets and file layout
            var codeBlocks = ExtractCodeBlocks(blueprint);
            var (rootFiles, stateFiles, instrFiles) = ParseFileLayout(blueprint);

            // Setup directories
            var projectRoot = Path.Combine(Directory.GetCurrentDirectory(), projectName);
            var srcRoot = Path.Combine(projectRoot, "src");
            var stateFolder = Path.Combine(srcRoot, "state");
            var instructionsFolder = Path.Combine(srcRoot, "instructions");

            Directory.CreateDirectory(srcRoot);
            Directory.CreateDirectory(stateFolder);
            Directory.CreateDirectory(instructionsFolder);

            // Helper to write a file
            void WriteIfExists(string folder, string fileName)
            {
                if (!codeBlocks.TryGetValue(fileName, out var content))
                {
                    Console.WriteLine($"Warning: No code block found for {fileName}, skipping.");
                    return;
                }
                var path = Path.Combine(folder, fileName);
                File.WriteAllText(path, content);
            }

            // Write Root files (e.g. lib.rs)
            foreach (var f in rootFiles)
                WriteIfExists(srcRoot, f);

            // Write State files
            foreach (var f in stateFiles)
                WriteIfExists(stateFolder, f);

            // Write Instruction files
            foreach (var f in instrFiles)
                WriteIfExists(instructionsFolder, f);
        }
    }
}
// Example usage:
//
// string blueprintText = File.ReadAllText("blueprint.md");
// BlueprintParser.CreateProjectFromBlueprint("MyRustGame", blueprintText);
//
// This will create:
//
// MyRustGame/
// └── src/
//     ├── lib.rs       (from <lib.rs>…</lib.rs>)
//     ├── state/
//     │   ├── player.rs
//     │   └── land.rs
//     └── instructions/
//         ├── init_season.rs
//         └── start_match.rs