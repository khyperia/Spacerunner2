using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

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

    class NetCon
    {
        private const int SIO_UDP_CONNRESET = -1744830452;
        private static readonly Module Module = Assembly.GetExecutingAssembly().GetLoadedModules().Single();
        private readonly UdpClient _udpClient;
        private readonly byte[] _sendBuffer = new byte[512];
        private readonly BinaryWriter _sendBufferWriter;

        public NetCon(int port)
        {
            _sendBufferWriter = new BinaryWriter(new MemoryStream(_sendBuffer));
            for (var bindTry = 0; bindTry < 100; bindTry++)
            {
                try
                {
                    _udpClient = new UdpClient(port);
                    break;
                }
                catch
                {
                    Form1.Output("Couldn't bind to port " + port + ", cycling to next port");
                    port++;
                }
            }
            if (_udpClient == null)
            {
                Form1.Output("Could not bind to any port, network will not function");
            }
            else
            {
                if (Ext.IsMono == false)
                    HackAroundNetExceptions();
                _udpClient.BeginReceive(ReceiveCallback, null);
                Form1.Output("Opened stream on port " + port);
            }
        }

        private void HackAroundNetExceptions()
        {
            _udpClient.Client.IOControl((IOControlCode)SIO_UDP_CONNRESET, new byte[] { 0, 0, 0, 0 }, null); // http://stackoverflow.com/questions/5199026
        }

        private void ReceiveCallback(IAsyncResult result)
        {
            IPEndPoint remoteEp = null;
            byte[] bytes = null;
            try
            {
                bytes = _udpClient.EndReceive(result, ref remoteEp);
            }
            catch (Exception e)
            {
                Form1.Output("Exception on read - " + e.GetType().Name + ": " + e.Message);
            }

            if (bytes != null)
            {
                try
                {
                    var reader = new BinaryReader(new MemoryStream(bytes));
                    ProcessPacket(remoteEp, reader);
                }
                catch (Exception e)
                {
                    Form1.Output("Exception on packet process - " + e.GetType().Name + ": " + e.Message);
                }
            }

            try
            {
                _udpClient.BeginReceive(ReceiveCallback, null);
            }
            catch (Exception e)
            {
                Form1.Output("FATAL (network will no longer function): Exception on read - " + e.GetType().Name + ": " + e.Message);
            }
        }

        private void ProcessPacket(IPEndPoint sender, BinaryReader reader)
        {
            var rpc = ReadRpc(reader);
            rpc.Invoke(this, sender);
            Network.AddOrUpdate(this, sender);
        }

        private static object InvokeReader(BinaryReader reader, Type type)
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

        public void Send(IPEndPoint destination, Rpc rpc)
        {
            lock (_sendBuffer)
            {
                _sendBufferWriter.Seek(0, SeekOrigin.Begin);
                WriteRpc(_sendBufferWriter, rpc);
                try
                {
                    _udpClient.Send(_sendBuffer, (int)_sendBufferWriter.BaseStream.Position, destination);
                }
                catch (Exception e)
                {
                    Form1.Output("Exception on write - " + e.GetType().Name + ": " + e.Message);
                    Network.KillConnection(destination);
                }
            }
        }

        private static void InvokeWriter(BinaryWriter writer, Type type, object value)
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

        private static Rpc ReadRpc(BinaryReader reader)
        {
            var method = Module.ResolveMethod(reader.ReadInt32());
            var parameters = method.GetParameters();
            var args = new object[parameters.Length - 2];
            for (var i = 2; i < parameters.Length; i++)
                args[i - 2] = InvokeReader(reader, parameters[i].ParameterType);
            if (reader.BaseStream.Position != reader.BaseStream.Length)
                throw new Exception("Invalid RPC packet length");
            return Rpc.UnsafeCreate(method, args);
        }

        private static void WriteRpc(BinaryWriter writer, Rpc rpc)
        {
            writer.Write(rpc.Method.MetadataToken);
            var parameters = rpc.Method.GetParameters();
            for (var i = 2; i < parameters.Length; i++)
                InvokeWriter(writer, parameters[i].ParameterType, rpc.Arguments[i - 2]);
        }
    }
}