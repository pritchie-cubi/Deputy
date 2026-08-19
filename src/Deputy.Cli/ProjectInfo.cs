namespace Deputy.Cli;

public readonly record struct ProjectInfo(string Name, string Path)
{
	public FileInfo FileInfo { get; } = new(Path);
}