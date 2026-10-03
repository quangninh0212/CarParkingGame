using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CarParkingGame.EditorTools
{
    // Draws the UI's icons as PNG assets.
    //
    // The project ships no icon set and nothing is downloaded, so the icons are described
    // here as shapes and rasterised. Each icon is a union of primitives with an optional
    // set of holes, sampled 4x4 per pixel so the edges come out smooth at any size; they
    // are written white on transparent, which lets the UI tint them per button.
    //
    // Replace the PNGs with drawn art whenever it exists - the file names are the contract,
    // and GameplayUiBuilder only ever looks them up by name.
    public static class IconSpriteGenerator
    {
        public const string IconFolder = "Assets/GameAssets/Sprites/Icons";

        private const int Resolution = 128;
        private const int SuperSamples = 4;

        [MenuItem("Tools/Car Parking/Generate UI Icons")]
        public static void GenerateFromMenu()
        {
            Generate();
            Debug.Log($"[IconSpriteGenerator] Wrote the icon set to '{IconFolder}'.");
        }

        public static void GenerateFromCommandLine()
        {
            Generate();
            EditorApplication.Exit(0);
        }

        // Writes every icon into one dark-backed sheet, because white-on-transparent PNGs
        // cannot be eyeballed in a file browser.
        public static void WritePreviewSheetFromCommandLine()
        {
            string output = "icon-sheet.png";
            string[] args = Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-sheetOut")
                {
                    output = args[i + 1];
                }
            }

            Dictionary<string, Shape[]> library = BuildLibrary();
            const int columns = 6;
            const int cell = 128;
            int rows = Mathf.CeilToInt(library.Count / (float)columns);

            var sheet = new Texture2D(columns * cell, rows * cell, TextureFormat.RGBA32, false);
            var background = new Color32(24, 27, 34, 255);
            var clear = new Color32[sheet.width * sheet.height];

            for (int i = 0; i < clear.Length; i++)
            {
                clear[i] = background;
            }

            sheet.SetPixels32(clear);

            int index = 0;

            foreach (KeyValuePair<string, Shape[]> icon in library)
            {
                int column = index % columns;

                // Rows fill from the top, and texture rows count up from the bottom.
                int row = rows - 1 - index / columns;

                var texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false);
                texture.LoadImage(File.ReadAllBytes($"{IconFolder}/{icon.Key}.png"));

                Color[] pixels = texture.GetPixels();

                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = Color.Lerp(background, Color.white, pixels[i].a);
                }

                sheet.SetPixels(column * cell, row * cell, Resolution, Resolution, pixels);
                UnityEngine.Object.DestroyImmediate(texture);
                index++;
            }

            sheet.Apply();
            File.WriteAllBytes(output, sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);

            Debug.Log($"[IconSpriteGenerator] Wrote a preview sheet to '{output}' in this order: {string.Join(", ", library.Keys)}");
            EditorApplication.Exit(0);
        }

        public static Sprite Load(string iconName)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{IconFolder}/{iconName}.png");
        }

        public static void Generate()
        {
            Directory.CreateDirectory(IconFolder);

            foreach (KeyValuePair<string, Shape[]> icon in BuildLibrary())
            {
                Write(icon.Key, icon.Value);
            }

            AssetDatabase.Refresh();

            foreach (string name in BuildLibrary().Keys)
            {
                ConfigureImporter($"{IconFolder}/{name}.png");
            }

            AssetDatabase.Refresh();
        }

        // ----- the icons -------------------------------------------------------------------
        //
        // Coordinates are 0..1 with (0,0) bottom left, so the shapes read like a drawing
        // rather than like pixel maths.

        private static Dictionary<string, Shape[]> BuildLibrary()
        {
            return new Dictionary<string, Shape[]>
            {
                ["camera"] = new[]
                {
                    Shape.RoundedRect(new Vector2(0.5f, 0.46f), new Vector2(0.78f, 0.52f), 0.1f),
                    Shape.RoundedRect(new Vector2(0.38f, 0.78f), new Vector2(0.26f, 0.14f), 0.05f),
                    Shape.Circle(new Vector2(0.5f, 0.46f), 0.2f).AsHole(),
                    Shape.Circle(new Vector2(0.5f, 0.46f), 0.13f),
                    Shape.Circle(new Vector2(0.76f, 0.63f), 0.05f).AsHole()
                },

                // A speaker cone with two sound arcs: the readable shorthand for a horn.
                ["horn"] = new[]
                {
                    Shape.Rect(new Vector2(0.26f, 0.5f), new Vector2(0.16f, 0.26f)),
                    Shape.Polygon(
                        new Vector2(0.34f, 0.5f),
                        new Vector2(0.56f, 0.82f),
                        new Vector2(0.56f, 0.18f)),
                    Shape.Arc(new Vector2(0.56f, 0.5f), 0.19f, 0.065f, -55f, 55f),
                    Shape.Arc(new Vector2(0.56f, 0.5f), 0.32f, 0.065f, -55f, 55f)
                },

                // A low-beam lamp: a D-shaped housing throwing three rays.
                ["headlight"] = new[]
                {
                    Shape.Circle(new Vector2(0.34f, 0.5f), 0.27f),
                    Shape.Rect(new Vector2(0.2f, 0.5f), new Vector2(0.28f, 0.54f)).AsHole(),
                    Shape.Rect(new Vector2(0.22f, 0.5f), new Vector2(0.1f, 0.54f)),
                    Shape.Capsule(new Vector2(0.64f, 0.72f), new Vector2(0.88f, 0.66f), 0.055f),
                    Shape.Capsule(new Vector2(0.64f, 0.5f), new Vector2(0.9f, 0.5f), 0.055f),
                    Shape.Capsule(new Vector2(0.64f, 0.28f), new Vector2(0.88f, 0.34f), 0.055f)
                },

                ["indicator-left"] = Arrow(-1f),
                ["indicator-right"] = Arrow(1f),

                ["pause"] = new[]
                {
                    Shape.RoundedRect(new Vector2(0.36f, 0.5f), new Vector2(0.16f, 0.6f), 0.06f),
                    Shape.RoundedRect(new Vector2(0.64f, 0.5f), new Vector2(0.16f, 0.6f), 0.06f)
                },

                ["play"] = new[]
                {
                    Shape.Polygon(
                        new Vector2(0.3f, 0.82f),
                        new Vector2(0.82f, 0.5f),
                        new Vector2(0.3f, 0.18f))
                },

                ["gear"] = BuildGear(),

                // A car from the side: body, cabin and two wheels.
                ["car"] = new[]
                {
                    Shape.RoundedRect(new Vector2(0.5f, 0.45f), new Vector2(0.84f, 0.22f), 0.08f),
                    Shape.Polygon(
                        new Vector2(0.24f, 0.55f),
                        new Vector2(0.36f, 0.76f),
                        new Vector2(0.68f, 0.76f),
                        new Vector2(0.78f, 0.55f)),
                    Shape.Circle(new Vector2(0.3f, 0.3f), 0.13f),
                    Shape.Circle(new Vector2(0.72f, 0.3f), 0.13f),
                    Shape.Circle(new Vector2(0.3f, 0.3f), 0.05f).AsHole(),
                    Shape.Circle(new Vector2(0.72f, 0.3f), 0.05f).AsHole()
                },

                ["star"] = new[] { Star(new Vector2(0.5f, 0.5f), 0.46f, 0.2f) },

                // A traffic cone: practice.
                ["cone"] = new[]
                {
                    Shape.Polygon(
                        new Vector2(0.5f, 0.86f),
                        new Vector2(0.72f, 0.26f),
                        new Vector2(0.28f, 0.26f)),
                    Shape.RoundedRect(new Vector2(0.5f, 0.2f), new Vector2(0.66f, 0.12f), 0.05f)
                },

                // A stopwatch: the timed challenge.
                ["stopwatch"] = new[]
                {
                    Shape.Ring(new Vector2(0.5f, 0.45f), 0.33f, 0.24f),
                    Shape.Rect(new Vector2(0.5f, 0.85f), new Vector2(0.24f, 0.1f)),
                    Shape.Rect(new Vector2(0.5f, 0.78f), new Vector2(0.1f, 0.08f)),
                    Shape.Capsule(new Vector2(0.5f, 0.45f), new Vector2(0.5f, 0.64f), 0.045f),
                    Shape.Capsule(new Vector2(0.5f, 0.45f), new Vector2(0.64f, 0.45f), 0.045f)
                },

                // A compass rose: free driving.
                ["compass"] = new[]
                {
                    Shape.Ring(new Vector2(0.5f, 0.5f), 0.4f, 0.32f),
                    Shape.Polygon(
                        new Vector2(0.5f, 0.78f),
                        new Vector2(0.62f, 0.46f),
                        new Vector2(0.5f, 0.54f)),
                    Shape.Polygon(
                        new Vector2(0.5f, 0.22f),
                        new Vector2(0.38f, 0.54f),
                        new Vector2(0.5f, 0.46f))
                },

                ["home"] = new[]
                {
                    Shape.Polygon(
                        new Vector2(0.5f, 0.88f),
                        new Vector2(0.92f, 0.48f),
                        new Vector2(0.08f, 0.48f)),
                    Shape.Rect(new Vector2(0.5f, 0.3f), new Vector2(0.56f, 0.36f)),
                    Shape.Rect(new Vector2(0.5f, 0.24f), new Vector2(0.18f, 0.24f)).AsHole()
                },

                ["back"] = new[]
                {
                    Shape.Capsule(new Vector2(0.62f, 0.78f), new Vector2(0.3f, 0.5f), 0.1f),
                    Shape.Capsule(new Vector2(0.3f, 0.5f), new Vector2(0.62f, 0.22f), 0.1f)
                },

                ["close"] = new[]
                {
                    Shape.Capsule(new Vector2(0.26f, 0.26f), new Vector2(0.74f, 0.74f), 0.09f),
                    Shape.Capsule(new Vector2(0.26f, 0.74f), new Vector2(0.74f, 0.26f), 0.09f)
                },

                ["exit"] = new[]
                {
                    Shape.RoundedRect(new Vector2(0.36f, 0.5f), new Vector2(0.4f, 0.74f), 0.08f),
                    Shape.RoundedRect(new Vector2(0.36f, 0.5f), new Vector2(0.24f, 0.58f), 0.04f).AsHole(),
                    Shape.Capsule(new Vector2(0.56f, 0.5f), new Vector2(0.86f, 0.5f), 0.07f),
                    Shape.Polygon(
                        new Vector2(0.94f, 0.5f),
                        new Vector2(0.74f, 0.66f),
                        new Vector2(0.74f, 0.34f))
                },

                // The navigation arrow the challenge HUD points at the next bay.
                ["guide-arrow"] = new[]
                {
                    Shape.Polygon(
                        new Vector2(0.5f, 0.94f),
                        new Vector2(0.94f, 0.42f),
                        new Vector2(0.5f, 0.58f),
                        new Vector2(0.06f, 0.42f))
                },

                // Flat white discs and rings the HUD uses as button backings.
                ["disc"] = new[] { Shape.Circle(new Vector2(0.5f, 0.5f), 0.5f) },
                ["ring"] = new[] { Shape.Ring(new Vector2(0.5f, 0.5f), 0.5f, 0.42f) },
                ["panel"] = new[] { Shape.RoundedRect(new Vector2(0.5f, 0.5f), Vector2.one, 0.18f) }
            };
        }

        // Ten alternating points, outer then inner: the usual five-pointed star.
        private static Shape Star(Vector2 center, float outer, float inner)
        {
            var points = new Vector2[10];

            for (int i = 0; i < points.Length; i++)
            {
                float radius = i % 2 == 0 ? outer : inner;

                // Starting at 90 degrees puts a point straight up.
                float angle = (90f + i * 36f) * Mathf.Deg2Rad;
                points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }

            return Shape.Polygon(points);
        }

        private static Shape[] Arrow(float direction)
        {
            float tip = 0.5f + direction * 0.4f;
            float neck = 0.5f + direction * 0.04f;
            float tail = 0.5f - direction * 0.42f;

            return new[]
            {
                Shape.Polygon(
                    new Vector2(tip, 0.5f),
                    new Vector2(neck, 0.86f),
                    new Vector2(neck, 0.14f)),
                Shape.Rect(new Vector2((neck + tail) * 0.5f, 0.5f), new Vector2(Mathf.Abs(neck - tail), 0.26f))
            };
        }

        private static Shape[] BuildGear()
        {
            var shapes = new List<Shape>
            {
                Shape.Ring(new Vector2(0.5f, 0.5f), 0.34f, 0.15f)
            };

            // Eight teeth laid round the rim, as capsules pointing outwards.
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 2f / 8f;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                shapes.Add(Shape.Capsule(
                    new Vector2(0.5f, 0.5f) + direction * 0.3f,
                    new Vector2(0.5f, 0.5f) + direction * 0.44f,
                    0.085f));
            }

            return shapes.ToArray();
        }

        // ----- rasteriser ------------------------------------------------------------------

        public class Shape
        {
            private enum Kind
            {
                Circle,
                Ring,
                Rect,
                RoundedRect,
                Polygon,
                Capsule,
                Arc
            }

            private Kind kind;
            private Vector2 a;
            private Vector2 b;
            private Vector2 size;
            private float radius;
            private float inner;
            private float thickness;
            private float fromDegrees;
            private float toDegrees;
            private Vector2[] points;
            private bool hole;

            public bool IsHole => hole;

            public Shape AsHole()
            {
                hole = true;
                return this;
            }

            public static Shape Circle(Vector2 center, float r) =>
                new Shape { kind = Kind.Circle, a = center, radius = r };

            public static Shape Ring(Vector2 center, float outer, float innerRadius) =>
                new Shape { kind = Kind.Ring, a = center, radius = outer, inner = innerRadius };

            public static Shape Rect(Vector2 center, Vector2 rectSize) =>
                new Shape { kind = Kind.Rect, a = center, size = rectSize };

            public static Shape RoundedRect(Vector2 center, Vector2 rectSize, float cornerRadius) =>
                new Shape { kind = Kind.RoundedRect, a = center, size = rectSize, radius = cornerRadius };

            public static Shape Polygon(params Vector2[] polygonPoints) =>
                new Shape { kind = Kind.Polygon, points = polygonPoints };

            public static Shape Capsule(Vector2 from, Vector2 to, float capsuleRadius) =>
                new Shape { kind = Kind.Capsule, a = from, b = to, radius = capsuleRadius };

            public static Shape Arc(Vector2 center, float arcRadius, float arcThickness, float from, float to) =>
                new Shape
                {
                    kind = Kind.Arc,
                    a = center,
                    radius = arcRadius,
                    thickness = arcThickness,
                    fromDegrees = from,
                    toDegrees = to
                };

            public bool Contains(Vector2 p)
            {
                switch (kind)
                {
                    case Kind.Circle:
                        return (p - a).sqrMagnitude <= radius * radius;

                    case Kind.Ring:
                    {
                        float distance = (p - a).magnitude;
                        return distance <= radius && distance >= inner;
                    }

                    case Kind.Rect:
                        return Mathf.Abs(p.x - a.x) <= size.x * 0.5f && Mathf.Abs(p.y - a.y) <= size.y * 0.5f;

                    case Kind.RoundedRect:
                    {
                        Vector2 half = size * 0.5f;
                        float r = Mathf.Min(radius, Mathf.Min(half.x, half.y));
                        var delta = new Vector2(Mathf.Abs(p.x - a.x), Mathf.Abs(p.y - a.y));
                        Vector2 corner = delta - (half - Vector2.one * r);

                        if (corner.x <= 0f || corner.y <= 0f)
                        {
                            return delta.x <= half.x && delta.y <= half.y;
                        }

                        return corner.sqrMagnitude <= r * r;
                    }

                    case Kind.Capsule:
                    {
                        Vector2 along = b - a;
                        float lengthSquared = Mathf.Max(0.000001f, along.sqrMagnitude);
                        float t = Mathf.Clamp01(Vector2.Dot(p - a, along) / lengthSquared);
                        return (p - (a + along * t)).sqrMagnitude <= radius * radius;
                    }

                    case Kind.Arc:
                    {
                        Vector2 delta = p - a;
                        float distance = delta.magnitude;

                        if (Mathf.Abs(distance - radius) > thickness * 0.5f)
                        {
                            return false;
                        }

                        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                        return angle >= fromDegrees && angle <= toDegrees;
                    }

                    case Kind.Polygon:
                        return InPolygon(p);

                    default:
                        return false;
                }
            }

            // Even-odd crossing test; the icons' outlines are all simple polygons.
            private bool InPolygon(Vector2 p)
            {
                bool inside = false;

                for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
                {
                    bool straddles = points[i].y > p.y != points[j].y > p.y;

                    if (!straddles)
                    {
                        continue;
                    }

                    float x = (points[j].x - points[i].x) * (p.y - points[i].y)
                              / (points[j].y - points[i].y) + points[i].x;

                    if (p.x < x)
                    {
                        inside = !inside;
                    }
                }

                return inside;
            }
        }

        private static void Write(string name, Shape[] shapes)
        {
            var pixels = new Color32[Resolution * Resolution];
            float step = 1f / (Resolution * SuperSamples);

            for (int y = 0; y < Resolution; y++)
            {
                for (int x = 0; x < Resolution; x++)
                {
                    int hits = 0;

                    for (int sy = 0; sy < SuperSamples; sy++)
                    {
                        for (int sx = 0; sx < SuperSamples; sx++)
                        {
                            var point = new Vector2(
                                (x * SuperSamples + sx + 0.5f) * step,
                                (y * SuperSamples + sy + 0.5f) * step);

                            if (Covered(shapes, point))
                            {
                                hits++;
                            }
                        }
                    }

                    byte alpha = (byte)Mathf.RoundToInt(255f * hits / (SuperSamples * SuperSamples));
                    pixels[y * Resolution + x] = new Color32(255, 255, 255, alpha);
                }
            }

            var texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();

            File.WriteAllBytes($"{IconFolder}/{name}.png", texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        private static bool Covered(Shape[] shapes, Vector2 point)
        {
            bool filled = false;

            foreach (Shape shape in shapes)
            {
                if (shape.IsHole)
                {
                    continue;
                }

                if (shape.Contains(point))
                {
                    filled = true;
                    break;
                }
            }

            if (!filled)
            {
                return false;
            }

            foreach (Shape shape in shapes)
            {
                if (shape.IsHole && shape.Contains(point))
                {
                    return false;
                }
            }

            return true;
        }

        private static void ConfigureImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer == null)
            {
                Debug.LogWarning($"[IconSpriteGenerator] '{path}' did not import as a texture.");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;

            // The rounded panel is the one icon stretched to arbitrary sizes, so it is
            // nine-sliced: the border matches its 0.18 corner radius at this resolution.
            if (path.EndsWith("/panel.png", StringComparison.Ordinal))
            {
                importer.spriteBorder = new Vector4(26f, 26f, 26f, 26f);
            }

            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
    }
}
