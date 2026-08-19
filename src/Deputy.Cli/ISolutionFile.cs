namespace Deputy.Cli;

public interface ISolutionFile
{
	IEnumerable<ProjectInfo> ProjectsInOrder { get; }
}