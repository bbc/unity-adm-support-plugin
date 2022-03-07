using System;
using System.Runtime.InteropServices;

using UInt8 = System.Byte;
using Int8 = System.SByte;
using CppChar = System.Byte;
using CppBool = System.Byte;

using ADM;

namespace ADM
{

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct RawMetadataBlock
    {
        public UInt64 id;
        public UInt8 channelCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public UInt8[] channelNums;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 100)]
        public CppChar[] name;
        public UInt8 typeDef;

        public double audioStartTime;
        public double audioEndTime;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
        public CppChar[] audioPackFormatId;

        public double rTime;
        public double duration;

        public CppBool jumpPosition;
        public double interpolationLength;

        public CppBool cartesian;
        public double x;
        public double y;
        public double z;
        public double azimuth;
        public double elevation;
        public double distance;

        public double absoluteDistance;

        public double width;
        public double height;
        public double depth;
        public double gain;
        public double diffuse;

        public double divergence;
        public double divergenceAzimuthRange;
        public double divergencePositionRange;

        public CppBool channelLock;
        public double channelLockMaxDistance;

        public CppBool screenRef;

        double highPass;
        double lowPass;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public CppChar[] speakerLabel;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public CppChar[] normalisation;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public Int8[] order;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public Int8[] degree;
        public double nfcRefDist;
    };

    public class LibraryInterface
    {
        const string dll = "libunityadm";

        [DllImport(dll)]
        public static extern int readAdm(byte[] filePath);

        [DllImport(dll)]
        public static extern int getSampleRate();

        [DllImport(dll)]
        public static extern int getNumberOfFrames();

        [DllImport(dll)]
        public static extern unsafe bool getAudioBlock(int startFrame, int numFrames, int[] channelNums, int channelNumsSize, float[] outputBuffer);

        [DllImport(dll)]
        public static extern unsafe bool getAudioBlockBounded(int startFrame, int numFrames, int[] channelNums, int channelNumsSize, int lowerFrameBound, int upperFrameBound, float[] outputBuffer);

        [DllImport(dll)]
        public static extern int discoverNewRenderableItems();

        [DllImport(dll)]
        public static extern bool getNextMetadataBlock(ref RawMetadataBlock metadataBlock);

        [DllImport(dll, CharSet = CharSet.Ansi)]
        private static extern IntPtr getLatestException();
        public static string getLatestExceptionString()
        {
            return Marshal.PtrToStringAnsi(getLatestException());
        }

        // BEAR

        [DllImport(dll)]
        public static extern bool setupBear(int maxObjectsChannels, int maxDirectSpeakersChannels, int maxHoaChannels);

        [DllImport(dll)]
        public static extern bool setupBearEx(int maxObjectsChannels, int maxDirectSpeakersChannels, int maxHoaChannels, int maxAnticipatedBlockFrameRequest, int rendererInternalBlockFrameCount, byte[] dataPath, byte[] fftImpl);

        [DllImport(dll)]
        public static extern bool restartBear();

        [DllImport(dll)]
        public static extern bool prewarnBearRender(int startFrame, int numFrames);

        [DllImport(dll)]
        public static extern bool prewarnBearRenderSrc(int startFrame, int numFrames, int outputSampleRate, int srcType);

        [DllImport(dll)]
        public static extern unsafe bool getBearRender(int[] objectInputChannelNums, int objectInputChannelNumsSize,
                                            int[] directSpeakersInputChannelNums, int directSpeakersInputChannelNumsSize,
                                            int[] hoaInputChannelNums, int hoaInputChannelNumsSize,
                                            float[] outputBuffer);

        [DllImport(dll)]
        public static extern unsafe bool getBearRenderBounded(int[] objectInputChannelNums, ChannelAudioBounds[] objectInputAudioBounds, int objectInputCount,
                                            int[] directSpeakersInputChannelNums, ChannelAudioBounds[] directSpeakersInputAudioBounds, int directSpeakersInputCount,
                                            int[] hoaInputChannelNums, ChannelAudioBounds[] hoaInputAudioBounds, int hoaInputCount,
                                            float[] outputBuffer, int outputBufferStartFrame, bool outputOverwrite);

        [DllImport(dll)]
        public static extern void setBearOutputGain(float gain);

        [DllImport(dll)]
        public static extern unsafe bool addBearObjectMetadata(int forBearChannel, ref RawMetadataBlock metadataBlock);

        [DllImport(dll)]
        public static extern unsafe bool addBearDirectSpeakersMetadata(int forBearChannel, ref RawMetadataBlock metadataBlock);

        [DllImport(dll)]
        public static extern unsafe bool addBearHoaMetadata(int[] forBearChannels, ref RawMetadataBlock metadataBlock);

        [DllImport(dll)]
        public static extern bool setListener(float position_x, float position_y, float position_z, float orientation_w, float orientation_x, float orientation_y, float orientation_z);

        // Methods to determine coordinate system

        [DllImport(dll)]
        public static extern unsafe bool getListenerLook(float* orientation_x, float* orientation_y, float* orientation_z);
        public static unsafe UnityEngine.Vector3 getLookVec()
        {
            float x, y, z;
            getListenerLook(&x, &y, &z);
            return new UnityEngine.Vector3(x, y, z);
        }

        [DllImport(dll)]
        public static extern unsafe bool getListenerUp(float* orientation_x, float* orientation_y, float* orientation_z);
        public static unsafe UnityEngine.Vector3 getUpVec()
        {
            float x, y, z;
            getListenerUp(&x, &y, &z);
            return new UnityEngine.Vector3(x, y, z);
        }

        [DllImport(dll)]
        public static extern unsafe bool getListenerRight(float* orientation_x, float* orientation_y, float* orientation_z);
        public static unsafe UnityEngine.Vector3 getRightVec()
        {
            float x, y, z;
            getListenerRight(&x, &y, &z);
            return new UnityEngine.Vector3(x, y, z);
        }
    }
}