using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
using ComposableSettings.Benchmarks;

var config = ManualConfig.Create(DefaultConfig.Instance)
    .AddJob(Job.Default.WithToolchain(InProcessNoEmitToolchain.Instance));

BenchmarkRunner.Run<SettingsJsonIoBenchmarks>(config);
