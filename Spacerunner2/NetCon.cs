using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;

namespace Spacerunner2
{
    struct Rpc
    {
        private readonly MethodBase _method;
        private readonly object[] _arguments;

        private Rpc(MethodBase method, params object[] arguments)
        {
            _method = method;
            _arguments = arguments;
        }

        public MethodBase Method
        {
            get { return _method; }
        }

        public object[] Arguments
        {
            get { return _arguments; }
        }

        public void Invoke(NetCon netCon, IPEndPoint sender)
        {
            var args = new object[_arguments.Length + 2];
            Array.Copy(_arguments, 0, args, 2, _arguments.Length);
            args[0] = netCon;
            args[1] = sender;

            var form = Form1.Fetch;
            if (form.InvokeRequired == false)
                _method.Invoke(null, args);
            else
                form.BeginInvoke((Func<object, object[], object>)_method.Invoke, null, args);
        }

        public static Rpc Create(Action<NetCon, IPEndPoint> action)
        {
            return new Rpc(action.Method);
        }

        public static Rpc Create<T1>(Action<NetCon, IPEndPoint, T1> action, T1 t1)
        {
            return new Rpc(action.Method, t1);
        }

        public static Rpc Create<T1, T2>(Action<NetCon, IPEndPoint, T1, T2> action, T1 t1, T2 t2)
        {
            return new Rpc(action.Method, t1, t2);
        }

        public static Rpc Create<T1, T2, T3>(Action<NetCon, IPEndPoint, T1, T2, T3> action, T1 t1, T2 t2, T3 t3)
        {
            return new Rpc(action.Method, t1, t2, t3);
        }

        public static Rpc Create<T1, T2, T3, T4>(Action<NetCon, IPEndPoint, T1, T2, T3, T4> action, T1 t1, T2 t2, T3 t3, T4 t4)
        {
            return new Rpc(action.Method, t1, t2, t3, t4);
        }

        public static Rpc Create<T1, T2, T3, T4, T5>(Action<NetCon, IPEndPoint, T1, T2, T3, T4, T5> action, T1 t1, T2 t2, T3 t3, T4 t4, T5 t5)
        {
            return new Rpc(action.Method, t1, t2, t3, t4, t5);
        }

        public static Rpc Create<T1, T2, T3, T4, T5, T6>(Action<NetCon, IPEndPoint, T1, T2, T3, T4, T5, T6> action, T1 t1, T2 t2, T3 t3, T4 t4, T5 t5, T6 t6)
        {
            return new Rpc(action.Method, t1, t2, t3, t4, t5, t6);
        }

        public static Rpc Create<T1, T2, T3, T4, T5, T6, T7>(Action<NetCon, IPEndPoint, T1, T2, T3, T4, T5, T6, T7> action, T1 t1, T2 t2, T3 t3, T4 t4, T5 t5, T6 t6, T7 t7)
        {
            return new Rpc(action.Method, t1, t2, t3, t4, t5, t6, t7);
        }

        public static Rpc Create<T1, T2, T3, T4, T5, T6, T7, T8>(Action<NetCon, IPEndPoint, T1, T2, T3, T4, T5, T6, T7, T8> action, T1 t1, T2 t2, T3 t3, T4 t4, T5 t5, T6 t6, T7 t7, T8 t8)
        {
            return new Rpc(action.Method, t1, t2, t3, t4, t5, t6, t7, t8);
        }

        public static Rpc Create<T1, T2, T3, T4, T5, T6, T7, T8, T9>(Action<NetCon, IPEndPoint, T1, T2, T3, T4, T5, T6, T7, T8, T9> action, T1 t1, T2 t2, T3 t3, T4 t4, T5 t5, T6 t6, T7 t7, T8 t8, T9 t9)
        {
            return new Rpc(action.Method, t1, t2, t3, t4, t5, t6, t7, t8, t9);
        }

        public static Rpc UnsafeCreate(MethodBase method, object[] args)
        {
            return new Rpc(method, args);
        }
    }

    static class Serializer
    {
        private static readonly Module Module = Assembly.GetExecutingAssembly().GetLoadedModules().Single();

        public static object InvokeReader(BinaryReader reader, Type type)
        {
            if (!type.IsArray)
                return ReaderMethods[type](reader);

