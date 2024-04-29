using System;
using System.Diagnostics;
using System.Xml;

namespace swigxml
{
    public enum ItemType
    {
        Class,
        Function,
        Namespace,
        Variable,
        Typedef,
        Enum
    }
    public interface LItem
    {
        public LItem Parent { get; set; }
        public string Name { get; set; }
        public List<LItem> SubItems { get; }
        public ItemType LItemType { get; }
        public string Description { get; }
        public string FullName {  get; }
        public string SymName { get; }
        public void SortItems();
    }

    public class LItemBase
    {

    }
    public class Param
    {
        public string Type { get; set; } = "";
        public string Name { get; set; } = "";

    }

    public class FuncParams
    {
        public List<Param> Params { get; set; } = new List<Param>();
        public Param? ReturnType { get; set; }
    }

    public class LFunc : LItem
    {
        public LItem Parent { get; set; }

        public string Name { get; set; }

        public List<FuncParams> FuncParams { get; set; }

        public List<LItem> SubItems => null;
        public ItemType LItemType => ItemType.Function;
        public string SymName { get; set; }
        public bool IsStatic { get; set; }
        public string Description
        {
            get
            {
                List<string> funcs = new List<string>();
                foreach (var fp in FuncParams)
                {
                    string paramline = string.Join(',', fp.Params.Select(p => $"{Parser.GetCppType(p.Type)} {p.Name}"));
                    funcs.Add($"{fp.ReturnType?.Type} {FullName}({paramline});");
                }
                return string.Join('\n', funcs);
            }
        }

        public string FullName => (Parent != null && Parent.SymName != null) ? Parent.FullName + "." + SymName : SymName;

        public void SortItems()
        {
        }
    }
    public class LVar : LItem
    {
        public LItem Parent { get; set; }

        public string Name { get; set; }

        public string Type { get; set; }
        public List<LItem> SubItems => null;
        public ItemType LItemType => ItemType.Variable;
        public string SymName => Name;
        public string Description
        {
            get
            {
                return $"{Type} {FullName}";
            }
        }
        public string FullName => (Parent != null && Parent.SymName != null) ? Parent.FullName + "." + SymName : SymName;

        public void SortItems()
        {
        }
    }

    public class LClass : LItem
    {
        public LItem Parent { get; set; }

        public string SymName { get; set; }
        public string Name { get; set; }
        public List<LFunc> Funcs { get; set; } = new List<LFunc>();
        public List<LVar> Variables { get; set; } = new List<LVar>();

        public List<LClass> SubClasses { get; set; } = new List<LClass>();
        public List<LTypedef> Typedefs { get; set; } = new List<LTypedef>();
        public List<LEnum> Enums { get; set; } = new List<LEnum>();

        
        void LItem.SortItems()
        {
            Funcs.Sort((a, b) => a.SymName.CompareTo(b.SymName)); 
            SubClasses.Sort((a, b) => a.SymName.CompareTo(b.SymName));
            Variables.Sort((a, b) => a.SymName.CompareTo(b.SymName));
        }

        public List<LItem> SubItems => Typedefs.Cast<LItem>().
            Concat(SubClasses.Cast<LItem>()).Concat(Funcs.Cast<LItem>()).Concat(Variables.Cast<LItem>()).
            Concat(Enums.Cast<LItem>()).
            ToList();
        public virtual ItemType LItemType => ItemType.Class;

        public string Description => Name;
        public string FullName => (Parent != null && Parent.SymName != null) ? Parent.FullName + "." + SymName : SymName;

    }
    public class LTypedef : LItem
    {
        public LItem Parent { get; set; }

        public string Name { get; set; }

        public string Type { get; set; }

        public bool IsPtr { get; set; }
        public List<LItem> SubItems => null;
        public ItemType LItemType => ItemType.Typedef;
        public string SymName => Name;
        public string Description
        {
            get
            {
                return $"{Type} {FullName}";
            }
        }
        public string FullName => (Parent != null && Parent.SymName != null) ? Parent.FullName + "." + SymName : SymName;

        public void SortItems()
        {
        }
    }

    public class LEnum : LItem
    {
        public LItem Parent { get; set; }

        public string Name { get; set; }

        public string Type { get; set; }
        public List<LEnumVal> EnumVals { get; set; } = new List<LEnumVal>();

        public bool IsPtr { get; set; }
        public List<LItem> SubItems => null;
        public ItemType LItemType => ItemType.Enum;
        public string SymName => Name;
        public string Description
        {
            get
            {
                return $"{Type} {FullName}";
            }
        }
        public string FullName => (Parent != null && Parent.SymName != null) ? Parent.FullName + "." + SymName : SymName;

