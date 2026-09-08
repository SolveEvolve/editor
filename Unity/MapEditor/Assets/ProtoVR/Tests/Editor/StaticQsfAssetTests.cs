using GaussianSplatting.Runtime;
using NUnit.Framework;
using ProtoVR.Splats;
using UnityEditor;
using UnityEngine;

namespace ProtoVR.Tests
{
    public sealed class StaticQsfAssetTests
    {
        const string ProtoScanPath = "Assets/ProtoVR/Scans/proto-august-scan-464k.qsf";

        [Test]
        public void ImportedProtoScanHasExpectedStaticProfile()
        {
            var asset = AssetDatabase.LoadAssetAtPath<StaticQsfAsset>(ProtoScanPath);

            Assert.That(asset, Is.Not.Null);
            Assert.That(asset.SplatCount, Is.EqualTo(464114));
            Assert.That(asset.SourceIndex, Is.Zero);
            Assert.That(asset.Sha256, Is.EqualTo("68e53190715884b0ab4b373ce6795ceba8f8c09524110e806053609e1ce06748"));
            Assert.DoesNotThrow(asset.Validate);
        }

        [Test]
        public void RuntimeAssetUsesQuestFormatsWithoutSequenceState()
        {
            var source = AssetDatabase.LoadAssetAtPath<StaticQsfAsset>(ProtoScanPath);
            var runtime = source.CreateRuntimeAsset();
            try
            {
                Assert.That(runtime.splatCount, Is.EqualTo(464114));
                Assert.That(runtime.posFormat, Is.EqualTo(GaussianSplatAsset.VectorFormat.Norm16));
                Assert.That(runtime.scaleFormat, Is.EqualTo(GaussianSplatAsset.VectorFormat.Norm11));
                Assert.That(runtime.colorFormat, Is.EqualTo(GaussianSplatAsset.ColorFormat.Norm8x4));
                Assert.That(runtime.HasChunkData, Is.True);
                Assert.That(runtime.HasSHData, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(runtime);
            }
        }

        [Test]
        public void InvalidPayloadLengthsAreRejected()
        {
            var asset = ScriptableObject.CreateInstance<StaticQsfAsset>();
            try
            {
                asset.Initialize(0, 1, Vector3.zero, Vector3.one, "test", new byte[64], new byte[7], new byte[8], new byte[131072]);
                Assert.Throws<System.InvalidOperationException>(asset.Validate);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }
    }
}
