using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Spacerunner2
{
    static class Network
    {
        private static readonly ConcurrentDictionary<IPEndPoint, DateTime> ProbedIps = new ConcurrentDictionary<IPEndPoint, DateTime>();

        public static void Probe(NetCon netCon)
        {
            Trim();
            foreach (var dest in ProbedIps)
                netCon.Send(dest.Key, Rpc.Create(AskProbe));
        }

        public static void KillConnection(IPEndPoint endPoint)
        {
            DateTime value;
            ProbedIps.TryRemove(endPoint, out value);
        }

        private static void Trim()
        {
            foreach (var ip in ProbedIps.Where(ip => (DateTime.UtcNow - ip.Value).TotalSeconds > 5))
            {
                KillConnection(ip.Key);
                Form1.Output(ip.Key + " has quit");
            }
        }

        public static void AddOrUpdate(NetCon netCon, IPEndPoint endPoint)
        {
            ProbedIps.AddOrUpdate(endPoint, ipep =>
            {
                netCon.Send(endPoint, Rpc.Create(AskProbe));
                Form1.Output(endPoint + " has joined");
                return DateTime.UtcNow;
            }, (ipep, oldTime) => DateTime.UtcNow);
        }

        private static void AskProbe(NetCon netCon, IPEndPoint sender)
        {
            AddOrUpdate(netCon, sender);
            var array = ProbedIps.Select(kvp => kvp.Key).Where(ip => ip.Equals(sender) == false).ToArray();
            netCon.Send(sender, Rpc.Create(RespondProbe, array));
        }

        private static void RespondProbe(NetCon netCon, IPEndPoint sender, IPEndPoint[] response)
        {
            AddOrUpdate(netCon, sender);
            foreach (var endPoint in response)
                AddOrUpdate(netCon, endPoint);
        }

        public static void SendOthers(this NetCon netCon, Rpc rpc)
        {
            foreach (var probedIp in ProbedIps)
                netCon.Send(probedIp.Key, rpc);
        }
    }

    static class GuaranteedPacket
    {
        private static readonly List<Event> Events = new List<Event>();
        private static int _currentEventId;

        public static void SendOthersGuaranteed(this NetCon netCon, Rpc rpc)
        {
            netCon.SendOthers(Rpc.Create(OnEvent, ++_currentEventId, rpc));
        }

        private static void OnEvent(NetCon netCon, IPEndPoint sender, int id, Rpc rpc)
        {
            Events.Add(new Event(sender, id, rpc));
            rpc.Invoke(netCon, sender);
        }

        public static void SendEnsurePacket(NetCon netCon)
        {
            var toSend = Events.Where(e => e.Sender == null).OrderBy(e => e.TimeCreated).Take(255).ToList();
            Events.RemoveAll(e => toSend.Contains(e) == false);
            var arrToSend = toSend.Select(e => e.Id).ToArray();
            netCon.SendOthers(Rpc.Create(EnsurePackets, arrToSend));
        }

        private static void EnsurePackets(NetCon netCon, IPEndPoint sender, int[] ids)
        {
            var bySender = Events.Where(e => sender.Equals(e.Sender)).Select(e => e.Id).ToList();
            var unsent = ids.Where(i => bySender.Contains(i) == false).ToArray();
            Events.RemoveAll(e => ids.Contains(e.Id) == false);
            if (unsent.Length != 0)
                RequestPackets(netCon, sender, unsent);
        }

        private static void RequestPackets(NetCon netCon, IPEndPoint sender, int[] ids)
        {
            var toSend = Events.Where(e => sender.Equals(e.Sender) && ids.Contains(e.Id));
            foreach (var e in toSend)
                netCon.SendOthers(Rpc.Create(OnEvent, e.Id, e.Rpc));
        }

        private struct Event : IEquatable<Event>
        {
            private readonly DateTime _created;
            private readonly IPEndPoint _sender;
            private readonly int _id;
            private readonly Rpc _rpc;

            public Event(IPEndPoint sender, int id, Rpc rpc)
            {
                _created = DateTime.UtcNow;
                _sender = sender;
                _id = id;
                _rpc = rpc;
            }

            public DateTime TimeCreated
            {
                get { return _created; }
            }

            public IPEndPoint Sender
            {
                get { return _sender; }
            }

            public int Id
            {
                get { return _id; }
            }

            public Rpc Rpc
            {
                get { return _rpc; }
            }

            public bool Equals(Event other)
            {
                return Equals(_sender, other._sender) && _id == other._id;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj))
                    return false;
                return obj is Event && Equals((Event) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((_sender != null ? _sender.GetHashCode() : 0) * 397) ^ _id;
                }
            }
        }
    }
}
