using swigxml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace SwigToLLS
{
    internal class LLSWriter
    {
        List<string> lines = new List<string>();
        Parser parser;
        HashSet<string> unknownTypes = new HashSet<string>();

        Dictionary<string, LTypedef> typedefMap = null;
        Dictionary<string, LClass> classesMap = null;
        Dictionary<string, LEnum> enumMap = null;

        public LLSWriter(Parser p)
        {
            parser = p;            
        }

        public void Write()
        {
            BuildGlobalMaps();
            List<LItem> namespaces = new List<LItem>();
            GetAllItemsOfType(parser.Root, ItemType.Namespace, namespaces);
            foreach (var ns in namespaces)
            {
                lines.Clear();
                lines.Add(@"---");
                lines.Add(@"---@meta");

                Traverse(ns, "");
                string outname = ns.Name + ".lua";
                File.WriteAllLines(outname, lines);
            }

            foreach (var type in unknownTypes)
            {
                Console.WriteLine(type);
            }
        }

        void BuildGlobalMaps()
        {
            List<LItem> typedefs = new List<LItem>();
            GetAllItemsOfType(parser.Root, ItemType.Typedef, typedefs);

            typedefMap = typedefs.GroupBy(s => s.FullName.Replace(".", "::")).ToDictionary(g => g.Key, g => (g.First() as LTypedef));

            List<LItem> classes = new List<LItem>();
            GetAllItemsOfType(parser.Root, ItemType.Class, classes);

            classesMap = classes.GroupBy(s => s.FullName.Replace(".", "::")).ToDictionary(g => g.Key, g => (g.First() as LClass));

            List<LItem> enums = new List<LItem>();
            GetAllItemsOfType(parser.Root, ItemType.Enum, enums);

            enumMap = enums.GroupBy(s => s.FullName.Replace(".", "::")).ToDictionary(g => g.Key, g => (g.First() as LEnum));
        }

        void GetAllItemsOfType(LItem c, ItemType itemType, List<LItem> outItems)
        {
            if (c.LItemType == itemType &&
                c.Name != null)
            {
                outItems.Add(c);
            }
            else if (c.SubItems != null)
            {
                foreach (var subitem in c.SubItems)
                {
                    GetAllItemsOfType(subitem, itemType, outItems);
                }
            }
        }

        void WriteEnum(LEnum lenum, string parent)
        {
            lines.Add($"---@enum {lenum.Name}Enum");
            lines.Add($"{lenum.FullName} = {{");
            int ct = lenum.EnumVals.Count;
            int idx = 0;
            foreach (LEnumVal lEnumVal in lenum.EnumVals)
            {
                lines.Add($"    {lEnumVal.Name} = {lEnumVal.Value}" + (idx == ct - 1 ? "" : ","));
                idx++;
            }
            lines.Add($"}}");
            lines.Add("");

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
                lines.Add($"{c.FullName} = {{}}");
                lines.Add("");
            }

            if (c.LItemType == ItemType.Enum)
            {
                WriteEnum(c as LEnum, parent);
                return true;
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

        Regex regex = new Regex(@"std::shared_ptr<\((.*)\)>");
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

            if (regex.IsMatch(t))
            {
                Match m = regex.Match(t);
                t = m.Groups[1].Value;
                isPointer = true;
                //Console.WriteLine(t);
            }

            LTypedef resolveType;
            while (typedefMap.TryGetValue(t, out resolveType))
            {
                if (resolveType.IsPtr)
                    t = "any";
                else
                    t = resolveType.Type;
            }
            if (isPointer)
            {
                if (t == "char")
                    return "string";
                else if (classesMap.TryGetValue(t, out LClass ci))
                {
                    return ci.Name;
                }
                else
                    return "any";
            }
            else
            {
                if (t == "int" ||
                    t == "unsigned short" || t == "unsigned char" ||
                    t == "unsigned long long" || t == "unsigned long" ||
                    t == "unsigned long long" || t == "unsigned int" ||
                    t == "char")
                    return "integer";
                if (t == "float" || t == "double")
                    return "number";
                if (t == "bool")
                    return "boolean";
                if (t == "va_list")
                    return string.Empty;
            }
            if (t == "function" || t == "userdata")
                return t;
            if (t.EndsWith("::Enum"))
                t = t.Remove(t.IndexOf("::Enum"));
            if (classesMap.TryGetValue(t, out LClass classitem))
            {
                return classitem.Name;
            }
            else if (enumMap.TryGetValue(t, out LEnum enumItem))
            {
                return enumItem.Name + "Enum";
            }
            else
            {
                unknownTypes.Add(t);
            }
            return string.Empty;
        }

        bool CanExpose(FuncParams p)
        {
            foreach (var parm in p.Params)
            {
                if (parm.Name == null || parm.Name.Length == 0)
                    return false;
                string retType = GetLuaType(parm.Type);
                if (retType.Length == 0)
                    return false;
            }
            if (p.ReturnType != null &&
                p.ReturnType.Type != "void")
            {
                string t = GetLuaType(p.ReturnType.Type);
                if (t.Length == 0)
                    return false;
            }
            return true;
        }
        void WriteFunction(LFunc f, string parent)
        {
            if (f.SymName.Contains("::"))
            {
                Console.WriteLine($"{f.SymName}: Malformed name");
                return;
            }

            foreach (var p in f.FuncParams)
            {
                if (!CanExpose(p))
                    continue;
                string funcname = f.Parent.FullName;
                if (parent.Length > 0)
                {
                    funcname += f.IsStatic ? "." : ":";
                    funcname += f.SymName;
                }
                else
                    funcname = f.FullName;

                foreach (var parm in p.Params)
                {
                    lines.Add($"--- {parm.Type}");
                    lines.Add($"---@param {parm.Name}? {GetLuaType(parm.Type)}");
                }
                if (p.ReturnType != null)
                {
                    if (p.ReturnType.Type != "void")
                    {
                        string t = GetLuaType(p.ReturnType.Type);
                        lines.Add($"---@return {t}");
                    }
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
