using System.Diagnostics;

using Graffs;
using Graffs.Algorithms;
using Graffs.Builders;

namespace Deputy.Cli;

// ReSharper disable once ClassNeverInstantiated.Global
public class Worker(ILogger<Worker> logger, IConfiguration configuration, IHostApplicationLifetime appLifetime) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
		var sourceDirectory = configuration["SourceDirectory"];
		if (string.IsNullOrEmpty(sourceDirectory))
		{
			if (logger.IsEnabled(LogLevel.Warning))
			{
				logger.LogWarning("SourceDirectory argument was missing. Aborting.");
			}
			appLifetime.StopApplication();
			return;
		}

		var codeGraphBuilder = new DependencyGraphBuilder<CodeNode>();
		var projectGraphBuilder = new DependencyGraphBuilder<ProjectNode>();
		var packageGraphBuilder = new DependencyGraphBuilder<PackageNode>();

		foreach (var subDirectory in Directory.GetDirectories(sourceDirectory))
		{
			var candidateRepoName = Path.GetFileName(subDirectory);
			foreach (var solutionFilePath in Directory.GetFiles(subDirectory, "*.sln?", SearchOption.AllDirectories))
			{
				Debug.Assert(File.Exists(solutionFilePath));
				var solutionFile = SolutionFileBuilder.Build(solutionFilePath);
				var solutionFileProjectsInOrder = solutionFile.ProjectsInOrder;
				foreach (var projectInfo in solutionFileProjectsInOrder)
				{
					Debug.Assert(projectInfo.FileInfo.Exists);
					var projectNode = new ProjectNode(projectInfo.Path, projectInfo.Name);
					ProjectFile projectFile = new(projectInfo.FileInfo);

					foreach (var projectDependency in projectFile.ProjectReferences)
					{
						projectGraphBuilder.AddDependency(
							new ProjectNode(projectDependency.ProjectFile.Path, projectDependency.Id),
							projectNode);
					}
					if(projectFile.WillOutputPackage)
					{
						PackageNode packageNode = new(projectFile.OutputPackageId!);
						foreach (var packageDependency in projectFile.PackageReferences)
						{
							packageGraphBuilder.AddDependency(
								dependency: new PackageNode(packageDependency.Id),
								dependent: packageNode);
						}
					}
				}

				CodeNode codeNode = new(solutionFilePath,
					Path.GetFileNameWithoutExtension(solutionFilePath),
					new CodeSource(CodeType.Solution,
						Path.Join(candidateRepoName, Path.GetFileName(solutionFilePath))));

				codeGraphBuilder.AddNode(codeNode);
				if (logger.IsEnabled(LogLevel.Information))
				{
					logger.LogInformation("Found solution file: {SolutionFile}", solutionFilePath);
				}
			}
			var packageGraph = packageGraphBuilder.Build();
			var prioritizedPackages = TopologicalSort.DfsSort(packageGraph);
			Debug.Assert(!prioritizedPackages.HasCycles);
		}

		appLifetime.StopApplication();
    }
}
public enum CodeType
{
	Unknown, Solution, Project, Other
}

public readonly record struct CodeSource(CodeType Type, string Identifier);
public readonly record struct CodeNode(string Id, string? DisplayName, CodeSource Source) : IGraphNode<string>;
public readonly record struct ProjectNode(string Id, string? DisplayName) : IGraphNode<string>;

#if NEED_PACKAGE_SOURCE
public enum PackageSourceType
{
	Unknown, Nuget, Project, InternalSource
}

public readonly record struct PackageSource(PackageSourceType Type, string Identifier)
{
}
#endif
public readonly record struct PackageNode(string Id
#if NEED_PACKAGE_SOURCE
	, PackageSource PackageSource
#endif
	) : IGraphNode<string>
{
	public string? DisplayName => Id;
}