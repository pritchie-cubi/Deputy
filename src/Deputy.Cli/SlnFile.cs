
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Deputy.Cli;

internal partial class SlnFile : ISolutionFile
{
	private readonly string solutionFilePath;

	public SlnFile(string solutionFilePath)
	{
		if (!File.Exists(solutionFilePath))
		{
			throw new FileNotFoundException($"Solution file not found: {solutionFilePath}");
		}
		this.solutionFilePath = solutionFilePath;
	}

	private IEnumerable<ProjectInfo>? projectInfos;

	private IEnumerable<ProjectInfo> ParseProjectsInfo()
	{
		using var reader = new StreamReader(solutionFilePath);
		var solutionDirectory = Path.GetDirectoryName(solutionFilePath)!;
		while (reader.ReadLine() is { } line)
		{
			var match = ProjectLineRegex().Match(line);
			if (!match.Success)
			{
				continue;
			}

			var fullPath = Path.Combine(solutionDirectory, match.Groups["Path"].Value);
			if (File.Exists(fullPath))
			{
				yield return new ProjectInfo(
					match.Groups["Name"].Value,
					fullPath);
			}
			else
			{
				Trace.TraceInformation($"Project file not found: {fullPath}");
			}
		}
	}
	public IEnumerable<ProjectInfo> ProjectsInOrder => projectInfos ??= ParseProjectsInfo();

	[GeneratedRegex(@"^Project\(""{(?<TypeGuid>[A-F0-9\-]+)}""\) = ""(?<Name>[^""]+)"", ""(?<Path>[^""]+)"", ""{(?<ProjectGuid>[A-F0-9\-]+)}""", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
	private static partial Regex ProjectLineRegex();
}
