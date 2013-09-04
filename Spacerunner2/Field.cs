using System;
using System.Drawing;

namespace Spacerunner2
{
    class Field : Entity
    {
        private int _seed = 654321; //Ext.Rand.Next();
        private int _boardSize = 2000;
        private int _scale = 200;
        private float _threshHold = 0.3f;
        private float[,] _board;
        private Bitmap _boardBitmap;

        public Field()
        {
            _board = GenerateBoard();
            _boardBitmap = GenerateBoardBrush();
        }

        public bool IsOnGrid(Vector2 vector)
        {
            return vector.X >= 0 && vector.X < _boardSize && vector.Y >= 0 && vector.Y < _boardSize;
        }

        public int BoardSize { get { return _boardSize; } }

        public bool this[int x, int y]
        {
            get
            {
                return x >= 0 && x < _boardSize && y >= 0 && y < _boardSize && _board[x, y] > _threshHold;
            }
        }

        private float[,] GenerateBoard()
        {
            var noise = new SimplexNoise(_seed);
            var retval = new float[_boardSize, _boardSize];

            for (var y = 0; y < _boardSize; y++)
                for (var x = 0; x < _boardSize; x++)
                    retval[x, y] = noise.Generate((float)x / _scale, (float)y / _scale);

            return retval;
        }

        public Vector2 FindRespawnPoint(float threshHold)
        {
            threshHold = Math.Min(threshHold, _threshHold);
            Vector2 position;
            do
            {
                position = new Vector2((float)(Ext.Rand.NextDouble() * _boardSize), (float)(Ext.Rand.NextDouble() * _boardSize));
            } while (_board[(int)position.X, (int)position.Y] > threshHold);
            return position;
        }

        private Bitmap GenerateBoardBrush()
        {
            var bmp = new Bitmap(_boardSize, _boardSize);

            for (var y = 0; y < _boardSize; y++)
            {
                for (var x = 0; x < _boardSize; x++)
                {
                    var value = _board[x, y];
                    if (value < _threshHold)
                        continue;
                    bmp.SetPixel(x, y, Color.FromArgb((byte)(value * 256), (byte)(value * 256), (byte)(value * 256)));
                }
            }

            return bmp;
        }

        protected override int DrawOrder
        {
            get { return 2; }
        }

        protected override void Tick(NetCon netCon, Graphics graphics, Rectangle camera)
        {
            graphics.DrawImageUnscaled(_boardBitmap, -camera.X, -camera.Y);

            var center = camera.Center();
            var screencenter = new Vector2(camera.Width / 2.0f, camera.Height / 2.0f);
            foreach (var pointable in EntitiesOfType<IPointable>())
            {
                var delta = pointable.Position - center;
                var dist = Math.Min(camera.Width, camera.Height) / 2 - 10;
                if (Math.Abs(delta.X) > dist || Math.Abs(delta.Y) > dist)
                {
                    graphics.DrawPolygon(pointable.Pen, new[]
                                                        {
                                                            (screencenter + delta.Normalized * dist).Point, 
                                                            (screencenter + Vector2.FromTheta(delta.Theta + 0.2f) * dist).Point, 
                                                            (screencenter + Vector2.FromTheta(delta.Theta - 0.2f) * dist).Point
                                                        });
                }
            }
        }
    }
}
