namespace Deputy.Cli;

internal record struct PackageDependency(string Id, string VersionRange, PackageDependencyType Type, string TargetFramework = "net8.0");
internal record struct ProjectDependency(string Id, ProjectFile ProjectFile);

public enum PackageDependencyType
{
	Unknown, Direct, Transient
}