        public void SortItems()
        {
        }
    }

    public class LEnumVal
    {
        public string Name { get; set; }
        public int Value { get; set; }
    }
    public class LNamespace : LClass
    {
        public override ItemType LItemType => ItemType.Namespace;
    }
    public class Parser
    {
        public LClass Root { get;  }

        public Parser(string file)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(file);
            XmlElement node = doc.DocumentElement.SelectSingleNode("/top/include/module") as XmlElement;
            string modulename = GetElementProp(node, "name");
            XmlElement parentInclude = node.ParentNode as XmlElement;
            LClass globalClass = new LClass();
            RecursiveParse(parentInclude, globalClass);
            Root = BuildNamespacesNodes(globalClass);
            ConsolidateFunctions(Root);
            SetParents(Root);
            SortItems(Root);
        }


        void SetParents(LItem item)
        {
            if (item.SubItems != null)
            {
                foreach (LItem subItem in item.SubItems)
                {
                    subItem.Parent = item;
                    SetParents(subItem);
                }
            }
        }
        void SortItems(LItem item)
        {
            item.SortItems();
            if (item.SubItems != null)
            {
                foreach (LItem subItem in item.SubItems)
                {
                    SortItems(subItem);
                }
            }
        }

        LClass BuildNamespacesNodes(LClass g)
        {
            Dictionary<string, LNamespace> namespsace = new Dictionary<string, LNamespace>();
            LNamespace topclass = new LNamespace();
            foreach (var child in g.SubItems)
            {
                if (child.Name != null && child.Name.Contains("::"))
                {
                    string ns = child.Name.Substring(0, child.Name.IndexOf("::"));
                    LNamespace? nsclass;
                    if (!namespsace.TryGetValue(ns, out nsclass))
                    {
                        nsclass = new LNamespace();
                        nsclass.Name = ns;
                        nsclass.SymName = ns;
                        namespsace.Add(ns, nsclass);
                    }
                    child.Name = child.Name.Substring(child.Name.IndexOf("::") + 2);
                    if (child is LClass lclass)
                        nsclass.SubClasses.Add(lclass);
                    else if (child is LFunc lfunc)
                        nsclass.Funcs.Add(lfunc);
                    else if (child is LVar lvar)
                        nsclass.Variables.Add(lvar);
                    else if (child is LTypedef typedef)
                        nsclass.Typedefs.Add(typedef);
                    else if (child is LEnum lenum)
                        nsclass.Enums.Add(lenum);
                }
                else
                {
                    if (child is LClass lclass)
                        topclass.SubClasses.Add(lclass);
                    else if (child is LFunc lfunc)
                        topclass.Funcs.Add(lfunc);
                    else if (child is LVar lvar)
                        topclass.Variables.Add(lvar);
                    else if (child is LTypedef typedef)
                        topclass.Typedefs.Add(typedef);
                    else if (child is LEnum lenum)
                        topclass.Enums.Add(lenum);
                }
            }
            topclass.SubClasses.AddRange(namespsace.Select(kv => kv.Value));
            return topclass;
        }


        void ConsolidateFunctions(LItem item)
        {
            if (item is LClass lclass)
            {
                Dictionary<string, LFunc> funcMap = new Dictionary<string, LFunc>();                
                foreach (LFunc func in lclass.Funcs)
                {
                    if (func.Name == null)
                        continue;
                    LFunc outfunc;
                    if (!funcMap.TryGetValue(func.Name, out outfunc))
                    {
                        funcMap.Add(func.Name, func);
                    }
                    else
                    {
                        outfunc.FuncParams.Add(func.FuncParams.First());
                    }
                }

                lclass.Funcs = funcMap.Select(kv => kv.Value).ToList(); 
            }
            if (item.SubItems != null)
            {
                foreach (var child in item.SubItems)
                {
                    ConsolidateFunctions(child);
                }
            }
        }

        static string GetElementProp(XmlElement el, string prop)
        {
            XmlElement att = el.SelectSingleNode($"attributelist/attribute[@name='{prop}']") as XmlElement;
            return att?.GetAttribute("value");
        }

