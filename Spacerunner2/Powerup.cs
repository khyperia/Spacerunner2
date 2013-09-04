using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;

namespace Spacerunner2
{
    class Powerup : Entity, IPointable
    {
        public enum PowerupType
        {
            Points
        }

        public const float Radius = 10.0f;

        private static readonly Dictionary<PowerupType, Pen> Pens = new Dictionary<PowerupType, Pen>
                                                                    {
                                                                        {PowerupType.Points, new Pen(Color.BlueViolet)}
                                                                    };

        private static int _powerupIdCounter;

        private readonly int _powerupId;
        private readonly IPEndPoint _owner;
        private readonly PowerupType _type;
        private Vector2 _location;
        private DateTime _lastNetwork;

        public Powerup(PowerupType type)
        {
            _owner = null;
            _powerupId = _powerupIdCounter++;
            _type = type;
            _location = EntitiesOfType<Field>().Single().FindRespawnPoint(-0.7f);
        }

        private Powerup(IPEndPoint owner, int powerupId, PowerupType type, Vector2 location)
        {
            _owner = owner;
            _powerupId = powerupId;
            _type = type;
            _location = location;
        }

        public PowerupType Type
        {
            get { return _type; }
        }

        public Vector2 Position
        {
            get { return _location; }
        }

        public Pen Pen { get { return Pens[_type]; }}

        protected override int DrawOrder
        {
            get { return 0; }
        }

        protected override void Tick(NetCon netCon, Graphics graphics, Rectangle camera)
        {
            var now = DateTime.UtcNow;
            if (_owner == null && (now - _lastNetwork).TotalSeconds > 5)
            {
                _lastNetwork = now;
                netCon.SendOthers(Rpc.Create(UpdateLocation, _powerupId, (int)_type, _location.X, _location.Y));
            }
            graphics.DrawEllipse(Pens[_type], _location.X - Radius - camera.X, _location.Y - Radius - camera.Y, Radius * 2, Radius * 2);
        }

        public void Collect(NetCon netCon)
        {
            if (_owner == null)
                Respawn(netCon);
            else
            {
                netCon.Send(_owner, Rpc.Create(Respawn, _powerupId));
                Die();
            }
        }

        private static void Respawn(NetCon netCon, IPEndPoint sender, int powerupId)
        {
            var powerup = EntitiesOfType<Powerup>().FirstOrDefault(p => p._owner == null && p._powerupId == powerupId);
            if (powerup != null)
                powerup.Respawn(netCon);
        }

        private void Respawn(NetCon netCon)
        {
            _location = EntitiesOfType<Field>().Single().FindRespawnPoint(-0.7f);
            netCon.SendOthersGuaranteed(Rpc.Create(UpdateLocation, _powerupId, (int)_type, _location.X, _location.Y));
        }

        private static void UpdateLocation(NetCon netCon, IPEndPoint sender, int powerupId, int type, float x, float y)
        {
            var powerup = EntitiesOfType<Powerup>().FirstOrDefault(p => sender.Equals(p._owner) && p._powerupId == powerupId);
            if (powerup == null)
            {
                powerup = new Powerup(sender, powerupId, (PowerupType)type, new Vector2(x, y));
                powerup.Spawn();
            }
            else
                powerup._location = new Vector2(x, y);
        }
    }
}