using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Windows.Forms;

namespace Spacerunner2
{
    interface IPointable
    {
        Vector2 Position { get; }
        Pen Pen { get; }
    }

    class Player : Entity, IPointable
    {
        [Flags]
        enum PlayerState
        {
            None = 0,
            Thrusting = 1,
            TurnLeft = 2,
            TurnRight = 4
        }

        public static bool FzooMode = false;

        private const float AccelSpeed = 60f;
        private const float RotSpeed = 3f;
        private const float Theta = 2.5f;
        private const float ShipSize = 8;
        private readonly IPEndPoint _owner;
        private readonly Pen _thrustPen = new Pen(Color.Red, 0);
        private readonly Pen _pen;
        private readonly Brush _brush;
        private readonly Field _field;
        private DateTime _lastNetwork = DateTime.UtcNow;
        private Vector2 _position;
        private Vector2 _velocity;
        private float _rotation;
        private PlayerState _playerState;
        private DateTime _lastShot;
        private int _bulletCharge;
        private int _score;

        public Player()
        {
            _owner = null;
            _pen = new Pen(Ext.ColorFromHsv(120, 0.75, 0.75));
            _brush = new SolidBrush(_pen.Color);
            _field = EntitiesOfType<Field>().Single();
            Respawn();
        }

        private Player(IPEndPoint owner)
        {
            _owner = owner;
            _pen = new Pen(Ext.ColorFromHsv((double)owner.GetHashCode() * 360 / int.MaxValue, 0.5, 0.5));
            _brush = new SolidBrush(_pen.Color);
            _field = EntitiesOfType<Field>().Single();
            Respawn();
        }

