using swigxml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace SwigToLLS
{
    internal class LLSWriter
    {
        List<string> lines = new List<string>();
        Parser parser;
        public LLSWriter(Parser p)
        {
            parser = p;            
        }

        public void Write(string outfile)
        {
            List<LItem> namespaces = new List<LItem>();
            GetAllNamespaces(parser.Root, namespaces);
            foreach (var ns in namespaces)
            {
                lines.Clear();
                lines.Add(@"---");
                lines.Add(@"---@meta");

                Traverse(ns, "");
                string outname = ns.Name + ".lua";
                File.WriteAllLines(outname, lines);
            }
        }

        void GetAllNamespaces(LItem c, List<LItem> namespaces)
        {
            if (c.LItemType == ItemType.Namespace &&
                c.Name != null)
            {
                namespaces.Add(c);
            }
            else if (c.SubItems != null)
            {
                foreach (var subitem in c.SubItems)
                {
                    GetAllNamespaces(subitem, namespaces);
                }
            }
        }

        bool Traverse(LItem c, string parent)
        {
            string fullname = parent;
            if (c.LItemType == ItemType.Namespace &&
                c.Name != null)
            {                
                lines.Add($"local {c.Name} = {{}}");
                lines.Add("");
            }

            if (c.LItemType == ItemType.Class)
            {
                if (fullname.Length > 0) fullname += ".";
                fullname += c.SymName;
                //if (c.Name == "vector<(double)>")
                //    Debugger.Break();
                lines.Add($"---@class {fullname}");
                lines.Add($"local {fullname} = {{}}");
                lines.Add("");
            }

            if (c.LItemType == ItemType.Function)
            {
                WriteFunction(c as LFunc, parent);
                return true;
            }

            if (c.SubItems == null)
                return true;

            bool shouldContinue = true;
            foreach (var subitem in c.SubItems)
            {
                shouldContinue &= Traverse(subitem, fullname);
                if (!shouldContinue)
                    break;
            }
            if (c.LItemType == ItemType.Namespace &&
                c.Name != null)
            {
                lines.Add("");
                lines.Add($"return {c.Name}");
            }

            return shouldContinue;
        }


        string GetLuaType(string t)
        {
            bool isPointer = false;
            if (t.Contains("."))
            {
                string[]quals = t.Split('.');

                if (quals[0] == "p")
                    isPointer = true;
                t = t.Substring(t.LastIndexOf(".") + 1);
            }

            if (t == "int" ||
                t == "uint16_t" || t == "uint8_t" ||
                t == "uint64_t" || t == "uint32_t")
                return "integer";
            if (t == "float")
                return "number";
            if (t == "bool")
                return "boolean";
            if (t == "char" && isPointer)
            {
                return "string";
            }
            if (t.Contains("::"))
            {
                t = t.Substring(t.IndexOf("::") + 2);
            }

            return t;
        }
        void WriteFunction(LFunc f, string parent)
        {
            foreach (var p in f.FuncParams)
            {
                string funcname = parent;
                if (parent.Length > 0)
                    funcname += f.IsStatic ? "." : ":";
                funcname += f.SymName;

                foreach (var parm in p.Params)
                {
                    lines.Add($"--- {parm.Type}");
                    lines.Add($"---@param {parm.Name}? {GetLuaType(parm.Type)}");
                }
                if (p.ReturnType != null)
                {
                    string t = GetLuaType(p.ReturnType.Type);
                    if (t != "void")
                        lines.Add($"---@return {t}");
                }
                string funline = $"function {funcname}(";
                funline += string.Join(", ", p.Params.Select(pm => pm.Name));
                funline += ") end";
                lines.Add(funline);
                lines.Add("");
            }
        }
    }
}
