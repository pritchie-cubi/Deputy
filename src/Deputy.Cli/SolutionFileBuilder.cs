namespace Deputy.Cli;

internal static class SolutionFileBuilder
{
	public static ISolutionFile Build(FileInfo solutionFile)
	{
		// Assuming a .slnx file extension for solution files
		if (solutionFile.Extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase))
		{
			return new SlnxFile(solutionFile.FullName);
		}

		if (solutionFile.Extension.Equals(".sln", StringComparison.OrdinalIgnoreCase))
		{
			return new SlnFile(solutionFile.FullName);
		}

		throw new NotSupportedException($"Solution file type '{solutionFile.Extension}' is not supported.");
	}

	public static ISolutionFile Build(string solutionFilePath)
	{
		return Build(new FileInfo(solutionFilePath));
	}
}