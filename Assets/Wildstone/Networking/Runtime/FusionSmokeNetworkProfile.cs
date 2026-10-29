#if WILDSTONE_SMOKE
using System;
using System.Collections.Generic;
using System.Reflection;
using Fusion;
using Fusion.Sockets;

namespace Wildstone.Networking
{
    public static class FusionSmokeNetworkProfile
    {
        [Serializable] public sealed class Report
        {
            public string profile, sdkBuildType;
            public bool injected;
            public int appliedCount;
            public int configuredAddedRttMs, configuredOneWayJitterMs;
            public double configuredLossProbabilityPerDirection, delayMinSeconds, delayMaxSeconds;
        }

        public static Report Configuration { get; private set; }

        public static void ConfigureFromArgs(Dictionary<string,string> args)
        {
            Configuration = null;
            if (!args.TryGetValue("--wildstone-smoke-network-profile", out var profile)) return;
            if (profile != "baseline" && (!args.TryGetValue("--wildstone-smoke-scenario", out var scenario) ||
                (scenario != "laser-moving-cover" && scenario != "projectile-combat")))
                throw new ArgumentException("Injected network profiles are supported only for laser-moving-cover and projectile-combat.");
            var report = new Report { profile = profile, sdkBuildType = NetworkRunner.BuildType.ToString() };
            switch (profile)
            {
                case "baseline": break;
                case "regional80":
                    report.configuredAddedRttMs = 80;
                    report.configuredOneWayJitterMs = 10;
                    report.configuredLossProbabilityPerDirection = .01;
                    break;
                case "adverse150":
                    report.configuredAddedRttMs = 150;
                    report.configuredOneWayJitterMs = 20;
                    report.configuredLossProbabilityPerDirection = .02;
                    break;
                default: throw new ArgumentException("Unknown smoke network profile: " + profile);
            }
            report.injected = report.configuredAddedRttMs > 0;
            if (report.injected && NetworkRunner.BuildType != NetworkRunner.BuildTypes.Debug)
                throw new InvalidOperationException("Smoke network injection requires Fusion Debug DLLs; Release DLLs disable simulation.");
            report.delayMinSeconds = (report.configuredAddedRttMs - 2 * report.configuredOneWayJitterMs) / 1000.0;
            report.delayMaxSeconds = (report.configuredAddedRttMs + 2 * report.configuredOneWayJitterMs) / 1000.0;
            Configuration = report;
        }

        public static NetworkProjectConfig Apply(NetworkProjectConfig source)
        {
            var report = Configuration;
            if (report == null) return source;
            // SDK Copy is internal; preserve its nonserialized prefab table and execution-order data.
            var clone = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
            if (clone == null) throw new InvalidOperationException("Cannot clone smoke network configuration.");
            var copy = (NetworkProjectConfig)clone.Invoke(source, null);
            // Fusion halves these RTT delays and applies the resulting seconds on receive at each peer.
            copy.NetworkConditions = new NetworkSimulationConfiguration
            {
                Enabled = report.injected,
                DelayShape = NetConfigSimulationOscillator.WaveShape.Noise,
                DelayMin = report.delayMinSeconds,
                DelayMax = report.delayMaxSeconds,
                AdditionalJitter = 0,
                LossChanceShape = NetConfigSimulationOscillator.WaveShape.Noise,
                LossChanceMin = report.configuredLossProbabilityPerDirection,
                LossChanceMax = report.configuredLossProbabilityPerDirection,
                AdditionalLoss = 0
            };
            report.appliedCount++;
            return copy;
        }
    }
}
#endif
