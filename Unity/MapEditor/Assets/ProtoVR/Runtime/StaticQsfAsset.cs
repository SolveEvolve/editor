using System;
using GaussianSplatting.Runtime;
using Unity.Mathematics;
using UnityEngine;

namespace ProtoVR.Splats
{
    public sealed class StaticQsfAsset : ScriptableObject
    {
        public const uint Magic = 0x32534651;
        public const int Version = 2;
        public const int HeaderSize = 68;
        public const int ChunkByteSize = 64;

        [SerializeField] int sourceIndex;
        [SerializeField] int splatCount;
        [SerializeField] Vector3 boundsMin;
        [SerializeField] Vector3 boundsMax;
        [SerializeField] string sha256;
        [SerializeField] byte[] chunkData;
        [SerializeField] byte[] positionData;
        [SerializeField] byte[] otherData;
        [SerializeField] byte[] colorData;

        public int SourceIndex => sourceIndex;
        public int SplatCount => splatCount;
        public Vector3 BoundsMin => boundsMin;
        public Vector3 BoundsMax => boundsMax;
        public string Sha256 => sha256;
        public int ChunkDataLength => chunkData?.Length ?? 0;
        public int PositionDataLength => positionData?.Length ?? 0;
        public int OtherDataLength => otherData?.Length ?? 0;
        public int ColorDataLength => colorData?.Length ?? 0;

        public void Initialize(int sourceFrame, int count, Vector3 minimum, Vector3 maximum,
            string contentSha256, byte[] chunks, byte[] positions, byte[] other, byte[] colors)
        {
            sourceIndex = sourceFrame;
            splatCount = count;
            boundsMin = minimum;
            boundsMax = maximum;
            sha256 = contentSha256;
            chunkData = chunks;
            positionData = positions;
            otherData = other;
            colorData = colors;
        }

        public GaussianSplatAsset CreateRuntimeAsset()
        {
            Validate();
            var runtimeAsset = CreateInstance<GaussianSplatAsset>();
            runtimeAsset.name = name + " Runtime";
            runtimeAsset.Initialize(
                splatCount,
                GaussianSplatAsset.VectorFormat.Norm16,
                GaussianSplatAsset.VectorFormat.Norm11,
                GaussianSplatAsset.ColorFormat.Norm8x4,
                GaussianSplatAsset.SHFormat.Float32,
                boundsMin,
                boundsMax,
                null);
            runtimeAsset.runtimeChunkData = DecodeChunks(chunkData);
            runtimeAsset.runtimePosData = positionData;
            runtimeAsset.runtimeOtherData = otherData;
            runtimeAsset.runtimeColorData = colorData;
            runtimeAsset.runtimeSHData = null;
            runtimeAsset.SetDataHash(Hash128.Compute(sha256));
            return runtimeAsset;
        }

        public void Validate()
        {
            if (splatCount <= 0 || splatCount > GaussianSplatAsset.kMaxSplats)
                throw new InvalidOperationException("Static QSF2 has an invalid splat count.");

            int expectedChunks = ((splatCount + GaussianSplatAsset.kChunkSize - 1) / GaussianSplatAsset.kChunkSize) * ChunkByteSize;
            int expectedPositions = Align4(checked(splatCount * 6));
            int expectedOther = checked(splatCount * 8);
            int expectedColor = checked((int)GaussianSplatAsset.CalcColorDataSize(splatCount, GaussianSplatAsset.ColorFormat.Norm8x4));
            if (ChunkDataLength != expectedChunks || PositionDataLength != expectedPositions ||
                OtherDataLength != expectedOther || ColorDataLength != expectedColor)
                throw new InvalidOperationException("Static QSF2 payload lengths do not match its splat count.");
        }

        static int Align4(int value) => checked((value + 3) & ~3);

        static GaussianSplatAsset.ChunkInfo[] DecodeChunks(byte[] bytes)
        {
            int count = bytes.Length / ChunkByteSize;
            var chunks = new GaussianSplatAsset.ChunkInfo[count];
            for (int index = 0; index < count; index++)
            {
                int offset = index * ChunkByteSize;
                chunks[index] = new GaussianSplatAsset.ChunkInfo
                {
                    colR = ReadUInt(bytes, offset),
                    colG = ReadUInt(bytes, offset + 4),
                    colB = ReadUInt(bytes, offset + 8),
                    colA = ReadUInt(bytes, offset + 12),
                    posX = new float2(ReadFloat(bytes, offset + 16), ReadFloat(bytes, offset + 20)),
                    posY = new float2(ReadFloat(bytes, offset + 24), ReadFloat(bytes, offset + 28)),
                    posZ = new float2(ReadFloat(bytes, offset + 32), ReadFloat(bytes, offset + 36)),
                    sclX = ReadUInt(bytes, offset + 40),
                    sclY = ReadUInt(bytes, offset + 44),
                    sclZ = ReadUInt(bytes, offset + 48),
                    shR = ReadUInt(bytes, offset + 52),
                    shG = ReadUInt(bytes, offset + 56),
                    shB = ReadUInt(bytes, offset + 60)
                };
            }
            return chunks;
        }

        static uint ReadUInt(byte[] bytes, int offset)
        {
            return (uint)(bytes[offset] | bytes[offset + 1] << 8 | bytes[offset + 2] << 16 | bytes[offset + 3] << 24);
        }

        static float ReadFloat(byte[] bytes, int offset)
        {
            return BitConverter.Int32BitsToSingle((int)ReadUInt(bytes, offset));
        }
    }
}
