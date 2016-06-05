using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LyncHEntity
{
    [Serializable]
    public class LyncMessage
    {
        public long ContactId{get;set;}

        public DateTime MessageTime { get; set; }        
        public string MessageText { get; set; }
        public bool IsCompressed { get; set; }
    }
}
