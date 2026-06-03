using System;
using System.Runtime.InteropServices;

namespace ColetorProfitRTD.Rtd
{
    [ComImport]
    [Guid("EC0E6191-DB51-11D3-8F3E-00C04F3651B8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    public interface IRtdServer
    {
        [DispId(10)]
        int ServerStart(IRTDUpdateEvent callback);

        [DispId(11)]
        object ConnectData(int topicId, ref Array strings, ref bool getNewValues);

        [DispId(12)]
        Array RefreshData(ref int topicCount);

        [DispId(13)]
        void DisconnectData(int topicId);

        [DispId(14)]
        int Heartbeat();

        [DispId(15)]
        void ServerTerminate();
    }
}
