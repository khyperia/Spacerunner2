using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;

namespace Spacerunner2
{
    static class World
    {
        public static PointF CameraCenter = new Point(0, 0);
        private static DateTime _lastTick = DateTime.UtcNow;
        public static float DeltaSeconds { get; private set; }

        public static Rectangle CameraRectangle(Size screenSize)
        {
            return new Rectangle((int)(CameraCenter.X - screenSize.Width / 2.0f), (int)(CameraCenter.Y - screenSize.Height / 2.0f), screenSize.Width, screenSize.Height);
        }

        public static void Tick()
        {
            var now = DateTime.UtcNow;
            DeltaSeconds = (float)(now - _lastTick).TotalSeconds;
            _lastTick = now;
        }
    }

    abstract class Entity
    {
        private static readonly List<Entity> Entities = new List<Entity>();
        private static Entity[] _entityCloneArray = new Entity[10];
        public IPEndPoint Owner;

        private static Entity[] GetEntityCloneArray()
        {
            if (Entities.Count > _entityCloneArray.Length)
                _entityCloneArray = new Entity[Entities.Count + 50];
            Entities.CopyTo(_entityCloneArray);
            return _entityCloneArray;
        }

        public static void RemoveObjectsByOwner(IPEndPoint owner)
        {
            Entities.RemoveAll(e => Equals(owner, e.Owner));
        }

        public void Spawn(IPEndPoint owner)
        {
            Owner = owner;
            for (var i = 0; i < Entities.Count; i++)
            {
                if (Entities[i].DrawOrder < DrawOrder)
                    continue;

                Entities.Insert(i, this);
                return;
            }
            Entities.Add(this);
        }

        public void Die()
        {
            Entities.Remove(this);
        }

        protected static IEnumerable<T> EntitiesOfType<T>()
        {
            return Entities.OfType<T>();
        }

        public static void TickAll(NetCon netCon, Graphics graphics, Size screenSize)
        {
            var camera = World.CameraRectangle(screenSize);
            var arr = GetEntityCloneArray();
            var count = Entities.Count;
            for (var i = 0; i < count; i++)
                arr[i].Tick(netCon, graphics, camera);
        }

        protected abstract int DrawOrder { get; }
        protected abstract void Tick(NetCon netCon, Graphics graphics, Rectangle camera);
    }
}