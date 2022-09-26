using System;

namespace GitTools.Editor
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
