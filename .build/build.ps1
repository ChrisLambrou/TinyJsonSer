# This file cannot be invoked directly; it simply contains a bunch of Invoke-Build tasks. To use it, invoke
# _init.ps1 which declares three global functions (build, clean, rebuild), then invoke one of those functions.

[CmdletBinding()]
param([string]$Configuration = 'Release')

use 14.0 MSBuild

# Useful paths used by multiple tasks.
$RepositoryRoot = "$PsScriptRoot\.." | Resolve-Path
$ReleaseNotesPath = "$RepositoryRoot\release-notes.md" | Resolve-Path
$SolutionPath = "$RepositoryRoot\TinyJsonSer.sln" | Resolve-Path
$NuGetPath = "$PsScriptRoot\nuget.exe" | Resolve-Path
$DistPath = "$RepositoryRoot\dist"


# Helper function for clearer logging of each task.
function Write-Info {
    [CmdletBinding()]
    param ([string] $Message)

    Write-Host "## $Message ##" -ForegroundColor Magenta
}


# Environment-specific configuration should happen here (and only here!)
task Init {
    Write-Info 'Establishing build properties'

    # Establish IsAutobuild property.
    $script:IsAutomatedBuild = $env:BRANCH_NAME -and $env:BUILD_NUMBER
    Write-Host "Is automated build = $IsAutomatedBuild"
    
    # Establish release notes and package version number
    $Notes = Read-ReleaseNotes $ReleaseNotesPath -ThreePartVersion
    $script:ReleaseNotes = $Notes.Content
    $script:Version = $Notes.Version
    Write-Host "Version = '$Version'"
    
    # Establish NuGet package version.
    $BranchName = Get-BranchName
    $IsDefaultBranch = $BranchName -eq 'master'
    $script:NuGetPackageVersion = New-SemanticNuGetPackageVersion -Version $Version -BranchName $BranchName -IsDefaultBranch $IsDefaultBranch
    Write-Host "NuGet package version = $NuGetPackageVersion"
}

function Get-BranchName {
    # If the branch name is specified via an environment variable (i.e. on TeamCity), use it.
    if ($env:BRANCH_NAME) {
        return $env:BRANCH_NAME
    }

    # If the .git folder is present, try to get the current branch using Git.
    $DotGitDirPath = "$RepositoryRoot\.git"
    if (Test-Path $DotGitDirPath) {
        Add-Type -Path ("$PsScriptRoot\packages\GitSharp\lib\GitSharp.dll" | Resolve-Path)
        Add-Type -Path ("$PsScriptRoot\packages\SharpZipLib\lib\20\ICSharpCode.SharpZipLib.dll" | Resolve-Path)
        Add-Type -Path ("$PsScriptRoot\packages\Tamir.SharpSSH\lib\Tamir.SharpSSH.dll" | Resolve-Path)
        Add-Type -Path ("$PsScriptRoot\packages\Winterdom.IO.FileMap\lib\Winterdom.IO.FileMap.dll" | Resolve-Path)
    
        $Repository = New-Object 'GitSharp.Repository' $DotGitDirPath
        return $Repository.CurrentBranch.Name
    }

    # Otherwise, assume 'dev'
    Write-Warning "Unable to determine the current branch name using either git or the BRANCH_NAME environment variable. Defaulting to 'dev'."
    return 'dev'
}


# Clean task, deletes all build output folders.
task Clean {
    Write-Info 'Cleaning build output'

    Get-ChildItem $RepositoryRoot -Exclude @('packages') -Include @('dist', 'bin', 'obj') -Directory -Recurse | ForEach-Object {
        Write-Host "Deleting $_"
        Remove-Item $_ -Force -Recurse
    }
}


# RestorePackages task, restores all the NuGet packages.
task RestorePackages {
    Write-Info "Restoring NuGet packages for solution $SolutionPath"

    & $NuGetPath @('restore', $SolutionPath)
}


# Compile task, runs MSBuild to build the solution.
task Compile  RestorePackages, {
    Write-Info "Compiling solution $SolutionPath"

    exec {
        msbuild "$SolutionPath" `
        /nodeReuse:False `
        /target:Build `
        /property:Configuration=$Configuration `
        $AdditionalMSBuildParameters
    }
}


# Test task, runs the automated tests.
task Test  Compile, {
    Write-Info 'Running tests'

    $TestAssemblyPath = "$RepositoryRoot\TinyJsonSer.Tests\bin\$Configuration\TinyJsonSer.Tests.dll" | Resolve-Path
    Invoke-NUnit3ForAssembly -AssemblyPath $TestAssemblyPath `
                             -NUnitVersion '3.6.1' `
                             -FrameworkVersion 'net-3.5' `
                             -EnableCodeCoverage $True `
                             -DotCoverFilters '+:TinyJsonSer.*;-:*.Tests' `
                             -DotCoverAttributeFilters '*.ExcludeFromCodeCoverageAttribute'
    $CoverageResultsPath = "$TestAssemblyPath.TestResult.coverage.snap" | Resolve-Path
    TeamCity-ImportDotNetCoverageResult 'dotcover' $CoverageResultsPath
}



# Package task, create the NuGet package.
task Package  Init, Compile, {
    Write-Info 'Generating NuGet package'
    
    # Create the output folder.
    $Null = mkdir $DistPath -Force

    # Transform the JsonSerializer source file.
    $SourcePath = "$RepositoryRoot\TinyJsonSer\JsonSerializer.cs" | Resolve-Path
    $TransformedSourcePath = "$DistPath\JsonSerializer.cs.pp"
    $Contents = [System.IO.File]::ReadAllText($SourcePath, [System.Text.Encoding]::UTF8)
    $Contents = $Contents.Replace('/***', '').Replace('***/', '')

    [System.IO.File]::WriteAllText($TransformedSourcePath, $Contents, [System.Text.Encoding]::UTF8)
    
    # Run NuGet pack.
    $NuSpecPath = "$RepositoryRoot\TinyJsonSer\TinyJsonSer.nuspec" | Resolve-Path
    $Parameters = @(
        'pack',
        "$NuSpecPath",
        '-Version', $NuGetPackageVersion,
        '-OutputDirectory', $DistPath,
        '-BasePath', $DistPath
        '-Properties', "releaseNotes=$ReleaseNotes"
    )
    Write-Host "$NuGetPath $Parameters"
    exec {
        & $NuGetPath $Parameters
    }

    # Publish the NuGet packages as TeamCity artefact.
    $NuGetPackagePath = "$DistPath\TinyJsonSer.Sources.$NuGetPackageVersion.nupkg" | Resolve-Path
    TeamCity-PublishArtifact $NuGetPackagePath

    # Delete the temporary transformed source file.
    Remove-Item $TransformedSourcePath
}



task Build  Test, Package
task Rebuild  Clean, Build
task Default  Build