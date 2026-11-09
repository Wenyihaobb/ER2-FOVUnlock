"""Inspect Il2CppInterop-generated assemblies without a .NET SDK.

Usage:
    python inspect_interop.py <assembly.dll> [keyword] [--full <TypeNameSubstring>]

Examples:
    python inspect_interop.py interop/Assembly-CSharp.dll fov
    python inspect_interop.py interop/Assembly-CSharp.dll camera --full PlayerCamera
"""
import sys

from dnfile import dnPE


def collect(path):
    pe = dnPE(path)
    out = []
    for t in pe.net.mdtables.TypeDef.rows:
        ns = t.TypeNamespace or ""
        name = t.TypeName or ""
        fields = [str(idx.row.Name) for idx in t.FieldList if idx.row is not None]
        methods = [str(idx.row.Name) for idx in t.MethodList if idx.row is not None]
        out.append((f"{ns}.{name}" if ns else name, fields, methods))
    return out


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 1
    path = sys.argv[1]
    keyword = (sys.argv[2] if len(sys.argv) > 2 else "fov").lower()
    full = None
    if "--full" in sys.argv:
        full = sys.argv[sys.argv.index("--full") + 1].lower()

    types = collect(path)
    print(f"# {path} : {len(types)} types\n")

    if full:
        for fullname, fields, methods in types:
            if full in fullname.lower():
                print(f"=== {fullname} ===")
                print("  fields:")
                for f in fields:
                    print(f"    {f}")
                print("  methods:")
                for m in methods:
                    print(f"    {m}")
                print()
        return 0

    hits = 0
    for fullname, fields, methods in types:
        mf = [f for f in fields if keyword in f.lower()]
        mm = [m for m in methods if keyword in m.lower()]
        if not mf and not mm:
            continue
        hits += 1
        print(f"=== {fullname} ===")
        for f in mf:
            print(f"  [F] {f}")
        for m in mm:
            print(f"  [M] {m}")
    print(f"\n# {hits} types matched '{keyword}'")
    return 0


if __name__ == "__main__":
    sys.exit(main())
