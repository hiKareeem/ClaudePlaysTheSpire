using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

class Program
{
    static void Main(string[] args)
    {
        var path = args[0];
        var targets = new HashSet<string>(args.Skip(1), StringComparer.Ordinal);
        using var fs = File.OpenRead(path);
        using var peReader = new PEReader(fs);
        var mr = peReader.GetMetadataReader();

        foreach (var th in mr.TypeDefinitions)
        {
            var td = mr.GetTypeDefinition(th);
            var name = mr.GetString(td.Name);
            if (!targets.Contains(name)) continue;
            var ns = mr.GetString(td.Namespace);
            Console.WriteLine($"=== {ns}.{name} ===");

            foreach (var fh in td.GetFields())
            {
                var fd = mr.GetFieldDefinition(fh);
                var fname = mr.GetString(fd.Name);
                var sig = fd.DecodeSignature(new SigProvider(mr), null);
                Console.WriteLine($"  F {sig} {fname}");
            }
            foreach (var ph in td.GetProperties())
            {
                var pd = mr.GetPropertyDefinition(ph);
                var pname = mr.GetString(pd.Name);
                var sig = pd.DecodeSignature(new SigProvider(mr), null);
                Console.WriteLine($"  P {sig.ReturnType} {pname}");
            }
            foreach (var mh in td.GetMethods())
            {
                var md = mr.GetMethodDefinition(mh);
                var mname = mr.GetString(md.Name);
                var sig = md.DecodeSignature(new SigProvider(mr), null);
                var ps = string.Join(", ", sig.ParameterTypes);
                Console.WriteLine($"  M {sig.ReturnType} {mname}({ps})");
            }
        }
    }
}

class SigProvider : ISignatureTypeProvider<string, object?>
{
    readonly MetadataReader _mr;
    public SigProvider(MetadataReader mr) { _mr = mr; }
    public string GetArrayType(string e, ArrayShape s) => e + "[]";
    public string GetByReferenceType(string e) => "ref " + e;
    public string GetFunctionPointerType(MethodSignature<string> s) => "fnptr";
    public string GetGenericInstantiation(string g, System.Collections.Immutable.ImmutableArray<string> ta) => g + "<" + string.Join(",", ta) + ">";
    public string GetGenericMethodParameter(object? gc, int index) => "!!" + index;
    public string GetGenericTypeParameter(object? gc, int index) => "!" + index;
    public string GetModifiedType(string mod, string t, bool req) => t;
    public string GetPinnedType(string e) => e;
    public string GetPointerType(string e) => e + "*";
    public string GetPrimitiveType(PrimitiveTypeCode t) => t.ToString();
    public string GetSZArrayType(string e) => e + "[]";
    public string GetTypeFromDefinition(MetadataReader r, TypeDefinitionHandle h, byte rk)
    { var td = r.GetTypeDefinition(h); return r.GetString(td.Name); }
    public string GetTypeFromReference(MetadataReader r, TypeReferenceHandle h, byte rk)
    { var tr = r.GetTypeReference(h); return r.GetString(tr.Name); }
    public string GetTypeFromSpecification(MetadataReader r, object? gc, TypeSpecificationHandle h, byte rk)
    { var ts = r.GetTypeSpecification(h); return ts.DecodeSignature(this, gc); }
}
