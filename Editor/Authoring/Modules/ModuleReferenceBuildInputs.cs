#if UNITY_6000_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Unity.Services.CloudCode.Editor.Shared.Crypto;

namespace Unity.Services.CloudCode.Authoring.Editor.Modules
{
    // Over-covering only costs an extra deploy, under-covering skips one.
    static class ModuleReferenceBuildInputs
    {
        static readonly string[] k_IgnoredDirectories = { "bin", "obj", ".vs", ".idea", ".vscode", ".git" };

        static readonly string[] k_InheritedFiles =
        {
            "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "global.json", "nuget.config", "NuGet.Config"
        };

        static readonly string[] k_PathItems =
        {
            "Compile", "EmbeddedResource", "Content", "None", "Resource", "AdditionalFiles", "Analyzer", "Reference"
        };

        static readonly Regex k_SolutionProject = new Regex(
            "^Project\\(\"[^\"]*\"\\)\\s*=\\s*\"[^\"]*\"\\s*,\\s*\"([^\"]+)\"", RegexOptions.Multiline);

        static readonly Regex k_KnownProperty = new Regex(
            @"\$\((MSBuildThisFileDirectory|MSBuildProjectDirectory)\)", RegexOptions.IgnoreCase);

        static readonly string[] k_ProjectExtensions = { ".csproj", ".fsproj", ".vbproj", ".proj" };

        class Inputs
        {
            public readonly HashSet<string> Directories = new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> Files = new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> VisitedProjects = new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> VisitedImports = new HashSet<string>(StringComparer.Ordinal);
        }

        // Null when an input cannot be located without evaluating MSBuild, such as a path built from a property.
        internal static string Fingerprint(string solutionPath)
        {
            var fullSolutionPath = Path.GetFullPath(solutionPath);
            var solutionDir = Path.GetDirectoryName(fullSolutionPath);
            var inputs = new Inputs();
            inputs.Directories.Add(solutionDir);
            inputs.Files.Add(fullSolutionPath);
            AddInheritedFiles(solutionDir, inputs);

            if (!TryAddSolutionProjects(fullSolutionPath, solutionDir, inputs))
                return null;

            if (inputs.Directories.Any(IsFileSystemRoot))
                return null;

            return ComputeFingerprint(inputs);
        }

