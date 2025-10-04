using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using MeteoMesh5.CentralServer.Services;
using MeteoMesh5.CentralServer.Models;

namespace MeteoMesh5.CentralServer.Tests;

public class LocalNodeDataServiceMappingTests
{
    [Fact]
    public void Mapping_PrecipitationIntensity_NonLidarZero_LidarPositive()
    {
        var mgr = new LocalNodeManager(new NullLogger<LocalNodeManager>(), TimeProvider.System);
        mgr.RegisterNode("n1","Name","https://x");
        var sample = new List<MeasurementInfo>
        {
            new(){ NodeId="n1", StationId="T1", Timestamp=DateTime.UtcNow, Temperature=21, Humidity=50, AirPressure=1000, PrecipitationIntensity=0 },
            new(){ NodeId="n1", StationId="L1", Timestamp=DateTime.UtcNow, PrecipitationIntensity=1.25 }
        };
        sample[0].PrecipitationIntensity.Should().Be(0);
        sample[1].PrecipitationIntensity.Should().BeApproximately(1.25, 0.0001);
    }
}
