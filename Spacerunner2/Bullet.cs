using System;
using System.Drawing;
using System.Linq;
using System.Net;

namespace Spacerunner2
{
    class Bullet : Entity
    {
        public static readonly Pen Pen = new Pen(Color.Orange);
        private static int _bulletIdCounter;
        private readonly int _bulletId;
        private readonly Field _field;
        private readonly float _width;
        private Vector2 _position;
        private Vector2 _velocity;
        private DateTime _lastNetwork;

        public Bullet(Vector2 position, Vector2 velocity, float width)
        {
            Owner = null;
            _bulletId = _bulletIdCounter++;
            _field = EntitiesOfType<Field>().Single();
            _position = position;
            _velocity = velocity;
            _width = width;
        }

        private Bullet(IPEndPoint owner, Vector2 position, Vector2 velocity, float width, int bulletId)
        {
            Owner = owner;
            _bulletId = bulletId;
            _field = EntitiesOfType<Field>().Single();
            _position = position;
            _velocity = velocity;
            _width = width;
        }

        public Vector2 Position { get { return _position; }}

        protected override int DrawOrder
        {
            get { return 1; }
        }

        protected override void Tick(NetCon netCon, Graphics graphics, Rectangle camera)
        {
            _position += _velocity * World.DeltaSeconds;
            graphics.DrawEllipse(Pen, _position.X - _width / 2.0f - camera.X, _position.Y - _width / 2.0f - camera.Y, _width, _width);
            if (_field.IsOnGrid(_position) == false || _field[(int)_position.X, (int)_position.Y])
                Die();
            var now = DateTime.UtcNow;
            if (Owner == null)
            {
                if ((now - _lastNetwork).TotalSeconds > 1)
                    netCon.SendOthers(Rpc.Create(UpdateFromNetwork, _position.X, _position.Y, _velocity.X, _velocity.Y, _width, _bulletId));
            }
            else
            {
                if ((now - _lastNetwork).TotalSeconds > 3)
                    Die();
            }
        }

        public void NetworkDie(NetCon netCon)
        {
            netCon.SendOthers(Rpc.Create(DeathFromNetwork, Owner, _bulletId));
            Die();
        }

        private static void DeathFromNetwork(NetCon netCon, IPEndPoint sender, IPEndPoint owner, int bulletId)
        {
            var realOwner = owner ?? sender;
            var bullet = EntitiesOfType<Bullet>().FirstOrDefault(p => realOwner.Equals(p.Owner) && p._bulletId == bulletId);
            if (bullet != null)
                bullet.Die();
        }

        private static void UpdateFromNetwork(NetCon netCon, IPEndPoint sender, float posX, float posY, float velX, float velY, float width, int bulletId)
        {
            var pos = new Vector2(posX, posY);
            var vel = new Vector2(velX, velY);
            var bullet = EntitiesOfType<Bullet>().FirstOrDefault(p => sender.Equals(p.Owner) && p._bulletId == bulletId);
            if (bullet == null)
            {
                bullet = new Bullet(sender, pos, vel, width, bulletId);
                bullet.Spawn(sender);
            }
            else
            {
                bullet._position = pos;
                bullet._velocity = vel;
            }
            bullet._lastNetwork = DateTime.UtcNow;
        }
    }
}