        static bool TryAddSolutionProjects(string solutionPath, string solutionDir, Inputs inputs)
        {
            if (!File.Exists(solutionPath))
                return true;

            foreach (Match match in k_SolutionProject.Matches(File.ReadAllText(solutionPath)))
            {
                var relative = match.Groups[1].Value;
                if (!k_ProjectExtensions.Any(extension => relative.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
                    continue;

                if (!TryAddProject(Combine(solutionDir, relative), inputs))
                    return false;
            }

            return true;
        }

        static bool TryAddProject(string projectPath, Inputs inputs)
        {
            if (!inputs.VisitedProjects.Add(projectPath))
                return true;

            inputs.Files.Add(projectPath);
            if (!File.Exists(projectPath))
                return true;

            var projectDir = Path.GetDirectoryName(projectPath);
            inputs.Directories.Add(projectDir);
            AddInheritedFiles(projectDir, inputs);
            return TryAddMsBuildFile(projectPath, projectDir, inputs);
        }

        // Item paths resolve against the project being built, import paths against the file that imports.
        static bool TryAddMsBuildFile(string file, string projectDir, Inputs inputs)
        {
            XDocument document;
            try
            {
                document = XDocument.Load(file);
            }
            catch (Exception)
            {
                return false;
            }

            var fileDir = Path.GetDirectoryName(file);
            foreach (var element in document.Descendants())
            {
                var name = element.Name.LocalName;
                if (name == "Import")
                {
                    if (element.Attribute("Sdk") != null)
                        continue;

                    if (!TryAddImport((string)element.Attribute("Project"), fileDir, projectDir, inputs))
                        return false;
                }
                else if (name == "ProjectReference")
                {
                    foreach (var path in SplitItems((string)element.Attribute("Include")))
                    {
                        if (!TryResolve(path, projectDir, fileDir, projectDir, out var resolved))
                            return false;
                        if (!TryAddProject(resolved, inputs))
                            return false;
                    }
                }
                else if (k_PathItems.Contains(name))
                {
                    var paths = SplitItems((string)element.Attribute("Include"))
                        .Where(path => name != "Reference" || LooksLikePath(path))
                        .Concat(element.Elements().Where(child => child.Name.LocalName == "HintPath").Select(child => child.Value));
                    foreach (var path in paths)
                    {
                        if (!TryResolve(path, projectDir, fileDir, projectDir, out var resolved))
                            return false;
                        AddPath(resolved, inputs);
                    }
                }
            }

            return true;
        }

        static bool TryAddImport(string project, string fileDir, string projectDir, Inputs inputs)
        {
            if (string.IsNullOrWhiteSpace(project))
                return true;

            if (!TryResolve(project, fileDir, fileDir, projectDir, out var resolved))
                return false;

            if (HasWildcard(resolved))
            {
                AddPath(resolved, inputs);
                return true;
            }

            if (!inputs.VisitedImports.Add(resolved))
                return true;

            inputs.Files.Add(resolved);
            return !File.Exists(resolved) || TryAddMsBuildFile(resolved, projectDir, inputs);
        }

        static bool TryResolve(string raw, string baseDir, string thisFileDir, string projectDir, out string resolved)
        {
            resolved = null;
            var path = k_KnownProperty.Replace(raw.Trim(), match =>
                match.Groups[1].Value.Equals("MSBuildProjectDirectory", StringComparison.OrdinalIgnoreCase)
                    ? projectDir
                    : thisFileDir + Path.DirectorySeparatorChar);

            if (path.Contains("$(") || path.Contains("@(") || path.Contains("%("))
                return false;

            resolved = Combine(baseDir, path);
            return true;
        }

        static void AddPath(string path, Inputs inputs)
        {
            if (!HasWildcard(path))
            {
                inputs.Files.Add(path);
                return;
            }

            var segments = path.Split(Path.DirectorySeparatorChar);
            var fixedSegments = segments.TakeWhile(segment => !HasWildcard(segment)).ToArray();
            var fixedPrefix = string.Join(Path.DirectorySeparatorChar.ToString(), fixedSegments);
            inputs.Directories.Add(string.IsNullOrEmpty(fixedPrefix) ? Path.DirectorySeparatorChar.ToString() : fixedPrefix);
        }

        static void AddInheritedFiles(string directory, Inputs inputs)
        {
            for (var current = directory; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            {
                foreach (var name in k_InheritedFiles)
                {
                    var candidate = Path.Combine(current, name);
                    if (File.Exists(candidate))
                        inputs.Files.Add(candidate);
                }
            }
        }

        static string ComputeFingerprint(Inputs inputs)
        {
            var files = new HashSet<string>(inputs.Files, StringComparer.Ordinal);
            foreach (var directory in inputs.Directories.Where(Directory.Exists))
            {
                foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                {
                    if (!IsIgnored(file, directory))
                        files.Add(file);
                }
            }

            var builder = new StringBuilder();
            foreach (var file in files.OrderBy(file => file, StringComparer.Ordinal))
            {
                builder.Append(file.Replace('\\', '/')).Append('\0')
                    .Append(File.Exists(file) ? FileDigest(file) : "missing").Append('\0');
            }

            return Hash.SHA1(builder.ToString());
        }

        static bool IsIgnored(string file, string directory)
        {
            if (file.EndsWith(".user", StringComparison.OrdinalIgnoreCase))
                return true;

            return file.Substring(directory.Length)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => k_IgnoredDirectories.Contains(segment, StringComparer.OrdinalIgnoreCase));
        }

        static string FileDigest(string path)
        {
            using var sha1 = System.Security.Cryptography.SHA1.Create();
            using var stream = File.OpenRead(path);
            return BitConverter.ToString(sha1.ComputeHash(stream));
        }

        static IEnumerable<string> SplitItems(string include)
        {
            return string.IsNullOrWhiteSpace(include)
                ? Enumerable.Empty<string>()
                : include.Split(';').Select(item => item.Trim()).Where(item => item.Length > 0);
        }

        static bool LooksLikePath(string include)
        {
            return include.Contains("/") || include.Contains("\\")
                || include.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
        }

        static bool HasWildcard(string path) => path.IndexOfAny(new[] { '*', '?' }) >= 0;

        static bool IsFileSystemRoot(string directory)
        {
            var trimmed = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return trimmed.Length == 0 || string.Equals(Path.GetPathRoot(directory)?.TrimEnd(Path.DirectorySeparatorChar), trimmed, StringComparison.Ordinal);
        }

        // Path.GetFullPath rejects wildcards on some runtimes, so a glob is normalized segment by segment.
        static string Combine(string baseDir, string path)
        {
            var normalized = path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            var combined = Path.IsPathRooted(normalized) ? normalized : Path.Combine(baseDir, normalized);
            var root = Path.GetPathRoot(combined) ?? string.Empty;
            var stack = new List<string>();
            foreach (var segment in combined.Substring(root.Length).Split(Path.DirectorySeparatorChar))
            {
                if (segment.Length == 0 || segment == ".")
                    continue;
                if (segment == "..")
                {
                    if (stack.Count > 0)
                        stack.RemoveAt(stack.Count - 1);
                    continue;
                }

                stack.Add(segment);
            }

            return root + string.Join(Path.DirectorySeparatorChar.ToString(), stack);
        }
    }
}
#endif
