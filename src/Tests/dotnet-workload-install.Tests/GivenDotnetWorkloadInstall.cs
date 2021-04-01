// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using ManifestReaderTests;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.Workload.Install.Tests
{
    public class GivenDotnetWorkloadInstall : SdkTest
    {
        private readonly BufferedReporter _reporter;
        private readonly string _manifestPath;

        public GivenDotnetWorkloadInstall(ITestOutputHelper log) : base(log)
        {
            _reporter = new BufferedReporter();
            _manifestPath = Path.Combine(_testAssetsManager.GetTestManifestsDirectory(), "SampleManifest", "Sample.json");
        }

        [Fact]
        public void WorkloadInstallManagerOrchestratesPackInstallation()
        {
            var mockWorkloadIds = new string[] { "xamarin-android" };
            var testDirectory = _testAssetsManager.CreateTestDirectory().Path;
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            var installer = new MockPackWorkloadInstaller();
            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(new[] { _manifestPath }), new string[] { dotnetRoot });
            var installManager = new WorkloadInstallManager(_reporter, installer, workloadResolver, "6.0.100");
            installManager.InstallWorkloads(mockWorkloadIds, true);

            installer.GarbageCollectionCalled.Should().BeTrue();
            installer.WorkloadInstallRecord.Should().BeEquivalentTo(mockWorkloadIds);
            installer.InstalledPacks.Count.Should().Be(8);
            installer.InstalledPacks.Where(pack => pack.Id.Contains("Android")).Count().Should().Be(8);
            _reporter.Lines.Contains(string.Format(LocalizableStrings.InstallationSucceeded, "xamarin-android"));
        }

        [Fact]
        public void WorkloadInstallManagerCanRollBackPackInstallation()
        {
            var mockWorkloadIds = new string[] { "xamarin-android", "xamarin-android-build" };
            var testDirectory = _testAssetsManager.CreateTestDirectory().Path;
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            var installer = new MockPackWorkloadInstaller(failingWorkload: "xamarin-android-build");
            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(new[] { _manifestPath }), new string[] { dotnetRoot });
            var installManager = new WorkloadInstallManager(_reporter, installer, workloadResolver, "6.0.100");
            try
            {
                installManager.InstallWorkloads(mockWorkloadIds, true);

                // Install should have failed
                true.Should().BeFalse();
            }
            catch (Exception e)
            {
                e.Message.Should().Be("Failing workload: xamarin-android-build");
                var expectedPacks = mockWorkloadIds
                    .SelectMany(workloadId => workloadResolver.GetPacksInWorkload(workloadId))
                    .Select(packId => workloadResolver.TryGetPackInfo(packId));
                installer.RolledBackPacks.ShouldBeEquivalentTo(expectedPacks);
                installer.WorkloadInstallRecord.Should().BeEmpty();
            }
        }
    }
}