        bool IsPublic(XmlElement node)
        {
            string acc = GetElementProp(node, "access");
            return acc == null || acc == "public";
        }
        void RecursiveParse(XmlElement node, LClass curClass)
        {
            if (node.Name == "class")
            {
                LClass lclass = new LClass();
                lclass.Name = GetElementProp(node, "name");
                lclass.SymName = GetElementProp(node, "sym_name");
                if (lclass.SymName == null)
                {
                    Console.WriteLine($"No symname for {lclass.Name}");
                }
                else
                {
                    foreach (XmlElement child in node.ChildNodes)
                    { RecursiveParse(child, lclass); }

                    if (lclass.Enums.Count == 1 &&
                        lclass.SubClasses.Count == 0 &&
                        lclass.Funcs.Count == 0
                        )
                    {
                        lclass.Enums[0].Name = lclass.Name;
                        curClass.Enums.Add(lclass.Enums[0]);
                    }
                    else
                        curClass.SubClasses.Add(lclass);
                }
            }            
            else if (node.Name == "cdecl")
            {
                string kindprop = GetElementProp(node, "kind");
                if (kindprop == "function" &&
                    IsPublic(node))
                {
                    LFunc lfunc = new LFunc();
                    lfunc.Name = GetElementProp(node, "name");
                    lfunc.SymName = GetElementProp(node, "sym_name");
                    if (lfunc.SymName == null)
                        lfunc.SymName = lfunc.Name;
                    
                    string view = GetElementProp(node, "view");
                    lfunc.IsStatic = (view == "staticmemberfunctionHandler");
                    GetFuncParams(node, lfunc);

                    curClass.Funcs.Add(lfunc);
                }
                else if (kindprop == "variable" &&
                    IsPublic(node))
                {
                    LVar lvar = new LVar();
                    lvar.Name = GetElementProp(node, "name");
                    lvar.Type = GetCppType(GetElementProp(node, "type"));

                    curClass.Variables.Add(lvar);
                }
                else if (kindprop == "typedef" &&
                    IsPublic(node))
                {
                    LTypedef ltd = new LTypedef();
                    ltd.Name = GetElementProp(node, "name");
                    ltd.Type = GetCppType(GetElementProp(node, "type"));
                    if (ltd.Type == "SWIGLUA_REF")
                        ltd.Type = "function";
                    string decl = GetElementProp(node, "decl");
                    ltd.IsPtr = decl.StartsWith("p.");

                    curClass.Typedefs.Add(ltd);
                }
            }
            else if (node.Name == "constructor" &&
                    IsPublic(node))
            {
                LFunc lfunc = new LFunc();
                lfunc.Name = lfunc.SymName = "new";
                GetFuncParams(node, lfunc);
                curClass.Funcs.Add(lfunc);
            }
            else if (node.Name == "template") { }
            else if (node.Name == "enum")
            {
                LEnum lenum = new LEnum();
                lenum.Name = GetElementProp(node, "sym_name");
                if (lenum.Name != null)
                {
                    foreach (XmlElement child in node.ChildNodes)
                    {
                        if (child.Name == "enumitem")
                        {
                            LEnumVal lEnumVal = new LEnumVal();
                            lEnumVal.Name = GetElementProp(child, "sym_name");
                            string exval = GetElementProp(child, "enumvalueex");
                            if (int.TryParse(exval, out int pval))
                                lEnumVal.Value = pval;
                            else
                                lEnumVal.Value = -1;
                            lenum.EnumVals.Add(lEnumVal);
                        }
                    }
                    curClass.Enums.Add(lenum);
                }
            }
            else
            {
                foreach (XmlElement child in node.ChildNodes)
                {
                    RecursiveParse(child, curClass);
                }
            }
        }

        public static string GetCppType(string reftype)
        {
            if (reftype == null)
                return null;
            string[] quals = reftype.Split('.');
            string cpptype = quals[quals.Length - 1];
            for (int idx = 0; idx < quals.Length - 1; ++idx)
            {
                if (quals[idx] == "r")
                    cpptype += "&";
                else if (quals[idx] == "p")
                    cpptype += "*";
                else if (quals[idx] == "q(const)")
                    cpptype = "const " + cpptype;
                else if (quals[idx].StartsWith("a("))
                    cpptype += "[]";
                else
                    return reftype;
            }
            return cpptype;
        }

        void GetFuncParams(XmlElement funcElement, LFunc func)
        {
            XmlNodeList nl = funcElement.SelectNodes("attributelist/parmlist/parm");
            FuncParams fp = new FuncParams();
            fp.Params = new List<Param>();
            foreach (XmlElement param in nl)
            {
                Param param1 = new Param();
                param1.Type = GetElementProp(param, "type");
                param1.Name = GetElementProp(param, "name");
                fp.Params.Add(param1);
            }
            string returnType = GetElementProp(funcElement, "type");
            fp.ReturnType = returnType != null ? new Param() { Type = returnType } : null;
            func.FuncParams = new List<FuncParams> { fp };
        }
    }
}