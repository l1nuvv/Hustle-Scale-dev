using System;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Inventory
{
    public sealed class CitySaveStore
    {
        private const int MaximumFileBytes = 256 * 1024;
        private readonly string path;
        public string Path => path;
        public bool Exists => File.Exists(path);
        public bool BackupExists => File.Exists(path + ".backup");

        [Serializable]
        private sealed class Envelope
        {
            public string Format, Payload, Checksum;
        }

        public CitySaveStore(string path) => this.path = System.IO.Path.GetFullPath(path);

        private static string Checksum(string payload)
        {
            using (var hash = SHA256.Create())
                return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(payload)));
        }

        private static bool ExpectedFailure(Exception exception) => exception is IOException ||
            exception is UnauthorizedAccessException || exception is ArgumentException || exception is SecurityException;

        private static bool TryRead(string source, out CitySaveData data, out WorkshopSession session, out string message)
        {
            data = null;
            session = null;
            message = "Файл сохранения отсутствует.";
            try
            {
                if (!File.Exists(source)) return false;
                if (new FileInfo(source).Length > MaximumFileBytes)
                { message = "Файл сохранения слишком велик."; return false; }
                var envelope = JsonUtility.FromJson<Envelope>(File.ReadAllText(source, Encoding.UTF8));
                if (envelope == null || envelope.Format != "HustleScale.City.1" ||
                    string.IsNullOrEmpty(envelope.Payload) || envelope.Checksum != Checksum(envelope.Payload))
                { message = "Сохранение повреждено или имеет несовместимый формат."; return false; }
                var candidate = JsonUtility.FromJson<CitySaveData>(envelope.Payload);
                if (!WorkshopSession.TryRestoreCity(candidate, out session, out message)) return false;
                data = candidate;
                return true;
            }
            catch (Exception exception) when (ExpectedFailure(exception))
            {
                message = "Не удалось прочитать сохранение: " + exception.Message;
                return false;
            }
        }

        public bool TryLoad(bool backup, out CitySaveData data, out WorkshopSession session, out string message) =>
            TryRead(backup ? path + ".backup" : path, out data, out session, out message);

        public bool TrySave(CitySaveData data, out string message)
        {
            if (!WorkshopSession.TryRestoreCity(data, out _, out message)) return false;
            var temporary = path + ".tmp";
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                var payload = JsonUtility.ToJson(data);
                var envelope = new Envelope { Format = "HustleScale.City.1", Payload = payload, Checksum = Checksum(payload) };
                var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(envelope));
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                // Never replace a known-good backup with a corrupt primary, including recovery/new game.
                if (File.Exists(path))
                {
                    var backup = TryRead(path, out _, out _, out _) ? path + ".backup" : null;
                    File.Replace(temporary, path, backup);
                }
                else File.Move(temporary, path);
                message = "Город сохранён.";
                return true;
            }
            catch (Exception exception) when (ExpectedFailure(exception))
            {
                message = "Не удалось сохранить город: " + exception.Message;
                return false;
            }
        }
    }
}