            var elementType = type.GetElementType();
            var length = reader.ReadUInt16();
            var retval = (IList)Array.CreateInstance(elementType, length);
            for (var i = 0; i < length; i++)
                retval[i] = InvokeReader(reader, elementType);
            return retval;
        }

        public static void InvokeWriter(BinaryWriter writer, Type type, object value)
        {
            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                var arr = (Array)value;
                writer.Write((ushort)arr.Length);
                foreach (var element in arr)
                    InvokeWriter(writer, elementType, element);
            }
            else
                WriterMethods[type](writer, value);
        }

        private static readonly Dictionary<Type, Func<BinaryReader, object>> ReaderMethods =
            new Dictionary<Type, Func<BinaryReader, object>>
            {
                {typeof (bool), b => b.ReadBoolean()},
                {typeof (byte), b => b.ReadByte()},
                {typeof (char), b => b.ReadChar()},
                {typeof (decimal), b => b.ReadDecimal()},
                {typeof (double), b => b.ReadDouble()},
                {typeof (short), b => b.ReadInt16()},
                {typeof (int), b => b.ReadInt32()},
                {typeof (long), b => b.ReadInt64()},
                {typeof (sbyte), b => b.ReadSByte()},
                {typeof (float), b => b.ReadSingle()},
                {typeof (string), b => b.ReadString()},
                {typeof (ushort), b => b.ReadUInt16()},
                {typeof (uint), b => b.ReadUInt32()},
                {typeof (ulong), b => b.ReadUInt64()},
                {typeof (IPEndPoint), ReadIpEndPoint},
                {typeof (Rpc), b => ReadRpc(b)}
            };

        private static readonly Dictionary<Type, Action<BinaryWriter, object>> WriterMethods =
            new Dictionary<Type, Action<BinaryWriter, object>>
            {
                {typeof (bool), (b, a) => b.Write((bool) a)},
                {typeof (byte), (b, a) => b.Write((byte) a)},
                {typeof (char), (b, a) => b.Write((char) a)},
                {typeof (decimal), (b, a) => b.Write((decimal) a)},
                {typeof (double), (b, a) => b.Write((double) a)},
                {typeof (short), (b, a) => b.Write((short) a)},
                {typeof (int), (b, a) => b.Write((int) a)},
                {typeof (long), (b, a) => b.Write((long) a)},
                {typeof (sbyte), (b, a) => b.Write((sbyte) a)},
                {typeof (float), (b, a) => b.Write((float) a)},
                {typeof (string), (b, a) => b.Write((string) a)},
                {typeof (ushort), (b, a) => b.Write((ushort) a)},
                {typeof (uint), (b, a) => b.Write((uint) a)},
                {typeof (ulong), (b, a) => b.Write((ulong) a)},
                {typeof (IPEndPoint), WriteIpEndPoint},
                {typeof (Rpc), (b, a) => WriteRpc(b, (Rpc) a)}
            };

        private static IPEndPoint ReadIpEndPoint(BinaryReader reader)
        {
            var length = reader.ReadByte();
            if (length == 0)
                return null;
            var arr = reader.ReadBytes(length);
            return new IPEndPoint(new IPAddress(arr), reader.ReadUInt16());
        }

        private static void WriteIpEndPoint(BinaryWriter writer, object obj)
        {
            var address = (IPEndPoint)obj;
            if (address == null)
            {
                writer.Write((byte)0);
                return;
            }
            var arr = address.Address.GetAddressBytes();
            writer.Write((byte)arr.Length);
            writer.Write(arr);
            writer.Write((ushort)address.Port);
        }

        public static Rpc ReadRpc(BinaryReader reader)
        {
            var method = Module.ResolveMethod(reader.ReadInt32());
            var parameters = method.GetParameters();
            var args = new object[parameters.Length - 2];
            for (var i = 2; i < parameters.Length; i++)
                args[i - 2] = InvokeReader(reader, parameters[i].ParameterType);
            return Rpc.UnsafeCreate(method, args);
        }

