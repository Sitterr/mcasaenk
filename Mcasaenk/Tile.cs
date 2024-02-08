using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Runtime.Intrinsics.Arm;
using System.Windows.Media.Media3D;
using System.Diagnostics;
using Mcasaenk.UI.Canvas;
using System.IO;
using System.Collections.Concurrent;
using Mcasaenk.Rendering;

namespace Mcasaenk
{
    public class TileMap {
        public readonly Dimension dimension;
        private Dictionary<Point2i, Tile> tiles;
        private HashSet<Point2i> possibleTiles;
        
        public TileMap(Dimension dimension, HashSet<Point2i> existingRegions) {
            this.dimension = dimension;
            this.possibleTiles = existingRegions;
            tiles = new Dictionary<Point2i, Tile>();       
        }

        public Tile GetTile(Point2i point) {
            if(!possibleTiles.Contains(point)) return null;
            Tile tile;
            if(tiles.TryGetValue(point, out tile) == false) {
                tile = new Tile(this, point);
                tiles.Add(point, tile);
            }
            return tile;
        }
    }

    public class Tile {

        private TileImage image;
        private TileMap tileMap;

        private List<WorldPosition> observers;

        public readonly Point2i pos;

        public volatile bool Loaded; // temp
        public volatile bool Loading; // temp
        public volatile bool Queued; // temp

        public Tile(TileMap tileMap, Point2i position) {
            this.tileMap = tileMap;
            this.pos = position;
            image = new TileImage(this);
            observers = new List<WorldPosition>();
        }

        public void Load(WorldPosition observer) {
            if(observers.Contains(observer) == false) observers.Add(observer);

            Queued = true;
            var task = new Task(() => {
                try {
                    bool atleastone = false;
                    foreach(var screen in this.observers) {
                        if(screen.IsVisible(this)) {
                            atleastone = true;
                            break;
                        }
                    }
                    if(!atleastone) return;
                    Loading = true;

                    image.GenerateForreal();

                    Loaded = true;
                }
                finally {
                    Queued = false;
                    Loading = false;
                }
            });
            PoolHandler.StartLoadingTask(task);
        }

        public ImageSource GetImage() {
            return image.GetImage();
        }

        public TileMap GetOrigin() { 
            return tileMap;
        }
    }
}
