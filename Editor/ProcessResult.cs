using System.Collections.Generic;

namespace GitTools.Editor
{
    public class ProcessResult
    {
        public List<string> OutLines { get; set; } = new List<string>();
        public List<string> ErrorLines { get; set; } = new List<string>();
    }
}