using System.Collections.Generic;
using System.Drawing;
using System.Linq;

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

        private readonly PowerupType _type;
        private Vector2 _location;

        public Powerup(PowerupType type)
        {
            _type = type;
            _location = EntitiesOfType<Field>().Single().FindRespawnPoint(-0.7f);
        }

        public PowerupType Type
        {
            get { return _type; }
        }

        public Vector2 Position
        {
            get { return _location; }
        }

        public Pen Pen { get { return Pens[_type]; } }

        protected override int DrawOrder
        {
            get { return 0; }
        }

        protected override void Tick(Graphics graphics, Rectangle camera)
        {
            graphics.DrawEllipse(Pens[_type], _location.X - Radius - camera.X, _location.Y - Radius - camera.Y, Radius * 2, Radius * 2);
        }

        public void Collect()
        {
            Respawn();
        }

        private void Respawn()
        {
            _location = EntitiesOfType<Field>().Single().FindRespawnPoint(-0.8f);
        }
    }
}