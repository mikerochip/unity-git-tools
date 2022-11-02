using System.Collections.Generic;

namespace GitTools.Editor
{
    public struct ProcessResult
    {
        public List<string> OutLines { get; set; }
        public List<string> ErrorLines { get; set; }
    }
}