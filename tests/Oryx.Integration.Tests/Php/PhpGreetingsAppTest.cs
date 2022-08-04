﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Oryx.BuildScriptGenerator.Common;
using Microsoft.Oryx.Tests.Common;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Oryx.Integration.Tests
{
    public class PhpGreetingsAppTest : PhpEndToEndTestsBase
    {
        private const string ExifImageDebianFlavorPng = "3";

        public PhpGreetingsAppTest(ITestOutputHelper output, TestTempDirTestFixture fixture)
            : base(output, fixture)
        {
        }

        // Unique category traits are needed to run each
        // platform-version in it's own pipeline agent. This is
        // because our agents currently a space limit of 10GB.
        [Fact, Trait("category", "php-81")]
        public async Task PipelineTestInvocationsPhp81Async()
        {   
            string phpVersion81 = "8.1";
            await Task.WhenAll(
                GreetingsAppTestAsync(phpVersion81),
                PhpFpmGreetingsAppTestAsync(phpVersion81));
        }

        [Fact, Trait("category", "php-80")]
        public async Task PipelineTestInvocationsPhp80Async()
        {   
            string phpVersion80 = "8.0";
            await Task.WhenAll(
                GreetingsAppTestAsync(phpVersion80),
                PhpFpmGreetingsAppTestAsync(phpVersion80));
        }

        [Fact, Trait("category", "php-74")]
        public async Task PipelineTestInvocationsPhp74Async()
        {
            string phpVersion74 = "7.4";
            await Task.WhenAll(
                GreetingsAppTestAsync(phpVersion74),
                PhpFpmGreetingsAppTestAsync(phpVersion74));
        }

        [Theory]
        [InlineData("8.1")]
        [InlineData("8.0")]
        [InlineData("7.4")]
        public async Task GreetingsAppTestAsync(string phpVersion)
        {
            // Arrange
            var appName = "greetings";
            var hostDir = Path.Combine(_hostSamplesDir, "php", appName);
            var volume = DockerVolume.CreateMirror(hostDir);
            var appDir = volume.ContainerDir;
            var appOutputDirVolume = CreateAppOutputDirVolume();
            var appOutputDir = appOutputDirVolume.ContainerDir;
            var buildScript = new ShellScriptBuilder()
               .AddCommand($"oryx build {appDir} -i /tmp/int -o {appOutputDir} " +
               $"--platform php --platform-version {phpVersion}")
               .ToString();
            var runScript = new ShellScriptBuilder()
                .AddCommand($"oryx create-script -appPath {appOutputDir} -output {RunScriptPath} -bindPort {ContainerPort}")
                .AddCommand(RunScriptPath)
                .ToString();

            // Act & Assert
            await EndToEndTestHelper.BuildRunAndAssertAppAsync(
                appName, _output, new[] { volume, appOutputDirVolume },
                "/bin/sh", new[] { "-c", buildScript },
                _imageHelper.GetRuntimeImage("php", phpVersion),
                ContainerPort,
                "/bin/sh", new[] { "-c", runScript },
                async (hostPort) =>
                {
                    var output = await _httpClient.GetStringAsync($"http://localhost:{hostPort}/");
                    Assert.Contains("Hello World", output);
                    Assert.Contains("oryx oryx oryx", output);
                });
        }

        [Theory]
        [InlineData("8.1")]
        [InlineData("8.0")]
        [InlineData("7.4")]
        public async Task PhpFpmGreetingsAppTestAsync(string phpVersion)
        {
            // Arrange
            var appName = "greetings";
            var hostDir = Path.Combine(_hostSamplesDir, "php", appName);
            var volume = DockerVolume.CreateMirror(hostDir);
            var appDir = volume.ContainerDir;
            var appOutputDirVolume = CreateAppOutputDirVolume();
            var appOutputDir = appOutputDirVolume.ContainerDir;
            var buildScript = new ShellScriptBuilder()
               .AddCommand($"oryx build {appDir} -i /tmp/int -o {appOutputDir} " +
               $"--platform php --platform-version {phpVersion}")
               .ToString();
            var runScript = new ShellScriptBuilder()
                .AddCommand($"oryx create-script -appPath {appOutputDir} -output {RunScriptPath} -bindPort {ContainerPort}")
                .AddCommand("mkdir -p /home/site/wwwroot")
                .AddCommand($"cp -rf {appOutputDir}/* /home/site/wwwroot")
                .AddCommand(RunScriptPath)
                .ToString();

            var phpimageVersion = string.Concat(phpVersion, "-", "fpm");

            // Act & Assert
            await EndToEndTestHelper.BuildRunAndAssertAppAsync(
                appName, _output, new[] { volume, appOutputDirVolume },
                "/bin/sh", new[] { "-c", buildScript },
                _imageHelper.GetRuntimeImage("php", phpimageVersion),
                ContainerPort,
                "/bin/sh", new[] { "-c", runScript },
                async (hostPort) =>
                {
                    var output = await _httpClient.GetStringAsync($"http://localhost:{hostPort}/");
                    Assert.Contains("Hello World", output);
                    Assert.Contains("oryx oryx oryx", output);
                });
        }

        [Fact, Trait("category", "php-81")]
        public async Task CanBuildAndRun_Greetings_WithCustomizedRunCommand()
        {
            // Arrange
            var appName = "greetings";
            var phpVersion = "8.1";
            var phpimageVersion = string.Concat(phpVersion, "-", "fpm");
            var hostDir = Path.Combine(_hostSamplesDir, "php", appName);
            var tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tmpDir);
            try
            {
                var tmpVolume = DockerVolume.CreateMirror(tmpDir, true);
                var tmpContainerDir = tmpVolume.ContainerDir;
                var volume = DockerVolume.CreateMirror(hostDir);
                var appDir = volume.ContainerDir;
                var appOutputDirVolume = CreateAppOutputDirVolume();
                var appOutputDir = appOutputDirVolume.ContainerDir;
                var appsvcFile = appOutputDirVolume.ContainerDir + "/appsvc.yaml";
                var runCommand = "echo 'Hello Azure! New Feature!!'";
                var buildImageScript = new ShellScriptBuilder()
                   .AddDefaultTestEnvironmentVariables()
                   .AddCommand(
                    $"oryx build {appDir} -i /tmp/int --platform php " +
                    $"--platform-version {phpVersion} -o {appOutputDir}")
                   .ToString();
                var runtimeImageScript = new ShellScriptBuilder()
                    .CreateFile(appsvcFile, $"\"run: {runCommand}\"")
                    .AddCommand(
                    $"oryx create-script -appPath {appOutputDir} -bindPort {ContainerPort} -output {tmpContainerDir}/run.sh")
                    .AddCommand($".{tmpContainerDir}/run.sh")
                    .ToString();

                await EndToEndTestHelper.BuildRunAndAssertAppAsync(
                    appName,
                    _output,
                    new DockerVolume[] { volume, appOutputDirVolume, tmpVolume },
                    _imageHelper.GetGitHubActionsBuildImage(),
                    "/bin/sh",
                    new[]
                    {
                    "-c",
                    buildImageScript
                    },
                    _imageHelper.GetRuntimeImage("php", phpimageVersion),
                    ContainerPort,
                    "/bin/sh",
                    new[]
                    {
                    "-c",
                    runtimeImageScript
                    },
                    async (hostPort) =>
                    {
                        var output = await _httpClient.GetStringAsync($"http://localhost:{hostPort}/");
                        Assert.Contains("Hello World", output);
                        Assert.Contains("oryx oryx oryx", output);

                        var runScript = File.ReadAllText(Path.Combine(tmpDir, "run.sh"));
                        Assert.Contains(runCommand, runScript);
                    });
            }
            finally
            {
                Directory.Delete(tmpDir, true);
            }
        }
    }
}
