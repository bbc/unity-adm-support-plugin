using System.Collections.Generic;
using System;
using ADM;

namespace ADM
{

    public enum AudioRendererType
    {
        NONE,
        UNITY,
        BEAR
    };

    public interface AudioRenderer
    {
        void scheduleAudioPlayback(double forDspTime);
        void stopAudioPlayback();
        void configureNewItems(ref List<UInt64> itemsAwaitingConfig);
        void handleMetadataUpdate(MetadataUpdate metadataUpdate);
        void onUpdateBegin();
        void onUpdateEnd();
        void shutdown();
    }

}
