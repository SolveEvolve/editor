using System;
using System.IO;
using System.Security.Cryptography;
using ProtoVR.Splats;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace ProtoVR.Editor
{
    [ScriptedImporter(1, "qsf")]
    public sealed class StaticQsfImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext context)
        {
            byte[] bytes = File.ReadAllBytes(context.assetPath);
            if (bytes.Length < StaticQsfAsset.HeaderSize)
                throw new InvalidDataException("QSF2 header is truncated.");

            using var stream = new MemoryStream(bytes, false);
            using var reader = new BinaryReader(stream);
            uint magic = reader.ReadUInt32();
            int version = reader.ReadInt32();
            if (magic != StaticQsfAsset.Magic || version != StaticQsfAsset.Version)
                throw new InvalidDataException("Only fixed-profile QSF2 static assets are supported.");

            int sourceIndex = reader.ReadInt32();
            int splatCount = reader.ReadInt32();
            var boundsMin = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            var boundsMax = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            int positionFormat = reader.ReadInt32();
            int scaleFormat = reader.ReadInt32();
            int colorFormat = reader.ReadInt32();
            int chunkLength = reader.ReadInt32();
            int positionLength = reader.ReadInt32();
            int otherLength = reader.ReadInt32();
            int colorLength = reader.ReadInt32();
            if (positionFormat != 1 || scaleFormat != 2 || colorFormat != 2)
                throw new InvalidDataException("QSF2 asset uses an unsupported encoding profile.");

            long expectedLength = StaticQsfAsset.HeaderSize + (long)chunkLength + positionLength + otherLength + colorLength;
            if (expectedLength != bytes.LongLength)
                throw new InvalidDataException("QSF2 file length does not match its header.");

            byte[] chunks = reader.ReadBytes(chunkLength);
            byte[] positions = reader.ReadBytes(positionLength);
            byte[] other = reader.ReadBytes(otherLength);
            byte[] colors = reader.ReadBytes(colorLength);
            if (stream.Position != stream.Length)
                throw new InvalidDataException("QSF2 asset contains trailing bytes.");

            string sha256;
            using (var hash = SHA256.Create())
                sha256 = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();

            var asset = ScriptableObject.CreateInstance<StaticQsfAsset>();
            asset.name = Path.GetFileNameWithoutExtension(context.assetPath);
            asset.Initialize(sourceIndex, splatCount, boundsMin, boundsMax, sha256, chunks, positions, other, colors);
            asset.Validate();
            context.AddObjectToAsset("Static QSF2", asset);
            context.SetMainObject(asset);
        }
    }
}
