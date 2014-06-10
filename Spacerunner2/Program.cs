using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace Spacerunner2
{
    static class Program
    {
        static void Main()
        {
            Application.Run(new Form1());
        }
    }

    static class Keyboard
    {
        public static bool IsKeyDown(Keys key)
        {
            return Form1.Fetch.KeyState(key);
        }
    }

    class Form1 : Form
    {
        private static Form1 _fetch;

        public static Form1 Fetch { get { return _fetch; } }
        public static readonly Font GlobalFont = new Font("Consolas", 10);

        private readonly Dictionary<Keys, bool> _keyStates = new Dictionary<Keys, bool>();
        private static readonly Color BackgroundColor = Color.FromArgb(10, 10, 20);
        private NetCon _netCon;
        private static readonly List<Tuple<string, DateTime>> OutputBuffer = new List<Tuple<string, DateTime>>();
        private readonly Pen _inputBufferPen = new Pen(Color.DeepPink);
        private readonly Brush _inputBufferBrush = new SolidBrush(Color.DeepPink);
        private string _inputBuffer;
        private bool _inputOpen;
        private const int DefaultPort = 34567;

        public Form1()
        {
            _fetch = this;
            base.Text = "Spacerunner 2";
            base.DoubleBuffered = true;
            ClientSize = new Size(500, 500);

            _inputBuffer = "";
            StartTimer(1000 / 60, (o, a) => Invalidate());

            ThreadPool.QueueUserWorkItem(o =>
                                         {
                                             new Field().Spawn(null);
                                             new Player().Spawn(null);
                                             new Powerup(Powerup.PowerupType.Points).Spawn(null);
                                             _netCon = new NetCon();
                                             Output("Ready to play, press T to open input, /help for help");
                                         });
            Output("Generating field");
        }

        private static void StartTimer(int interval, EventHandler tick)
        {
            var timer = new Timer { Interval = interval };
            timer.Tick += tick;
            timer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            World.Tick();
            var graphics = e.Graphics;
            graphics.Clear(BackgroundColor);
            var csize = ClientSize;
            if (_netCon != null)
                Entity.TickAll(_netCon, graphics, csize);
            if (_inputOpen)
                graphics.DrawRectangle(_inputBufferPen, 5, csize.Height - 20, csize.Width - 10, 15);
            OutputBuffer.RemoveAll(o => (DateTime.UtcNow - o.Item2).TotalSeconds > 20);
            var i = OutputBuffer.Count;
            foreach (var tuple in OutputBuffer)
                graphics.DrawString(tuple.Item1, GlobalFont, _inputBufferBrush, 7, csize.Height - i-- * 14 - 30);
            graphics.DrawString(_inputBuffer, GlobalFont, _inputBufferBrush, 7, csize.Height - 20);
            base.OnPaint(e);
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            var keyChar = e.KeyChar;
            if (_inputOpen == false && keyChar == 't')
                _inputOpen = true;
            else if (_inputOpen)
            {
                if (keyChar == '\r' || keyChar == '\n')
                {
                    DoInput(_inputBuffer);
                    _inputBuffer = "";
                    _inputOpen = false;
                }
                else if (keyChar == '\b')
                {
                    if (_inputBuffer.Length > 0)
                        _inputBuffer = _inputBuffer.Substring(0, _inputBuffer.Length - 1);
                }
                else
                    _inputBuffer += keyChar;
            }
            base.OnKeyPress(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (_inputOpen)
            {
                if (e.KeyCode == Keys.Escape)
                    _inputOpen = false;
            }
            else
            {
                if (e.KeyCode != Keys.T)
                    _keyStates[e.KeyCode] = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            _keyStates[e.KeyCode] = false;
            base.OnKeyUp(e);
        }

        public bool KeyState(Keys key)
        {
            return _keyStates.ContainsKey(key) && _keyStates[key];
        }

        public static void Output(string s)
        {
            Action<string> action = m => OutputBuffer.Add(Tuple.Create(m, DateTime.UtcNow));
            if (_fetch.InvokeRequired)
                _fetch.BeginInvoke(action, s);
            else
                action(s);
        }

        private void DoInput(string input)
        {
            if (input.StartsWith("/"))
            {
                var command = input.Substring(1).Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                switch (command[0])
                {
                    case "connect":
                        if (_netCon == null)
                        {
                            Output("Field not created; wait for map to generate first");
                            break;
                        }
                        IPEndPoint address;
                        if (command.Length != 2 || Ext.TryParseIpEndPoint(command[1], DefaultPort, out address) == false)
                            Output("Syntax: /connect [ip]:[optional port]");
                        else
                            _netCon = new NetCon(address);
                        break;
                    case "host":
                        _netCon = new NetCon(DefaultPort);
                        break;
                    case "fzoo":
                        Player.FzooMode = !Player.FzooMode;
                        Output(Player.FzooMode ? "Fzoo <3" : "Fzoo </3");
                        break;
                    case "yay":
                        Player.DestressMode = !Player.DestressMode;
                        Output(Player.DestressMode ? "Destress mode, have fun!" : "Be careful out there!");
                        break;
                    case "help":
                        Output("A/D to rotate, W to thrust, S to shoot");
                        Output("Don't hit the white stuff, you'll die (and respawn)");
                        Output("To connect to multiplayer, /connect [ip address]");
                        break;
                }
            }
            else if (_netCon != null)
                _netCon.SendOthers(Rpc.Create(MessageGet, input));
        }

        private static void MessageGet(NetCon netCon, IPEndPoint endPoint, string message)
        {
            Output("<" + endPoint.Address + "> " + message);
        }
    }
}
