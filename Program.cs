// See https://aka.ms/new-console-template for more information

using SwigToLLS;
using swigxml;

Parser p = new Parser("swig_lua.xml");
LLSWriter lLSWriter = new LLSWriter(p);
lLSWriter.Write();
