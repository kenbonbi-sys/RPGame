// RefPatch <patch.spec> <refs folder>
//
// The compile check builds the game against Unity 2021.x reference assemblies from NuGet.
// Unity 6 added members the game uses (Rigidbody2D.linearVelocity, ...). This tool adds
// those members to the reference assemblies in place, as signatures whose bodies throw,
// so the compiler accepts them. It never runs any Unity code.
using Mono.Cecil;
using Mono.Cecil.Cil;

if (args.Length != 2)
{
    Console.Error.WriteLine("usage: RefPatch <patch.spec> <refs folder>");
    return 2;
}
string dir = Path.GetFullPath(args[1]);
var lines = File.ReadAllLines(args[0])
    .Select(l => l.Trim())
    .Where(l => l.Length > 0 && !l.StartsWith("#"))
    .Select(l => l.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
    .ToList();

var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(dir);
var modules = new Dictionary<string, ModuleDefinition>();
var changed = new HashSet<string>();

ModuleDefinition Module(string name)
{
    if (!modules.TryGetValue(name, out var m))
    {
        var bytes = File.ReadAllBytes(Path.Combine(dir, name + ".dll"));   // read into memory: we overwrite the file later
        m = ModuleDefinition.ReadModule(new MemoryStream(bytes), new ReaderParameters { AssemblyResolver = resolver, ReadingMode = ReadingMode.Immediate });
        modules[name] = m;
    }
    return m;
}

TypeDefinition FindAnywhere(string fullName)
{
    foreach (var m in modules.Values)
        if (m.GetType(fullName) is { } t) return t;
    foreach (var file in Directory.GetFiles(dir, "*.dll"))
    {
        try
        {
            if (Module(Path.GetFileNameWithoutExtension(file)).GetType(fullName) is { } t) return t;
        }
        catch (BadImageFormatException) { }
    }
    return null;
}

TypeReference Ref(ModuleDefinition m, string name, GenericParameter t = null)
{
    switch (name)
    {
        case "void": return m.TypeSystem.Void;
        case "bool": return m.TypeSystem.Boolean;
        case "int": return m.TypeSystem.Int32;
        case "long": return m.TypeSystem.Int64;
        case "float": return m.TypeSystem.Single;
        case "double": return m.TypeSystem.Double;
        case "string": return m.TypeSystem.String;
        case "object": return m.TypeSystem.Object;
        case "T" when t != null: return t;
    }
    if (name.EndsWith("[]")) return new ArrayType(Ref(m, name[..^2], t));
    if (m.GetType(name) is { } local) return local;
    if (name.StartsWith("System."))
    {
        var clr = Type.GetType(name) ?? throw new Exception("unknown system type " + name);
        int dot = name.LastIndexOf('.');
        return new TypeReference(name[..dot], name[(dot + 1)..], m, m.TypeSystem.CoreLibrary, clr.IsValueType);
    }
    return m.ImportReference(FindAnywhere(name) ?? throw new Exception("type not found: " + name));
}

static void ThrowingBody(MethodDefinition md)
{
    var il = md.Body.GetILProcessor();
    il.Emit(OpCodes.Ldnull);
    il.Emit(OpCodes.Throw);
}

foreach (var p in lines)
{
    string op = p[0], asm = p[1], typeName = p[2];
    var m = Module(asm);
    var type = m.GetType(typeName) ?? throw new Exception($"{asm}: no type {typeName}");
    bool isStatic = p.Contains("static");
    const MethodAttributes Public = MethodAttributes.Public | MethodAttributes.HideBySig;
    const MethodAttributes Accessor = Public | MethodAttributes.SpecialName;
    switch (op)
    {
        case "prop":
        {
            if (type.Properties.Any(x => x.Name == p[3])) continue;
            var rt = Ref(m, p[4]);
            var attrs = Accessor | (isStatic ? MethodAttributes.Static : 0);
            var get = new MethodDefinition("get_" + p[3], attrs, rt);
            ThrowingBody(get);
            type.Methods.Add(get);
            var prop = new PropertyDefinition(p[3], PropertyAttributes.None, rt) { GetMethod = get };
            if (p.Contains("set"))
            {
                var set = new MethodDefinition("set_" + p[3], attrs, m.TypeSystem.Void);
                set.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, rt));
                ThrowingBody(set);
                type.Methods.Add(set);
                prop.SetMethod = set;
            }
            type.Properties.Add(prop);
            break;
        }
        case "getter":
        {
            var prop = type.Properties.First(x => x.Name == p[3]);
            if (prop.GetMethod != null) continue;
            var get = new MethodDefinition("get_" + p[3], Accessor, Ref(m, p[4]));
            ThrowingBody(get);
            type.Methods.Add(get);
            prop.GetMethod = get;
            break;
        }
        case "method":
        case "gmethod":
        {
            var md = new MethodDefinition(p[3], Public | (isStatic ? MethodAttributes.Static : 0), m.TypeSystem.Void);
            GenericParameter gp = null;
            if (op == "gmethod")
            {
                gp = new GenericParameter("T", md);
                gp.Constraints.Add(new GenericParameterConstraint(Ref(m, "UnityEngine.Object")));
                md.GenericParameters.Add(gp);
            }
            md.ReturnType = Ref(m, p[4], gp);
            if (p[5] != "-")
            {
                int i = 0;
                foreach (var pt in p[5].Split(','))
                    md.Parameters.Add(new ParameterDefinition("arg" + i++, ParameterAttributes.None, Ref(m, pt, gp)));
            }
            string Sig(MethodDefinition x) => x.GenericParameters.Count + ":" + string.Join(",", x.Parameters.Select(q => q.ParameterType.Name));
            if (type.Methods.Any(x => x.Name == md.Name && Sig(x) == Sig(md))) continue;
            ThrowingBody(md);
            type.Methods.Add(md);
            break;
        }
        case "field":
        {
            if (type.Fields.Any(x => x.Name == p[3])) continue;
            type.Fields.Add(new FieldDefinition(p[3], FieldAttributes.Public | (isStatic ? FieldAttributes.Static : 0), Ref(m, p[4])));
            break;
        }
        case "movens":
            type.Namespace = p[3];
            break;
        default:
            throw new Exception("unknown operation " + op);
    }
    changed.Add(asm);
    Console.WriteLine($"  {op} {typeName}{(p.Length > 3 && op != "movens" ? "." + p[3] : " -> " + p[3])}");
}

foreach (var name in changed)
    modules[name].Write(Path.Combine(dir, name + ".dll"));
return 0;
