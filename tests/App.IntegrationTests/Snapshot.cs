using System.Runtime.CompilerServices;

namespace App.IntegrationTests;

/// <summary>
/// Golden-master comparison against a file under <c>Snapshots/</c>, committed with the code. On mismatch the
/// actual value is written next to it as <c>*.received.json</c> (git-ignored) and the test fails; to accept a
/// deliberate contract change, replace the snapshot with the received file. Set <c>UPDATE_SNAPSHOTS=1</c> to
/// rewrite snapshots in place.
/// </summary>
internal static class Snapshot
{
    public static void Match(string name, string actual, [CallerFilePath] string callerFile = "")
    {
        var directory = Path.Combine(Path.GetDirectoryName(callerFile)!, "Snapshots");
        var expectedPath = Path.Combine(directory, name + ".json");
        var normalisedActual = actual.ReplaceLineEndings("\n").TrimEnd() + "\n";

        if (Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS") == "1" || !File.Exists(expectedPath))
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(expectedPath, normalisedActual);
            Assert.Fail($"Snapshot {name} was (re)written; review it, commit it, and run again.");
        }

        var expected = File.ReadAllText(expectedPath).ReplaceLineEndings("\n").TrimStart('﻿').TrimEnd() + "\n";
        if (expected == normalisedActual)
        {
            File.Delete(Path.Combine(directory, name + ".received.json"));
            return;
        }

        File.WriteAllText(Path.Combine(directory, name + ".received.json"), normalisedActual);
        Assert.Equal(expected, normalisedActual);
    }
}
