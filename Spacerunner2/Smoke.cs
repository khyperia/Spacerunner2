using System.Drawing;

namespace Spacerunner2
{
    class Smoke : Entity
    {
        private static readonly Pen Pen = new Pen(Color.FromArgb(30,30,30));
        private readonly Vector2 _velocity;
        private readonly Field _field;
        private Vector2 _position;
        private int _frame;

        public Smoke(Field field, Vector2 position, Vector2 velocity)
        {
            _field = field;
            _position = position;
            _velocity = velocity;
        }

        protected override void Tick(Graphics graphics, Rectangle camera)
        {
            _position += _velocity * World.DeltaSeconds;
            var width = (_frame++ + 10) / 10.0f;
            graphics.DrawEllipse(Pen, _position.X - width / 2.0f - camera.X, _position.Y - width / 2.0f - camera.Y, width, width);
            if (_frame > 100 || _field[(int)_position.X, (int)_position.Y])
                Die();
        }

        protected override int DrawOrder
        {
            get { return 1; }
        }
    }
}