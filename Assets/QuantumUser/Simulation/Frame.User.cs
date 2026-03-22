using System;
using System.Runtime.InteropServices;

namespace Quantum {
    public unsafe partial class Frame {

        public StageTileInstance* StageTiles;
        public int StageTilesLength;

        partial void FreeUser() {
            if (StageTiles != null) {
                Marshal.FreeHGlobal((IntPtr) StageTiles);
                StageTiles = null;
            }
        }

        partial void SerializeUser(FrameSerializer serializer) {
            var stream = serializer.Stream;

            // Tilemap
            if (stream.Writing) {
                stream.WriteInt(StageTilesLength);
            } else {
                int newLength = stream.ReadInt();
                ReallocStageTiles(newLength);
            }
            for (int i = 0; i < StageTilesLength; i++) {
                StageTileInstance.Serialize(StageTiles + i, serializer);
            }
        }

        partial void CopyFromUser(Frame frame) {
            ReallocStageTiles(frame.StageTilesLength);
            Buffer.MemoryCopy(frame.StageTiles, StageTiles, StageTileInstance.SIZE * frame.StageTilesLength, StageTileInstance.SIZE * frame.StageTilesLength);
        }

        public void ReallocStageTiles(int newSize) {
            if (StageTilesLength == newSize) {
                return;
            }

            if (StageTiles != null) {
                Marshal.FreeHGlobal((IntPtr) StageTiles);
                StageTiles = null;
            }

            if (newSize > 0) {
                StageTiles = (StageTileInstance*) Marshal.AllocHGlobal(StageTileInstance.SIZE * newSize);
            }

            StageTilesLength = newSize;
        }
    }
}