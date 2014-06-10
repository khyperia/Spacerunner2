using System;
using System.Drawing;
using System.Globalization;
using System.Net;

namespace Spacerunner2
{
    static class Ext
    {
        public static readonly Random Rand = new Random();

        public static Vector2 Center(this Rectangle rect)
        {
            return new Vector2(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
        }

        public static PointF Add(this PointF left, PointF right)
        {
            return new PointF(left.X + right.X, left.Y + right.Y);
        }

        public static Color ColorFromHsv(double hue, double saturation, double value)
        {
            var hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
            var f = hue / 60 - Math.Floor(hue / 60);

            value = value * 255;
            var v = Convert.ToInt32(value);
            var p = Convert.ToInt32(value * (1 - saturation));
            var q = Convert.ToInt32(value * (1 - f * saturation));
            var t = Convert.ToInt32(value * (1 - (1 - f) * saturation));

            switch (hi)
            {
                case 0: return Color.FromArgb(255, v, t, p);
                case 1: return Color.FromArgb(255, q, v, p);
                case 2: return Color.FromArgb(255, p, v, t);
                case 3: return Color.FromArgb(255, p, q, v);
                case 4: return Color.FromArgb(255, t, p, v);
                default: return Color.FromArgb(255, v, p, q);
            }
        }

        private static bool TryParseIpAddress(string address, out IPAddress result)
        {
            if (address != "localhost")
                return IPAddress.TryParse(address, out result);
            result = IPAddress.Loopback;
            return true;
        }

        public static bool TryParseIpEndPoint(string endPoint, int defaultPort, out IPEndPoint result)
        {
            result = new IPEndPoint(IPAddress.Any, 0);
            var ep = endPoint.Split(':');
            if (ep.Length == 0)
            {
                return false;
            }
            IPAddress ip;
            if (ep.Length == 1)
            {
                if (!TryParseIpAddress(ep[0], out ip))
                    return false;
                result = new IPEndPoint(ip, defaultPort);
                return true;
            }
            if (ep.Length > 2)
            {
                if (!TryParseIpAddress(string.Join(":", ep, 0, ep.Length - 1), out ip))
                    return false;
            }
            else
            {
                if (!TryParseIpAddress(ep[0], out ip))
                    return false;
            }
            int port;
            if (!int.TryParse(ep[ep.Length - 1], NumberStyles.None, NumberFormatInfo.CurrentInfo, out port))
                return false;
            result = new IPEndPoint(ip, port);
            return true;
        }
    }

    public struct Vector2
    {
        private readonly float _x, _y;

        public Vector2(float x, float y)
        {
            _x = x;
            _y = y;
        }

        public float X { get { return _x; } }
        public float Y { get { return _y; } }

        public static Vector2 operator +(Vector2 left, Vector2 right)
        {
            return new Vector2(left._x + right._x, left._y + right._y);
        }

        public static Vector2 operator -(Vector2 left, Vector2 right)
        {
            return new Vector2(left._x - right._x, left._y - right._y);
        }

        public static Vector2 operator *(Vector2 left, float right)
        {
            return new Vector2(left._x * right, left._y * right);
        }

        public static Vector2 operator *(float left, Vector2 right)
        {
            return new Vector2(left * right._x, left * right._y);
        }

        public static Vector2 operator /(Vector2 left, float right)
        {
            return new Vector2(left._x / right, left._y / right);
        }

        public static Vector2 Lerp(Vector2 left, Vector2 right, float value)
        {
            return (1 - value) * left + value * right;
        }

        public static Vector2 FromTheta(float theta)
        {
            return new Vector2((float)Math.Cos(theta), (float)Math.Sin(theta));
        }

        public float Theta
        {
            get { return (float)Math.Atan2(_y, _x); }
        }

        public PointF Point
        {
            get { return new PointF(_x, _y); }
        }

        public float MagnitudeSquared
        {
            get { return _x * _x + _y * _y; }
        }

        public float Magnitude
        {
            get { return (float)Math.Sqrt(MagnitudeSquared); }
        }

        public Vector2 Normalized
        { get { return this / Magnitude; } }

        public static float Dot(Vector2 left, Vector2 right)
        {
            return left.X * right.X + left.Y * right.Y;
        }
    }
}