﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Microsoft.DotNet.Tests.EndToEnd
{
    public class GivenDotNetUsesMSBuild : TestBase
    {
        [Fact]
        public void ItCanNewRestoreBuildRunCleanMSBuildProject()
        {
            using (DisposableDirectory directory = Temp.CreateDirectory())
            {
                string projectDirectory = directory.Path;

                string newArgs = "console --debug:ephemeral-hive --no-restore";
                new NewCommandShim()
                    .WithWorkingDirectory(projectDirectory)
                    .Execute(newArgs)
                    .Should().Pass();

                new RestoreCommand()
                    .WithWorkingDirectory(projectDirectory)
                    .Execute("/p:SkipInvalidConfigurations=true")
                    .Should().Pass();

                new BuildCommand()
                    .WithWorkingDirectory(projectDirectory)
                    .Execute()
                    .Should().Pass();

                var runCommand = new RunCommand()
                    .WithWorkingDirectory(projectDirectory);

                //  Set DOTNET_ROOT as workaround for https://github.com/dotnet/cli/issues/10196
                runCommand = runCommand.WithEnvironmentVariable(Environment.Is64BitProcess ? "DOTNET_ROOT" : "DOTNET_ROOT(x86)",
                    Path.GetDirectoryName(DotnetUnderTest.FullName));

                runCommand.ExecuteWithCapturedOutput()
                    .Should().Pass()
                    .And.HaveStdOutContaining("Hello World!");

                var binDirectory = new DirectoryInfo(projectDirectory).Sub("bin");
                binDirectory.Should().HaveFilesMatching("*.dll", SearchOption.AllDirectories);

                new CleanCommand()
                    .WithWorkingDirectory(projectDirectory)
                    .Execute()
                    .Should().Pass();

                binDirectory.Should().NotHaveFilesMatching("*.dll", SearchOption.AllDirectories);
            }
        }

        [Fact]
        public void ItCanRunToolsInACSProj()
        {
            var testInstance = TestAssets.Get("MSBuildTestApp")
                                         .CreateInstance()
                                         .WithSourceFiles()
                                         .WithRestoreFiles();
         
            var testProjectDirectory = testInstance.Root;

            new DotnetCommand()
                .WithWorkingDirectory(testInstance.Root)
                .ExecuteWithCapturedOutput("-d portable")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello Portable World!");;
        }

        [Fact(Skip="https://github.com/dotnet/cli/issues/9688")]
        public void ItCanRunToolsThatPrefersTheCliRuntimeEvenWhenTheToolItselfDeclaresADifferentRuntime()
        {
            var testInstance = TestAssets.Get("MSBuildTestApp")
                                         .CreateInstance()
                                         .WithSourceFiles()
                                         .WithRestoreFiles();

            var testProjectDirectory = testInstance.Root;

            new DotnetCommand()
                .WithWorkingDirectory(testInstance.Root)
                .ExecuteWithCapturedOutput("-d prefercliruntime")
                .Should().Pass()
                .And.HaveStdOutContaining("Hello I prefer the cli runtime World!");;
        }

        [Fact(Skip="https://github.com/dotnet/cli/issues/9688")]
        public void ItCanRunAToolThatInvokesADependencyToolInACSProj()
        {
            var repoDirectoriesProvider = new RepoDirectoriesProvider();

            var testInstance = TestAssets.Get("TestAppWithProjDepTool")
                                         .CreateInstance()
                                         .WithSourceFiles()
                                         .WithRestoreFiles();

            var configuration = "Debug";

            var testProjectDirectory = testInstance.Root;

            new BuildCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute($"-c {configuration} ")
                .Should()
                .Pass();

            new DotnetCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .ExecuteWithCapturedOutput(
                    $"-d dependency-tool-invoker -c {configuration} -f netcoreapp3.0 portable")
                .Should().Pass()
                     .And.HaveStdOutContaining("Hello Portable World!");;
        }
    }
}
