using System.Drawing;
using System.Linq;

namespace Spacerunner2
{
    class Bullet : Entity
    {
        public static readonly Pen Pen = new Pen(Color.Orange);
        private readonly Field _field;
        private readonly float _width;
        private Vector2 _position;
        private readonly Vector2 _velocity;

        public Bullet(Vector2 position, Vector2 velocity, float width)
        {
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

        protected override void Tick(Graphics graphics, Rectangle camera)
        {
            _position += _velocity * World.DeltaSeconds;
            graphics.DrawEllipse(Pen, _position.X - _width / 2.0f - camera.X, _position.Y - _width / 2.0f - camera.Y, _width, _width);
            if (_field.IsOnGrid(_position) == false || _field[(int)_position.X, (int)_position.Y])
                Die();
        }
    }
}