using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using MeteoMesh5.CentralServer.Services;

namespace MeteoMesh5.CentralServer.Tests;

public class LocalNodeManagerConcurrencyTests
{
    [Fact]
    public async Task ConcurrentRegistration_IsThreadSafe()
    {
        var mgr = new LocalNodeManager(new NullLogger<LocalNodeManager>(), TimeProvider.System);
        var tasks = Enumerable.Range(0, 200).Select(i => Task.Run(() =>
        {
            mgr.RegisterNode($"node{i%25}", $"Name{i}", $"https://n{i%25}");
        }));
        await Task.WhenAll(tasks);
        mgr.GetTotalNodeCount().Should().Be(25); // only 25 distinct ids
    }
}