        protected override void Tick(NetCon netCon, Graphics graphics, Rectangle camera)
        {
            var now = DateTime.UtcNow;
            if (_owner == null)
            {
                _bulletCharge = Math.Min(_bulletCharge + 1, 100);
                _playerState = PlayerState.None;
                if (Keyboard.IsKeyDown(Keys.A))
                    _playerState |= PlayerState.TurnLeft;
                if (Keyboard.IsKeyDown(Keys.D))
                    _playerState |= PlayerState.TurnRight;
                if (Keyboard.IsKeyDown(Keys.W))
                    _playerState |= PlayerState.Thrusting;
                if (Keyboard.IsKeyDown(Keys.S) && _bulletCharge > 30 && (now - _lastShot).TotalSeconds > 0.2f)
                {
                    if (!FzooMode)
                        _bulletCharge -= 30;
                    var forwards = new Vector2((float)Math.Cos(_rotation), (float)Math.Sin(_rotation));
                    new Bullet(_position + forwards * ShipSize, _velocity + 100 * forwards, 3).Spawn();
                    _lastShot = now;
                }

                if ((now - _lastNetwork).TotalSeconds > 0.2)
                {
                    _lastNetwork = now;
                    netCon.SendOthers(Rpc.Create(UpdatePlayerFromNet, _position.X, _position.Y, _velocity.X, _velocity.Y, _rotation, (int)_playerState, now.Ticks, _score));
                }

                World.CameraCenter.X += (_position.X + _velocity.X * World.DeltaSeconds * 30 - World.CameraCenter.X) / 20;
                World.CameraCenter.Y += (_position.Y + _velocity.Y * World.DeltaSeconds * 30 - World.CameraCenter.Y) / 20;

                if (!FzooMode && CollisionDetection())
                    Respawn();

                graphics.DrawRectangle(Bullet.Pen, 10, 10, _bulletCharge, 10);

                var i = 0;
                foreach (var otherPlayer in EntitiesOfType<Player>())
                    graphics.DrawString((otherPlayer._owner == null ? "Self" : otherPlayer._owner.ToString()) + " score:" + otherPlayer._score, Form1.GlobalFont, otherPlayer._brush, 10, ++i * 10 + 10);

                List<Powerup> collected = null;
                foreach (var powerup in EntitiesOfType<Powerup>())
                {
                    var dist = (powerup.Position - _position).MagnitudeSquared;
                    if (dist < Powerup.Radius * Powerup.Radius)
                    {
                        switch (powerup.Type)
                        {
                            case Powerup.PowerupType.Points:
                                _score += 2;
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                        if (collected == null)
                            collected = new List<Powerup>();
                        collected.Add(powerup);
                    }
                }
                if (collected != null)
                    foreach (var powerup in collected)
                        powerup.Collect(netCon);

                var deadBullet = EntitiesOfType<Bullet>().FirstOrDefault(bullet => bullet.Owner != null && (bullet.Position - _position).MagnitudeSquared < ShipSize * ShipSize);

                if (deadBullet != null)
                {
                    netCon.Send(deadBullet.Owner, Rpc.Create(AddScore, 5));
                    deadBullet.NetworkDie(netCon);
                    Respawn();
                }
            }
            else
            {
                if ((now - _lastNetwork).TotalSeconds > 5)
                    Die();
            }

            if (_playerState.HasFlag(PlayerState.TurnLeft))
                _rotation -= RotSpeed * World.DeltaSeconds;
            if (_playerState.HasFlag(PlayerState.TurnRight))
                _rotation += RotSpeed * World.DeltaSeconds;
            if (_playerState.HasFlag(PlayerState.Thrusting))
                _velocity += new Vector2((float)Math.Cos(_rotation), (float)Math.Sin(_rotation)) * (FzooMode && _owner == null ? AccelSpeed * 10 : AccelSpeed) * World.DeltaSeconds;

            if (_position.X < 0)
                _velocity = new Vector2(Math.Abs(_velocity.X), _velocity.Y);
            if (_position.X > _field.BoardSize)
                _velocity = new Vector2(-Math.Abs(_velocity.X), _velocity.Y);
            if (_position.Y < 0)
                _velocity = new Vector2(_velocity.X, Math.Abs(_velocity.Y));
            if (_position.Y > _field.BoardSize)
                _velocity = new Vector2(_velocity.X, -Math.Abs(_velocity.Y));

            graphics.DrawPolygon(_pen, GetPlayerPoints(camera.X, camera.Y));
            if (_playerState.HasFlag(PlayerState.Thrusting))
            {
                graphics.DrawPolygon(_thrustPen, GetFlamePoints(camera.X, camera.Y));
                var backwards = new Vector2((float)Math.Cos(_rotation - Math.PI), (float)Math.Sin(_rotation - Math.PI));
                new Smoke(_field, _position + backwards * ShipSize, _velocity + 50 * backwards).Spawn();
            }

            _velocity *= 0.9999f;
            _position += _velocity * World.DeltaSeconds;
        }

        private static void AddScore(NetCon netCon, IPEndPoint sender, int incrementBy)
        {
            var player = EntitiesOfType<Player>().FirstOrDefault(p => p._owner == null);
            if (player != null)
                player._score += incrementBy;
        }

        private void Respawn()
        {
            _velocity = new Vector2(0, 0);
            _position = _field.FindRespawnPoint(-0.3f);
        }

        private bool CollisionDetection()
        {
            return GetPlayerPoints(0, 0).Any(point => _field[(int)point.X, (int)point.Y]);
        }

        private PointF[] GetPlayerPoints(int offsetX, int offsetY)
        {
            var center = new PointF(_position.X - offsetX, _position.Y - offsetY);
            var front = new PointF((float)Math.Cos(_rotation) * ShipSize, (float)Math.Sin(_rotation) * ShipSize);
            var left = new PointF((float)Math.Cos(_rotation + Theta) * ShipSize, (float)Math.Sin(_rotation + Theta) * ShipSize);
            var right = new PointF((float)Math.Cos(_rotation - Theta) * ShipSize, (float)Math.Sin(_rotation - Theta) * ShipSize);

            return new[] { center.Add(front), center.Add(left), center.Add(right) };
        }

        private PointF[] GetFlamePoints(int offsetX, int offsetY)
        {
            var center = new PointF(_position.X - offsetX, _position.Y - offsetY);
            var front = new PointF((float)Math.Cos(_rotation + Math.PI) * ShipSize * 1.5f, (float)Math.Sin(_rotation + Math.PI) * ShipSize * 1.5f);
            var left = new PointF((float)Math.Cos(_rotation + Theta + 0.3f) * ShipSize, (float)Math.Sin(_rotation + Theta + 0.3f) * ShipSize);
            var right = new PointF((float)Math.Cos(_rotation - Theta - 0.3f) * ShipSize, (float)Math.Sin(_rotation - Theta - 0.3f) * ShipSize);

            return new[] { center.Add(front), center.Add(left), center.Add(right) };
        }

        private static void UpdatePlayerFromNet(NetCon netCon, IPEndPoint sender, float posX, float posY, float velX, float velY, float rotation, int state, long ticks, int score)
        {
            var packetTransferTime = (float)(DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc)).TotalSeconds;
            var player = EntitiesOfType<Player>().FirstOrDefault(p => sender.Equals(p._owner));
            if (player == null)
            {
                player = new Player(sender);
                player.Spawn();
            }
            player._velocity = new Vector2(velX, velY);
            player._position = new Vector2(posX, posY) + player._velocity * packetTransferTime;
            player._rotation = rotation;
            player._playerState = (PlayerState)state;
            player._lastNetwork = DateTime.UtcNow;
            player._score = score;
        }

        public Vector2 Position
        {
            get { return _position; }
        }

        public Pen Pen
        {
            get { return _pen; }
        }

        protected override int DrawOrder
        {
            get { return 4; }
        }
    }
}