using System;
using System.IO;
using UnityEngine;
using Hermit.Core;

namespace Hermit.Economy
{
    /// <summary>C9.1: where the economy save lives. The file store is the
    /// shipped one; tests use <see cref="InMemoryHermitEconomyStore"/>. A
    /// future account/server-backed store implements the same interface.</summary>
    public interface IHermitEconomyStore
    {
        /// <summary>The saved data, or null when nothing has been saved yet.</summary>
        HermitEconomySaveData Load();

        void Save(HermitEconomySaveData data);

        void Clear();
    }

    /// <summary>
    /// C9.1: local JSON file under Application.persistentDataPath — the same
    /// plaintext local-file approach as C4's LocalFileSessionStore (see its
    /// own doc-comment for why a file rather than PlayerPrefs). Writes go to
    /// a temp file first and then replace the real one, so a crash mid-write
    /// never leaves a truncated save. A corrupt or newer-version file is set
    /// aside (renamed) rather than silently overwritten.
    /// </summary>
    public sealed class LocalFileHermitEconomyStore : IHermitEconomyStore
    {
        public const string FileName = "hermit_economy.json";

        public string FilePath { get; }

        public LocalFileHermitEconomyStore()
            : this(Path.Combine(Application.persistentDataPath, FileName))
        {
        }

        public LocalFileHermitEconomyStore(string filePath)
        {
            FilePath = filePath;
        }

        public HermitEconomySaveData Load()
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            try
            {
                var data = JsonUtility.FromJson<HermitEconomySaveData>(File.ReadAllText(FilePath));
                if (data == null)
                {
                    SetAside("unreadable");
                    return null;
                }

                if (data.version > HermitEconomySaveData.CurrentVersion)
                {
                    HermitLog.Warning($"Hermit economy save is version {data.version} (this build understands {HermitEconomySaveData.CurrentVersion}) — setting it aside and starting fresh.");
                    SetAside($"v{data.version}");
                    return null;
                }

                return data;
            }
            catch (Exception e)
            {
                HermitLog.Warning($"Hermit economy save could not be read ({e.Message}) — setting it aside and starting fresh.");
                SetAside("corrupt");
                return null;
            }
        }

        public void Save(HermitEconomySaveData data)
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = FilePath + ".tmp";
            File.WriteAllText(tempPath, JsonUtility.ToJson(data, true));
            if (File.Exists(FilePath))
            {
                File.Replace(tempPath, FilePath, null);
            }
            else
            {
                File.Move(tempPath, FilePath);
            }
        }

        public void Clear()
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }

        private void SetAside(string tag)
        {
            try
            {
                var target = $"{FilePath}.{tag}.{DateTime.UtcNow:yyyyMMddHHmmss}.bak";
                File.Move(FilePath, target);
            }
            catch (Exception e)
            {
                HermitLog.Warning($"Could not set aside the Hermit economy save: {e.Message}");
            }
        }
    }

    /// <summary>C9.1: test/dev store — round-trips through JSON exactly like
    /// the file store, so a "save/reload" test exercises real serialization.</summary>
    public sealed class InMemoryHermitEconomyStore : IHermitEconomyStore
    {
        public string Json { get; private set; }

        public int SaveCount { get; private set; }

        public HermitEconomySaveData Load() =>
            string.IsNullOrEmpty(Json) ? null : JsonUtility.FromJson<HermitEconomySaveData>(Json);

        public void Save(HermitEconomySaveData data)
        {
            Json = JsonUtility.ToJson(data);
            SaveCount++;
        }

        public void Clear()
        {
            Json = null;
        }
    }
}
