#tool "nuget:?package=GitVersion.CommandLine&prerelease"
#tool "nuget:?package=OctopusTools"

using Path = System.IO.Path;
using IO = System.IO;
using Cake.Common.Tools;

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var octopusServer = Argument("octopusServer", "");
var octopusApiKey = Argument("octopusApiKey", "");

var isLocalBuild = string.IsNullOrWhiteSpace(octopusServer);

var packageId = "CakeDemoAspNetCoreApp";

var publishDir = "./publish";
var artifactsDir = "./artifacts";
var localPackagesDir = "../LocalPackages";

var gitVersionInfo = GitVersion(new GitVersionSettings {
    OutputType = GitVersionOutput.Json
});

var nugetVersion = gitVersionInfo.NuGetVersion;

Setup(context =>
{
    if(BuildSystem.IsRunningOnTeamCity)
        BuildSystem.TeamCity.SetBuildNumber(gitVersionInfo.NuGetVersion);

    Information("Building v{0}", nugetVersion);
});

Teardown(context =>
{
    Information("Finished running tasks.");
});

Task("__Default")
    .IsDependentOn("__Clean")
    .IsDependentOn("__Restore")
    .IsDependentOn("__Build")
    .IsDependentOn("__Publish")
    .IsDependentOn("__Pack")
    .IsDependentOn("__Push");

Task("__Clean")
    .Does(() =>
{
    CleanDirectory(artifactsDir);
    CleanDirectory(publishDir);
    CleanDirectories("./source/**/bin");
    CleanDirectories("./source/**/obj");
});

Task("__Restore")
    .Does(() => DotNetCoreRestore("source", new DotNetCoreRestoreSettings
    {
        ArgumentCustomization = args => args.Append($"/p:Version={nugetVersion}")
    })
);

Task("__Build")
    .Does(() =>
{
    DotNetCoreBuild("source", new DotNetCoreBuildSettings
    {
        Configuration = configuration,
        ArgumentCustomization = args => args.Append($"/p:Version={nugetVersion}")
    });
});

Task("__Publish")
    .Does(() =>
{
    DotNetCorePublish("source", new DotNetCorePublishSettings
    {
        Framework = "netcoreapp2.0",
        Configuration = configuration,
        OutputDirectory = publishDir,
        ArgumentCustomization = args => args.Append($"--no-build")
    });
});

Task("__Pack")
    .Does(() => {
    
    OctoPack(packageId, new OctopusPackSettings{
        BasePath = publishDir,
        Version=nugetVersion,
        OutFolder=artifactsDir
        });
});

Task("__Push")
    .Does(() => {

    var packageFile = $"{artifactsDir}\\{packageId}.{nugetVersion}.nupkg";
    
    if (!isLocalBuild)
    {
        OctoPush(octopusServer, octopusApiKey, new FilePath(packageFile), new OctopusPushSettings());
    }
    else
    {
        CreateDirectory(localPackagesDir);
        CopyFileToDirectory(packageFile, localPackagesDir);
    }
});

Task("Default")
    .IsDependentOn("__Default");

RunTarget(target);
