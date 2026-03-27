using System;
using System.Linq;
using Network.NetworkTransport;
using NUnit.Framework;

namespace Tests.EditMode.Network
{
    public class TransportArchitectureTests
    {
        [Test]
        public void KcpTransport_ImplementsITransport()
        {
            Assert.That(typeof(ITransport).IsAssignableFrom(typeof(KcpTransport)), Is.True);
        }

        [Test]
        public void ReliableUdpTransport_IsNotAvailable()
        {
            var reliableUdpTransportType = AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType("Network.NetworkTransport.ReliableUdpTransport", throwOnError: false))
                .FirstOrDefault(type => type != null);

            Assert.That(reliableUdpTransportType, Is.Null);
        }
    }
}
