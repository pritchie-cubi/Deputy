using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;

namespace Deputy.Cli;

internal class ProjectFile(FileInfo projectFileInfo)
{
	public string Name { get; } = System.IO.Path.GetFileNameWithoutExtension(projectFileInfo.Name);
	public string Path { get; } = projectFileInfo.FullName;

	private class OutputPackageInfo(bool willOutputPackage, string? packageId)
	{
		public bool WillOutputPackage { get; } = willOutputPackage;
		public string? PackageId { get; } = packageId;
	}

	private OutputPackageInfo? outputPackageInfo;

	public IEnumerable<PackageDependency> PackageReferences => field ??= GetReferencedPackages();
	public IEnumerable<ProjectDependency> ProjectReferences => field ??= GetReferencedProjects();

	public bool WillOutputPackage
	{
		get
		{
			outputPackageInfo ??= GetOutputPackageInfo();
			return outputPackageInfo.WillOutputPackage;
		}
	}

	public string? OutputPackageId
	{
		get
		{
			outputPackageInfo ??= GetOutputPackageInfo();
			return outputPackageInfo.PackageId;
		}
	}

	private OutputPackageInfo GetOutputPackageInfo()
	{
		XDocument doc = XDocument.Load(projectFileInfo.FullName);

		return TryGetNuGetPackageId(System.IO.Path.GetFileNameWithoutExtension(projectFileInfo.Name), doc, out var id)
			? new OutputPackageInfo(true, id)
			: new OutputPackageInfo(false, null);
	}

	private List<PackageDependency> GetReferencedPackages()
	{
		try
		{
			XDocument doc = XDocument.Load(projectFileInfo.FullName);

			return [.. GetReferencedPackages(doc)];
		}
		catch (Exception ex)
		{
			Console.WriteLine($"  Error reading file: {ex.Message}");
			throw;
		}
	}

	private List<ProjectDependency> GetReferencedProjects()
	{
		try
		{
			XDocument doc = XDocument.Load(projectFileInfo.FullName);

			return [.. GetReferencedProjects(doc)];
		}
		catch (Exception ex)
		{
			Console.WriteLine($"  Error reading file: {ex.Message}");
			throw;
		}
	}

	private static IEnumerable<ProjectDependency> GetReferencedProjects(XDocument doc)
	{
		var projects = doc.Descendants("ProjectReference");

		foreach (var project in projects)
		{
			string? path = project.Attribute("Include")?.Value;

			if (string.IsNullOrEmpty(path))
			{
				continue;
			}

			ProjectFile projectFile = new(new FileInfo(path));
			yield return new ProjectDependency(projectFile.Name, projectFile);
		}
	}

	private static IEnumerable<PackageDependency> GetReferencedPackages(XDocument doc)
	{
		// Query all <PackageReference> nodes within the XML
		var packages = doc.Descendants("PackageReference");

		foreach (var package in packages)
		{
			string? name = package.Attribute("Include")?.Value;

			// Versions can be attributes or child elements
			string version = package.Attribute("Version")?.Value
			                 ?? package.Element("Version")?.Value
			                 ?? "Centrally Managed / Implied";

			if (string.IsNullOrEmpty(name))
			{
				continue;
			}

			yield return new PackageDependency(name, version, PackageDependencyType.Direct);
		}
	}

