using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CarParkingGame.EditorTools
{
    // Writes the two impact sounds the car uses when it hits something.
    //
    // The project has an engine, an idle and a horn, and nothing for a collision. Rather
    // than leave the car hitting a wall in silence, these are synthesised: a low thud for
    // the body, a short noise burst for the scrape, and a little metallic ring on the
    // harder of the two. It is not a recording of a car hitting a wall, but it lands at
    // the right moment with the right weight, which is the whole job.
    //
    // Re-run it and the files are rewritten; nothing else reads the samples.
    public static class ImpactSoundGenerator
    {
        private const string Folder = "Assets/GameAssets/Audio";
        private const int SampleRate = 44100;

        public const string SoftPath = Folder + "/ImpactSoft.wav";
        public const string HardPath = Folder + "/ImpactHard.wav";

        [MenuItem("Tools/Car Parking/Generate Impact Sounds")]
        public static void Generate()
        {
            Write(SoftPath, Synthesise(0.26f, 92f, 52f, 0.0035f, 0.55f, false));
            Write(HardPath, Synthesise(0.42f, 134f, 58f, 0.0060f, 1f, true));

            AssetDatabase.Refresh();

            Configure(SoftPath);
            Configure(HardPath);

            Debug.Log($"[ImpactSoundGenerator] Wrote '{SoftPath}' and '{HardPath}'.");
        }

        public static void GenerateFromCommandLine()
        {
            Generate();
            EditorApplication.Exit(0);
        }

        // One impact: a pitch-dropping thud carrying the weight, a noise burst for the
        // crunch, and on the hard one a pair of ringing partials for the metal.
        private static float[] Synthesise(float seconds, float fromHz, float toHz,
            float noiseDecay, float level, bool metallic)
        {
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var samples = new float[count];

            // Fixed seed, so a rebuild produces the same file and git sees no change.
            var random = new System.Random(20260104);

            double phase = 0d;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float through = t / seconds;

                // The body of the hit. The pitch falls as it decays, which is what makes
                // it read as a mass stopping rather than as a beep.
                float hz = Mathf.Lerp(fromHz, toHz, Mathf.Sqrt(through));
                phase += 2d * Math.PI * hz / SampleRate;

                float thud = (float)Math.Sin(phase) * Mathf.Exp(-t / (seconds * 0.28f));

                // The crunch, gone almost before it starts.
                float noise = (float)(random.NextDouble() * 2d - 1d) * Mathf.Exp(-t / noiseDecay);

                float value = thud * 0.85f + noise * 0.45f;

                if (metallic)
                {
                    float ring =
                        Mathf.Sin(2f * Mathf.PI * 523f * t) * 0.5f +
                        Mathf.Sin(2f * Mathf.PI * 781f * t) * 0.3f;

                    value += ring * Mathf.Exp(-t / (seconds * 0.12f)) * 0.3f;
                }

                // A couple of milliseconds of attack, so the very first sample is not a
                // step - a step is a click.
                value *= Mathf.Clamp01(t / 0.002f);

                samples[i] = Mathf.Clamp(value * level, -1f, 1f);
            }

            return samples;
        }

        private static void Write(string path, float[] samples)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Folder);

            using var stream = new FileStream(path, FileMode.Create);
            using var writer = new BinaryWriter(stream);

            int dataBytes = samples.Length * 2;

            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataBytes);
            writer.Write(new[] { 'W', 'A', 'V', 'E' });

            writer.Write(new[] { 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(SampleRate);
            writer.Write(SampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);

            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(dataBytes);

            foreach (float sample in samples)
            {
                writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue));
            }
        }

        // Short, played often, and never worth streaming or decoding on demand.
        private static void Configure(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;

            if (importer == null)
            {
                return;
            }

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.preloadAudioData = true;

            importer.defaultSampleSettings = settings;
            importer.forceToMono = true;

            importer.SaveAndReimport();
        }
    }
}
