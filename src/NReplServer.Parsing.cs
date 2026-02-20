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
    public partial class NReplServer
    {
        private string ExtractPrefixFromRequest(Dictionary<string, object> request)
        {
            // Try to derive prefix from line/buffer + cursor position
            var line = request.GetValueOrDefault("line") as string;
            var buffer = request.GetValueOrDefault("buffer") as string
                ?? request.GetValueOrDefault("code") as string
                ?? request.GetValueOrDefault("text") as string;

            int? pos = TryGetInt(request, "pos")
                ?? TryGetInt(request, "cursor")
                ?? TryGetInt(request, "cursor-pos")
                ?? TryGetInt(request, "column");

            if (!string.IsNullOrEmpty(line))
            {
                var slice = line;
                if (pos.HasValue)
                {
                    var p = Math.Max(0, Math.Min(pos.Value, line.Length));
                    slice = line.Substring(0, p);
                }
                return ExtractToken(slice);
            }

            if (!string.IsNullOrEmpty(buffer))
            {
                var slice = buffer;
                if (pos.HasValue)
                {
                    var p = Math.Max(0, Math.Min(pos.Value, buffer.Length));
                    slice = buffer.Substring(0, p);
                }
                return ExtractToken(slice);
            }

            return "";
        }

        private string GetContextSlice(Dictionary<string, object> request)
        {
            var line = request.GetValueOrDefault("line") as string;
            var buffer = request.GetValueOrDefault("buffer") as string
                ?? request.GetValueOrDefault("code") as string
                ?? request.GetValueOrDefault("text") as string;

            int? pos = TryGetInt(request, "pos")
                ?? TryGetInt(request, "cursor")
                ?? TryGetInt(request, "cursor-pos")
                ?? TryGetInt(request, "column");

            if (!string.IsNullOrEmpty(line))
            {
                var slice = line;
                if (pos.HasValue)
                {
                    var p = Math.Max(0, Math.Min(pos.Value, line.Length));
                    slice = line.Substring(0, p);
                }
                return slice;
            }

            if (!string.IsNullOrEmpty(buffer))
            {
                var slice = buffer;
                if (pos.HasValue)
                {
                    var p = Math.Max(0, Math.Min(pos.Value, buffer.Length));
                    slice = buffer.Substring(0, p);
                }
                return slice;
            }

            return "";
        }

        private bool TryParseDotContext(string slice, out string receiver, out string memberPrefix)
        {
            receiver = "";
            memberPrefix = "";

            // Pattern: (. receiver memberPrefix)
            var m = Regex.Match(slice, @"\(\.\s+([^\s\)]+)\s+([^\s\)]*)$");
            if (m.Success)
            {
                receiver = m.Groups[1].Value;
                memberPrefix = m.Groups[2].Value;
                return true;
            }

            // Pattern: (.Member receiver)  -> method-first style
            m = Regex.Match(slice, @"\(\.([^\s\)]+)\s+([^\s\)]*)$");
            if (m.Success)
            {
                memberPrefix = m.Groups[1].Value;
                receiver = m.Groups[2].Value;
                return !string.IsNullOrEmpty(receiver);
            }

            return false;
        }

        private bool TryParseDotContextAny(string text, out string receiver, out string memberPrefix)
        {
            receiver = "";
            memberPrefix = "";
            if (string.IsNullOrEmpty(text)) return false;

            Match bestMatch = null;
            var matches1 = Regex.Matches(text, @"\(\.\s+([^\s\)]+)\s+([^\s\)]*)");
            foreach (Match m in matches1)
            {
                if (!m.Success) continue;
                if (bestMatch == null || m.Index > bestMatch.Index) bestMatch = m;
            }

            var matches2 = Regex.Matches(text, @"\(\.([^\s\)]+)\s+([^\s\)]*)");
            foreach (Match m in matches2)
            {
                if (!m.Success) continue;
                if (bestMatch == null || m.Index > bestMatch.Index) bestMatch = m;
            }

            if (bestMatch == null) return false;

            memberPrefix = bestMatch.Groups[1].Value;
            receiver = bestMatch.Groups[2].Value;

            if (bestMatch.Value.StartsWith("(. ", StringComparison.Ordinal))
            {
                receiver = bestMatch.Groups[1].Value;
                memberPrefix = bestMatch.Groups[2].Value;
            }

            return !string.IsNullOrEmpty(memberPrefix);
        }

        private bool TryGetContextReceiver(Dictionary<string, object> request, out string receiver)
        {
            receiver = "";
            if (!request.TryGetValue("context", out var ctx) || ctx == null) return false;

            if (ctx is List<object> list)
            {
                var parts = new List<string>();
                foreach (var item in list)
                {
                    if (item == null) continue;
                    parts.Add(item.ToString());
                }

                if (parts.Count == 0) return false;

                var prefixIndex = parts.IndexOf("__prefix__");
                if (prefixIndex >= 0 && prefixIndex + 1 < parts.Count)
                {
                    receiver = parts[prefixIndex + 1];
                    return !string.IsNullOrEmpty(receiver);
                }

                if (parts.Count >= 1)
                {
                    receiver = parts[parts.Count - 1];
                    return !string.IsNullOrEmpty(receiver);
                }
            }
            else if (ctx is string s)
            {
                var tokens = Regex.Matches(s, @"[^\s\(\)\[\]]+");
                var parts = new List<string>();
                foreach (Match m in tokens)
                {
                    if (!m.Success) continue;
                    parts.Add(m.Value);
                }
                if (parts.Count == 0) return false;

                var prefixIndex = parts.IndexOf("__prefix__");
                if (prefixIndex >= 0 && prefixIndex + 1 < parts.Count)
                {
                    receiver = parts[prefixIndex + 1];
                    return !string.IsNullOrEmpty(receiver);
                }

                receiver = parts[parts.Count - 1];
                return !string.IsNullOrEmpty(receiver);
            }

            return false;
        }

        private string ExtractToken(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            int i = s.Length - 1;
            while (i >= 0 && IsTokenChar(s[i])) i--;
            return s.Substring(i + 1);
        }

        private bool IsTokenChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '+' || c == '*'
                || c == '?' || c == '!' || c == '$' || c == '<' || c == '>' || c == '='
                || c == '.' || c == ':' || c == '/' || c == '\\' || c == '\'';
        }

        private int? TryGetInt(Dictionary<string, object> request, string key)
        {
            if (!request.TryGetValue(key, out var val) || val == null) return null;
            try
            {
                if (val is int i) return i;
                if (val is long l) return (int)l;
                if (val is string s && int.TryParse(s, out var si)) return si;
            }
            catch { }
            return null;
        }

    }
}
