using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using clojure.lang;

namespace clojureCLR_nrepl
{
    internal sealed class NReplSession
    {
        public string Id { get; set; }
        public Namespace CurrentNamespace { get; set; }
    }
}
