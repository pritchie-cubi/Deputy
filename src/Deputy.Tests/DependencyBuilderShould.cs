using System.Xml.Linq;
using System.Xml.XPath;

using Graffs.Builders;

namespace Deputy.Tests;

public class DependencyBuilderShould
{
    [Fact]
    public void NotAddTheSameDependencyTwice()
    {
		DependencyGraphBuilder<string> builder = new();
		builder.AddDependency("A", "B");
		var graph = builder.Build();
		Assert.Single(graph.Edges);
		builder.AddDependency("A", "B");
		graph = builder.Build();
		Assert.Single(graph.Edges);
	}

	[Fact]
	public void Test()
	{
		var xmlText = @"<Project Sdk=""Microsoft.NET.Sdk"">

  <PropertyGroup>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>true</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include=""xunit.v3.mtp-v2"" Version=""4.0.0"" />
  </ItemGroup>

</Project>";
		var doc = XDocument.Parse(xmlText);
		Assert.NotNull(doc);
		var elements = doc.XPathSelectElements("/Project/PropertyGroup/IsPackable");
		Assert.NotEmpty(elements);
		var lastElement = elements.Last();
		Assert.Equal("true", lastElement.Value, ignoreCase: true);
		elements = doc.XPathSelectElements("/Project/ItemGroup/PackageReference");
		Assert.Equal("xunit.v3.mtp-v2", elements.Last().Attribute("Include")?.Value);
		Assert.True(Cli.ProjectFile.IsTestProject(doc));
	}

	[Fact]
	public void Test2()
	{
		var xmlText = @"<Project Sdk=""Microsoft.NET.Sdk"">

  <PropertyGroup>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>true</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include=""xunit.v3.mtp-v2"" Version=""4.0.0"" />
  </ItemGroup>

</Project>";
		var doc = XDocument.Parse(xmlText);
		Assert.NotNull(doc);
		Assert.True(Cli.ProjectFile.IsTestProject(doc));
	}
}