        public static void WriteRpc(BinaryWriter writer, Rpc rpc)
        {
            writer.Write(rpc.Method.MetadataToken);
            var parameters = rpc.Method.GetParameters();
            for (var i = 2; i < parameters.Length; i++)
                InvokeWriter(writer, parameters[i].ParameterType, rpc.Arguments[i - 2]);
        }
    }

    class NetCon
    {
        private readonly List<TcpClient> _connections;
        private readonly byte[] _sendBuffer = new byte[512];
        private readonly BinaryWriter _sendBufferWriter;

        public NetCon()
        {
            _connections = new List<TcpClient>();
            _sendBufferWriter = new BinaryWriter(new MemoryStream(_sendBuffer));
        }

        public NetCon(int port)
            : this()
        {
            TcpListener host;
            while (true)
            {
                try
                {
                    host = new TcpListener(IPAddress.Any, port);
                    break;
                }
                catch
                {
                    Form1.Output("Couldn't bind to port " + port + ", cycling to next port");
                    port++;
                }
            }
            host.Start();
            host.BeginAcceptTcpClient(AcceptCallback, host);
            Form1.Output("Opened server on port " + port);
        }

        public NetCon(IPEndPoint endPoint)
            : this()
        {
            var client = new TcpClient();
            client.Connect(endPoint);
            _connections.Add(client);
            new Thread(ListenTcpClient) { IsBackground = true }.Start(client);
            Form1.Output("Connected to " + endPoint);
        }

        private void AcceptCallback(IAsyncResult result)
        {
            var host = (TcpListener)result.AsyncState;
            var client = host.EndAcceptTcpClient(result);
            _connections.Add(client);
            new Thread(ListenTcpClient) { IsBackground = true }.Start(client);
            host.BeginAcceptTcpClient(AcceptCallback, host);
        }

        private void ListenTcpClient(object clientObject)
        {
            var client = (TcpClient)clientObject;
            var reader = new BinaryReader(client.GetStream());
            var endpoint = (IPEndPoint)client.Client.RemoteEndPoint;
            while (client.Connected)
            {
                try
                {
                    var rpc = Serializer.ReadRpc(reader);
                    rpc.Invoke(this, (IPEndPoint)client.Client.RemoteEndPoint);
                }
                catch (Exception e)
                {
                    Form1.Output(string.Format("{0}> {1}: {2}", endpoint, e.GetType().Name, e.Message));
                }
            }
            _connections.Remove(client);
            Form1.Fetch.Invoke((Action)(() => Entity.RemoveObjectsByOwner(endpoint)));
            Form1.Output(string.Format("Disconnected {0}", client));
        }

        public void Send(IPEndPoint destination, Rpc rpc)
        {
            lock (_connections)
            {
                _sendBufferWriter.Seek(0, SeekOrigin.Begin);
                Serializer.WriteRpc(_sendBufferWriter, rpc);
                _connections.RemoveAll(t => !t.Connected);
                foreach (var connection in _connections)
                {
                    if (destination.Equals(connection.Client.RemoteEndPoint))
                    {
                        connection.Client.Send(_sendBuffer, (int)_sendBufferWriter.BaseStream.Position, SocketFlags.None);
                        return;
                    }
                }
                if (_connections.Count == 1)
                    Send((IPEndPoint)_connections[0].Client.RemoteEndPoint, Rpc.Create(ServerSend, destination, rpc));
                else if (_connections.Count != 0)
                    Form1.Output("Warning: Not connected to destination " + destination);
            }
        }

        private static void ServerSend(NetCon net, IPEndPoint sender, IPEndPoint destination, Rpc packet)
        {
            net.Send(destination, Rpc.Create(ClientFromServer, sender, packet));
        }

        private static void ClientFromServer(NetCon net, IPEndPoint server, IPEndPoint sender, Rpc packet)
        {
            packet.Invoke(net, sender);
        }

        public void SendOthers(Rpc packet)
        {
            var rpc = Rpc.Create(SendOthersGet, (IPEndPoint)null, packet);
            foreach (var connection in _connections)
                Send((IPEndPoint)connection.Client.RemoteEndPoint, rpc);
        }

        private static void SendOthersGet(NetCon net, IPEndPoint immediateSender, IPEndPoint originalSender, Rpc packet)
        {
            packet.Invoke(net, originalSender ?? immediateSender);
            var rpc = Rpc.Create(SendOthersGet, (IPEndPoint)null, packet);
            foreach (var connection in net._connections)
            {
                var rep = (IPEndPoint)connection.Client.RemoteEndPoint;
                if (!rep.Equals(immediateSender))
                    net.Send(rep, rpc);
            }
        }
    }
}