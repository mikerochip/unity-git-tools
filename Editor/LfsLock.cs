using System;

namespace MikeSchweitzer.Git.Editor
{
    [Serializable]
    public class LfsLock
    {
        public string _Path;
        public string _AssetGuid;
        public string _User;
        public string _Id;
        public bool _IsPending;
    }
}
