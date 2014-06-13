using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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

        public static bool FzooMode;

        private const float AccelSpeed = 60f;
        private const float RotSpeed = 0.5f;
        private const float RotDecline = 0.9f;
        private const float Theta = 2.5f;
        private const float ShipSize = 8;
        private readonly Pen _thrustPen = new Pen(Color.Red, 0);
        private readonly Pen _pen;
        private readonly Brush _brush;
        private readonly Field _field;
        private Vector2 _position;
        private Vector2 _velocity;
        private float _rotation;
        private float _rotationVelocity;
        private PlayerState _playerState;
        private DateTime _lastShot;
        private int _bulletCharge;
        private int _score;

        public Player()
        {
            _pen = new Pen(Ext.ColorFromHsv(120, 0.75, 0.75));
            _brush = new SolidBrush(_pen.Color);
            _field = EntitiesOfType<Field>().Single();
            Respawn();
        }

        protected override void Tick(Graphics graphics, Rectangle camera)
        {
            UpdateBulletCharge();
            DoKeybindings();
            CollisionDetection();
            DrawSelfUi(graphics);
            UpdateCamera();
            CollectPowerups();
            ApplyTurnAndThrust();
            BounceOffWalls();
            DrawPlayer(graphics, camera);
            ApplyPositionVelocity();
        }

        private void UpdateBulletCharge()
        {
            _bulletCharge = Math.Min(_bulletCharge + 1, 100);
        }

        private void DoKeybindings()
        {
            var now = DateTime.UtcNow;
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
        }

        private void UpdateCamera()
        {
            World.CameraCenter.X += (_position.X + _velocity.X * World.DeltaSeconds * 30 - World.CameraCenter.X) / 20;
            World.CameraCenter.Y += (_position.Y + _velocity.Y * World.DeltaSeconds * 30 - World.CameraCenter.Y) / 20;
        }

        private void DrawSelfUi(Graphics graphics)
        {
            graphics.DrawRectangle(Bullet.Pen, 10, 10, _bulletCharge, 10);

            graphics.DrawString("score:" + _score, Form1.GlobalFont, _brush, 10, 20);
        }

        private void CollectPowerups()
        {
            List<Powerup> collected = null;
            foreach (var powerup in EntitiesOfType<Powerup>().Where(powerup => GetPlayerPoints(0, 0).Any(pos => ((powerup.Position - pos)).MagnitudeSquared < Powerup.Radius * Powerup.Radius)))
            {
                switch (powerup.Type)
                {
                    case Powerup.PowerupType.Points:
                        _score += 2;
                        break;
                    default:
                        Form1.Output("Internal error: Unknown powerup type " + powerup.Type);
                        break;
                }
                if (collected == null)
                    collected = new List<Powerup>();
                collected.Add(powerup);
            }
            if (collected != null)
                foreach (var powerup in collected)
                    powerup.Collect();
        }

        private void ApplyTurnAndThrust()
        {
            if (_playerState.HasFlag(PlayerState.TurnLeft))
                _rotationVelocity -= RotSpeed;
            if (_playerState.HasFlag(PlayerState.TurnRight))
                _rotationVelocity += RotSpeed;
            _rotation += _rotationVelocity * World.DeltaSeconds;
            _rotationVelocity *= RotDecline;
            if (_playerState.HasFlag(PlayerState.Thrusting))
                _velocity += new Vector2((float)Math.Cos(_rotation), (float)Math.Sin(_rotation)) * (FzooMode ? AccelSpeed * 10 : AccelSpeed) * World.DeltaSeconds;
        }

        private void BounceOffWalls()
        {
            if (_position.X < 0)
                _velocity = new Vector2(Math.Abs(_velocity.X), _velocity.Y);
            if (_position.X > _field.BoardSize)
                _velocity = new Vector2(-Math.Abs(_velocity.X), _velocity.Y);
            if (_position.Y < 0)
                _velocity = new Vector2(_velocity.X, Math.Abs(_velocity.Y));
            if (_position.Y > _field.BoardSize)
                _velocity = new Vector2(_velocity.X, -Math.Abs(_velocity.Y));
        }

        private void DrawPlayer(Graphics graphics, Rectangle camera)
        {
            graphics.DrawPolygon(_pen, GetPlayerPoints(camera.X, camera.Y).Select(v => new PointF(v.X, v.Y)).ToArray());
            if (_playerState.HasFlag(PlayerState.Thrusting))
            {
                graphics.DrawPolygon(_thrustPen, GetFlamePoints(camera.X, camera.Y));
                var backwards = new Vector2((float)Math.Cos(_rotation - Math.PI), (float)Math.Sin(_rotation - Math.PI));
                new Smoke(_field, _position + backwards * ShipSize, _velocity + 50 * backwards).Spawn();
            }
        }

        private void ApplyPositionVelocity()
        {
            _velocity *= 0.9999f;
            _position += _velocity * World.DeltaSeconds;
        }

        private void Respawn()
        {
            _velocity = new Vector2(0, 0);
            _position = _field.FindRespawnPoint(-0.3f);
        }

        private void CollisionDetection()
        {
            if (FzooMode)
                return;
            foreach (var playerPoint in GetPlayerPoints(0, 0))
            {
                if (_field[playerPoint])
                {
                    var normal = _field.Normal(playerPoint);
                    var rotationalForce = Vector2.Dot(normal, Vector2.FromTheta((_position - playerPoint).Theta - (float)Math.PI / 2));
                    _rotationVelocity += rotationalForce * 3;
                    _velocity = normal * 5;
                }
            }
        }

        private IEnumerable<Vector2> GetPlayerPoints(int offsetX, int offsetY)
        {
            var center = new Vector2(_position.X - offsetX, _position.Y - offsetY);
            var front = new Vector2((float)Math.Cos(_rotation) * ShipSize, (float)Math.Sin(_rotation) * ShipSize);
            var left = new Vector2((float)Math.Cos(_rotation + Theta) * ShipSize, (float)Math.Sin(_rotation + Theta) * ShipSize);
            var right = new Vector2((float)Math.Cos(_rotation - Theta) * ShipSize, (float)Math.Sin(_rotation - Theta) * ShipSize);

            return new[] { center + front, center + left, center + right };
        }

        private PointF[] GetFlamePoints(int offsetX, int offsetY)
        {
            var center = new PointF(_position.X - offsetX, _position.Y - offsetY);
            var front = new PointF((float)Math.Cos(_rotation + Math.PI) * ShipSize * 1.5f, (float)Math.Sin(_rotation + Math.PI) * ShipSize * 1.5f);
            var left = new PointF((float)Math.Cos(_rotation + Theta + 0.3f) * ShipSize, (float)Math.Sin(_rotation + Theta + 0.3f) * ShipSize);
            var right = new PointF((float)Math.Cos(_rotation - Theta - 0.3f) * ShipSize, (float)Math.Sin(_rotation - Theta - 0.3f) * ShipSize);

            return new[] { center.Add(front), center.Add(left), center.Add(right) };
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