	private static bool TryGetNuGetPackageId(string projectName, XDocument doc,
		[NotNullWhen(true)] out string? packageId)
	{
		XElement? root = doc.Root;
		if (root == null)
		{
			packageId = null;
			return false;
		}

		// Determine SDK from Project Sdk attribute (e.g., <Project Sdk="Microsoft.NET.Sdk">)
		string? sdkAttr = root.Attribute("Sdk")?.Value?.Trim();
		string sdkNormalized = sdkAttr ?? string.Empty;

		// Find explicit IsPackable elements (search all PropertyGroup/IsPackable)
		var isPackableElements = doc.Descendants()
			.Where(e => string.Equals(e.Name.LocalName, "IsPackable", StringComparison.OrdinalIgnoreCase))
			.ToList();

		bool explicitIsPackablePresent = isPackableElements.Any();
		bool explicitIsPackableValue = false;
		if (explicitIsPackablePresent)
		{
			// Use the last IsPackable element found (document order) as the effective value
			var last = isPackableElements.Last();
			var txt = last.Value?.Trim();
			if (!bool.TryParse(txt, out explicitIsPackableValue))
			{
				// treat non-boolean as false to be conservative
				explicitIsPackableValue = false;
			}
		}

		// Determine default behavior based on SDK when IsPackable is missing
		bool willPack;
		if (explicitIsPackablePresent)
		{
			willPack = explicitIsPackableValue;
		}
		else
		{
			// When missing:
			// - Microsoft.NET.Sdk => default true
			// - Microsoft.NET.Sdk.Web, Microsoft.NET.Test.Sdk, MSTest.Sdk => default false
			// - otherwise default false (conservative)
			string sdkLower = sdkNormalized.ToLowerInvariant();
			if (sdkLower.StartsWith("microsoft.net.sdk", StringComparison.OrdinalIgnoreCase) &&
			    sdkLower == "microsoft.net.sdk")
			{
				willPack = true;
			}
			else if (sdkLower.StartsWith("microsoft.net.sdk", StringComparison.OrdinalIgnoreCase) &&
			         sdkLower.Contains("microsoft.net.sdk") &&
			         sdkLower != "microsoft.net.sdk")
			{
				// If Sdk attribute contains multiple SDKs separated by ';' or has suffixes,
				// check for exact known web/test sdk names explicitly
				if (sdkLower.Contains("microsoft.net.sdk.web") ||
				    sdkLower.Contains("microsoft.net.test.sdk") ||
				    sdkLower.Contains("mstest.sdk"))
				{
					willPack = false;
				}
				else
				{
					// If the primary token is Microsoft.NET.Sdk treat as Microsoft.NET.Sdk default true
					var primary = sdkNormalized.Split([';'], StringSplitOptions.RemoveEmptyEntries).First().Trim();
					willPack = string.Equals(primary, "Microsoft.NET.Sdk", StringComparison.OrdinalIgnoreCase);
				}
			}
			else
			{
				// Simpler checks for common SDK names
				if (string.Equals(sdkNormalized, "Microsoft.NET.Sdk", StringComparison.OrdinalIgnoreCase))
				{
					willPack = true;
				}
				else if (string.Equals(sdkNormalized, "Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase) ||
				         string.Equals(sdkNormalized, "Microsoft.NET.Test.Sdk", StringComparison.OrdinalIgnoreCase) ||
				         string.Equals(sdkNormalized, "MSTest.Sdk", StringComparison.OrdinalIgnoreCase))
				{
					willPack = false;
				}
				else
				{
					// Conservative default when SDK is unknown and IsPackable missing
					willPack = false;
				}
			}
		}

		if (!willPack)
		{
			packageId = null;
			return false;
		}

		// If we get here, project will produce a package by rules. Now determine package id.
		// Precedence: PackageId, AssemblyName, RootNamespace
		string? id = GetFirstElementValue(doc, "PackageId")
		             ?? projectName;

		if (string.IsNullOrWhiteSpace(id))
		{
			// Could not determine an id; per spec return false and null packageId
			packageId = null;
			return false;
		}

		packageId = id;
		return true;

		// Helper to get element values by local name ignoring XML namespaces
		static string? GetFirstElementValue(XDocument d, string localName)
		{
			var el = d.Descendants().FirstOrDefault(e => string.Equals(e.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase));
			return el == null ? null : (el.Value?.Trim().Length > 0 ? el.Value.Trim() : null);
		}
	}
}

internal class SlnxFile : ISolutionFile
{
	private string solutionFilePath;
	private IEnumerable<ProjectInfo>? projectInfos;

	public SlnxFile(string solutionFilePath)
	{
		this.solutionFilePath = solutionFilePath;
	}
	private IEnumerable<ProjectInfo> ParseProjectsInfo()
	{
		var solutionDocument = XDocument.Load(solutionFilePath);
		var solutionDirectory = Path.GetDirectoryName(solutionFilePath)!;
		var projects = solutionDocument.Descendants("Project")
			.Where(p => p.Attribute("Path") != null)
			.Select(p => new {
				Name = Path.GetFileNameWithoutExtension(p.Attribute("Path")?.Value),
				Path = p.Attribute("Path")?.Value
			});
		return projects.Select(p => new ProjectInfo(p.Name!, GetFullyQualifiedPath(solutionDirectory, p.Path!)));

		static string GetFullyQualifiedPath(string rootDir, string subDir)
		{
			return Path.IsPathFullyQualified(subDir)
				? subDir
				: Path.Join(rootDir, subDir);
		}
	}

	public IEnumerable<ProjectInfo> ProjectsInOrder => projectInfos ??= ParseProjectsInfo();
}