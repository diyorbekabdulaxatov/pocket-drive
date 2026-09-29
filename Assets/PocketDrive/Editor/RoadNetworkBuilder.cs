using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PocketDrive.Editor
{
    // Reads Downtown's street layout from the city's ground texture, where roads are painted dark on grey,
    // and turns it into a RoadGraph: straight road bands, joined where they cross or meet.
    public static class RoadNetworkBuilder
    {
        public const string GraphPath = "Assets/PocketDrive/Settings/DowntownRoads.asset";
        const string GroundTexture = ImportedAssets.Root + "/City/textures/base_baseColor.jpeg";
        const int Size = 1024;             // analysis resolution of the road map
        const int MinRun = 24;             // shortest road piece, in pixels (about 10 m)
        const int MaxBandWidth = 34;       // wider dark areas are plazas, not roads
        const int MinBandWidth = 10;       // narrower dark strips are kerbs or markings

        sealed class Band
        {
            public bool horizontal;  // runs along texture u
            public float centre;     // v for horizontal bands, u for vertical ones
            public int from, to;     // extent along the band
            public float width;
        }

        // Expects the Downtown scene to be open, with colliders on the city.
        public static RoadGraph Build()
        {
            bool[,] road = RoadMask(out Texture2D map);
            // Split the map into along-x and along-z roads: a pixel belongs to an along-x road when its dark run
            // is long in x and short in z (so crossing roads and the thick border don't swallow each other).
            int[,] hRun = RunLengths(road, true), vRun = RunLengths(road, false);
            var alongX = new bool[Size, Size];
            var alongZ = new bool[Size, Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    alongX[x, y] = road[x, y] && hRun[x, y] >= MinRun && vRun[x, y] <= MaxBandWidth;
                    alongZ[x, y] = road[x, y] && vRun[x, y] >= MinRun && hRun[x, y] <= MaxBandWidth;
                }
            var bands = Bands(alongX, true).Concat(Bands(alongZ, false)).ToList();
            var (toWorld, metresPerPixel) = TextureToWorld();

            var nodes = new List<Vector2>();               // in texture pixels
            int NodeAt(Vector2 p)
            {
                for (int i = 0; i < nodes.Count; i++)
                    if ((nodes[i] - p).sqrMagnitude < 18 * 18) return i;
                nodes.Add(p);
                return nodes.Count - 1;
            }

            // Nodes where bands cross or touch, plus the ends of every band.
            var onBand = bands.ToDictionary(b => b, _ => new List<int>());
            foreach (var h in bands.Where(b => b.horizontal))
                foreach (var v in bands.Where(b => !b.horizontal))
                {
                    float slack = Mathf.Max(h.width, v.width) + 6; // band ends stop short of the crossing road
                    if (v.centre < h.from - slack || v.centre > h.to + slack) continue;
                    if (h.centre < v.from - slack || h.centre > v.to + slack) continue;
                    int n = NodeAt(new Vector2(v.centre, h.centre));
                    onBand[h].Add(n);
                    onBand[v].Add(n);
                }
            foreach (var b in bands)
            {
                onBand[b].Add(NodeAt(b.horizontal ? new Vector2(b.from, b.centre) : new Vector2(b.centre, b.from)));
                onBand[b].Add(NodeAt(b.horizontal ? new Vector2(b.to, b.centre) : new Vector2(b.centre, b.to)));
            }

            var edges = new HashSet<Vector2Int>();
            foreach (var b in bands)
            {
                var ordered = onBand[b].Distinct()
                    .OrderBy(n => b.horizontal ? nodes[n].x : nodes[n].y).ToList();
                for (int i = 0; i + 1 < ordered.Count; i++)
                {
                    int a = ordered[i], c = ordered[i + 1];
                    if ((nodes[a] - nodes[c]).magnitude < 8) continue;
                    edges.Add(a < c ? new Vector2Int(a, c) : new Vector2Int(c, a));
                }
            }

            // Straighten: a node's position is the average of where its bands say it is, so edges stay axis-aligned.
            var graph = AssetDatabase.LoadAssetAtPath<RoadGraph>(GraphPath);
            if (graph == null)
            {
                graph = ScriptableObject.CreateInstance<RoadGraph>();
                Directory.CreateDirectory(Path.GetDirectoryName(GraphPath));
                AssetDatabase.CreateAsset(graph, GraphPath);
            }
            graph.nodes = nodes.Select(toWorld).ToArray();
            graph.edges = edges.ToArray();
            float typicalWidth = bands.Select(b => b.width).OrderBy(w => w).ElementAt(bands.Count / 2);
            graph.laneOffset = typicalWidth * metresPerPixel * .25f;
            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();

            DrawOverlay(map, road, nodes, edges, "outputs/downtown-roads.png");
            Object.DestroyImmediate(map);
            Debug.Log($"POCKET_DRIVE_ROADS_OK: {bands.Count} road bands, {graph.nodes.Length} nodes, {graph.edges.Length} edges, lane offset {graph.laneOffset:0.0} m");
            return graph;
        }

        static bool[,] RoadMask(out Texture2D map)
        {
            var source = new Texture2D(2, 2);
            source.LoadImage(File.ReadAllBytes(GroundTexture));
            map = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            var road = new bool[Size, Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    Color c = source.GetPixelBilinear((x + .5f) / Size, (y + .5f) / Size);
                    map.SetPixel(x, y, c);
                    road[x, y] = c.grayscale < .24f;
                }
            Object.DestroyImmediate(source);
            // Closing (grow, then shrink) fills the painted lane lines so each road is one solid band.
            return Erode(Dilate(road, 4), 4);
        }

        // Length of the dark run through each pixel, along x (horizontal) or along y.
        static int[,] RunLengths(bool[,] m, bool horizontal)
        {
            var result = new int[Size, Size];
            for (int line = 0; line < Size; line++)
            {
                int start = 0;
                for (int i = 0; i <= Size; i++)
                {
                    bool dark = i < Size && (horizontal ? m[i, line] : m[line, i]);
                    if (dark) continue;
                    for (int k = start; k < i; k++)
                        if (horizontal) result[k, line] = i - start; else result[line, k] = i - start;
                    start = i + 1;
                }
            }
            return result;
        }

        static bool[,] Dilate(bool[,] m, int r) => Morph(m, r, true);
        static bool[,] Erode(bool[,] m, int r) => Morph(m, r, false);

        static bool[,] Morph(bool[,] m, int r, bool grow)
        {
            // Separable square structuring element: rows, then columns.
            var tmp = new bool[Size, Size];
            var result = new bool[Size, Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    bool v = !grow;
                    for (int d = -r; d <= r && v != grow; d++)
                    {
                        int xx = Mathf.Clamp(x + d, 0, Size - 1);
                        v = grow ? m[xx, y] : m[xx, y] && v;
                    }
                    tmp[x, y] = v;
                }
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    bool v = !grow;
                    for (int d = -r; d <= r && v != grow; d++)
                    {
                        int yy = Mathf.Clamp(y + d, 0, Size - 1);
                        v = grow ? tmp[x, yy] : tmp[x, yy] && v;
                    }
                    result[x, y] = v;
                }
            return result;
        }

        // Rows (or columns) with long dark runs, grouped into bands of consecutive rows that overlap.
        static List<Band> Bands(bool[,] road, bool horizontal)
        {
            var bands = new List<Band>();
            var open = new List<(int start, int from, int to, int rows)>();
            for (int line = 0; line <= Size; line++)
            {
                var runs = new List<(int from, int to)>();
                if (line < Size)
                {
                    int start = -1;
                    for (int i = 0; i <= Size; i++)
                    {
                        bool dark = i < Size && (horizontal ? road[i, line] : road[line, i]);
                        if (dark && start < 0) start = i;
                        if (!dark && start >= 0)
                        {
                            if (i - start >= MinRun) runs.Add((start, i - 1));
                            start = -1;
                        }
                    }
                }
                var next = new List<(int start, int from, int to, int rows)>();
                var used = new HashSet<int>();
                foreach (var o in open)
                {
                    int match = runs.FindIndex(r => r.from <= o.to && r.to >= o.from);
                    if (match >= 0 && !used.Contains(match))
                    {
                        used.Add(match);
                        var r = runs[match];
                        next.Add((o.start, Mathf.Min(o.from, r.from), Mathf.Max(o.to, r.to), o.rows + 1));
                    }
                    else if (o.rows >= MinBandWidth && o.rows <= MaxBandWidth)
                        bands.Add(new Band { horizontal = horizontal, centre = o.start + (o.rows - 1) * .5f, from = o.from, to = o.to, width = o.rows });
                }
                for (int i = 0; i < runs.Count; i++)
                    if (!used.Contains(i)) next.Add((line, runs[i].from, runs[i].to, 1));
                open = next;
            }
            return bands;
        }

        // Fits texture pixel -> world position from raycasts onto the ground (u,v are linear in x,z on a flat plane).
        static (Func<Vector2, Vector3> toWorld, float metresPerPixel) TextureToWorld()
        {
            var samples = new List<(Vector2 world, Vector2 uv)>();
            for (float x = 30; x < 400; x += 37)
                for (float z = -390; z < -20; z += 37)
                    if (Physics.Raycast(new Vector3(x, 200, z), Vector3.down, out var hit, 400) && hit.collider.name == "Object_946")
                        samples.Add((new Vector2(x, z), hit.textureCoord));
            if (samples.Count < 6) throw new Exception("Could not sample the city ground texture");
            // Least squares for world = A * uv + b, solved separately for x and z.
            Vector3 Solve(Func<Vector2, float> target)
            {
                double[,] m = new double[3, 3];
                double[] r = new double[3];
                foreach (var s in samples)
                {
                    double[] row = { s.uv.x, s.uv.y, 1 };
                    for (int i = 0; i < 3; i++)
                    {
                        for (int j = 0; j < 3; j++) m[i, j] += row[i] * row[j];
                        r[i] += row[i] * target(s.world);
                    }
                }
                return GaussSolve(m, r);
            }
            Vector3 fx = Solve(w => w.x), fz = Solve(w => w.y);
            float y = -.123f;
            Vector3 ToWorld(Vector2 px)
            {
                float u = (px.x + .5f) / Size, v = (px.y + .5f) / Size;
                return new Vector3(fx.x * u + fx.y * v + fx.z, y, fz.x * u + fz.y * v + fz.z);
            }
            float mpp = (ToWorld(new Vector2(Size, 0)) - ToWorld(Vector2.zero)).magnitude / Size;
            return (ToWorld, mpp);
        }

        static Vector3 GaussSolve(double[,] m, double[] r)
        {
            int n = 3;
            for (int c = 0; c < n; c++)
            {
                int p = c;
                for (int i = c + 1; i < n; i++) if (Math.Abs(m[i, c]) > Math.Abs(m[p, c])) p = i;
                for (int j = 0; j < n; j++) (m[c, j], m[p, j]) = (m[p, j], m[c, j]);
                (r[c], r[p]) = (r[p], r[c]);
                for (int i = c + 1; i < n; i++)
                {
                    double f = m[i, c] / m[c, c];
                    for (int j = c; j < n; j++) m[i, j] -= f * m[c, j];
                    r[i] -= f * r[c];
                }
            }
            var x = new double[n];
            for (int i = n - 1; i >= 0; i--)
            {
                double s = r[i];
                for (int j = i + 1; j < n; j++) s -= m[i, j] * x[j];
                x[i] = s / m[i, i];
            }
            return new Vector3((float)x[0], (float)x[1], (float)x[2]);
        }

        static void DrawOverlay(Texture2D map, bool[,] road, List<Vector2> nodes, IEnumerable<Vector2Int> edges, string path)
        {
            var image = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    image.SetPixel(x, y, road[x, y] ? new Color(.25f, .25f, .3f) : map.GetPixel(x, y) * .6f);
            foreach (var e in edges)
            {
                Vector2 a = nodes[e.x], b = nodes[e.y];
                int steps = Mathf.CeilToInt((a - b).magnitude);
                for (int s = 0; s <= steps; s++)
                {
                    Vector2 p = Vector2.Lerp(a, b, s / (float)Mathf.Max(1, steps));
                    for (int d = -1; d <= 1; d++) image.SetPixel((int)p.x + d, (int)p.y, Color.yellow);
                }
            }
            foreach (var n in nodes)
                for (int dx = -4; dx <= 4; dx++) for (int dy = -4; dy <= 4; dy++) image.SetPixel((int)n.x + dx, (int)n.y + dy, Color.red);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }

        public static void BuildFromScene()
        {
            EditorSceneManager.OpenScene(DowntownBuilder.ScenePath);
            Physics.SyncTransforms();
            Build();
        }
    }
}
